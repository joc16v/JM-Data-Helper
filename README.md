# JM Data Helper v0.2.2

Lokalni Windows helper za Viz Pilot Edge. Template može ostati na centralnom PDS-u, ali helper mora raditi na računalu na kojem je otvoren browser s Pilot Edgeom. Sama zajednička uredska mreža nije dovoljna: `127.0.0.1` uvijek znači to računalo.

## Instalacija

Raspakiraj ZIP i pokreni `JMDataHelper.exe`. Uz EXE ostavi `config.json`. Potpune upute su u [INSTALL.txt](INSTALL.txt).

- **framework-dependent**: mali paket; treba **.NET 10 Desktop Runtime x64** i **ASP.NET Core Runtime 10 x64**. SDK nije potreban.
- **self-contained**: uključuje runtimeove; radi bez njihove prethodne instalacije.

Runtimeovi: https://dotnet.microsoft.com/en-us/download/dotnet/10.0

Prije zamjene izađi iz stare verzije. X i minimiziranje skrivaju prozor u tray dok helper nastavlja raditi. Za potpuno zatvaranje koristi **Izlaz** u tray izborniku. Ponovno pokretanje otvara postojeći prozor umjesto druge instance.

Pilot Edge mora ostati upotrebljiv ručno kada helper nije pokrenut ili izvor nije dostupan. Helper ne upisuje podatke u Viz: template odlučuje kada primijeniti valjan odgovor.

## Što je novo

- .NET 10, tray i jedna instanca po Windows korisniku.
- Status izvora i zadnje uspješno dohvaćanje ažuriraju se i pri zahtjevima iz Pilota.
- Vlastita JM ikona sa zupčanikom u EXE-u, prozoru i trayu.
- Dnevni logovi u `%LOCALAPPDATA%\JMDataHelper\logs`: zadnjih 14 kalendarskih dana, do dvije datoteke po danu, svaka najviše 2 MB (ukupno do 56 MB). Čišćenje pri pokretanju i prvom zapisu svakog novog dana. Preveliki pojedinačni zapisi skraćuju se.
- Podesivi port, dopušteni browser origin, timeout i cache.
- Odvojeni DHMZ parser i servis, zajednički registar statusa za buduće izvore.
- Stroga provjera svih 21 županije; nedostajuća županija je greška. Odgovor uvijek sadrži i svih 6 poznatih morskih područja.
- Cache po danu ne vrijedi nakon promjene datuma u Zagrebu; istodobni zahtjevi dijele dohvat.
- Automatske provjere i izrada obaju paketa u GitHub Actions.

## Postavke

Uredi `config.json` i ponovno pokreni helper. Zadane vrijednosti:

```json
{
  "port": 8765,
  "allowedOrigins": ["http://viz-plt-mos-gwy:8177"],
  "requestTimeoutSeconds": 15,
  "cacheSeconds": 45
}
```

Helper sluša samo na `127.0.0.1`. Origin sadrži točan protokol, hostname i port Pilota, bez putanje i završnog `/`. Promjena porta traži istu promjenu u konfiguraciji Pilot templatea. Za drugi skup postavki može se pokrenuti `JMDataHelper.exe --config "C:\putanja\config.json"`.

CORS i Private Network Access preflight dopušteni su samo navedenim originima. Ako browser traži dozvolu za lokalnu mrežu, treba je omogućiti za Pilot stranicu.

## API i kompatibilnost

- `GET /api/health`: dostupnost lokalnog API-ja, verzija i vrijeme.
- `GET /api/sources`: stanje izvora, zadnji pokušaj, uspjeh i pogreška.
- `GET /api/dhmz/warnings?day=0|1|2`: danas, sutra, prekosutra.
- Dodaj `&refresh=true` za ponovno dohvaćanje.

Warning odgovor ostaje kompatibilan: `ok`, `day`, `sourceUrl`, `fetchedAt`, `counties`, `sea`. Svaka regija ima `id`, `name`, `color`, `level`. `fetchedAt` je vrijeme uspješnog dohvaćanja, a ne objave DHMZ-a. Leveli: **0 none, 1 green, 2 yellow, 3 orange, 4 red**.

DHMZ može izostaviti morska područja bez upozorenja ili poslati njihov pointer samo s osnovnom klasom `fd-c-map__regija-v2`, bez klase boje. U oba slučaja poznato morsko područje vraća se kao `color: "none", level: 0`. Pronađene boje ostaju nepromijenjene. Ovo pravilo vrijedi samo za more: svih 21 županija i dalje mora biti prisutno s valjanom bojom, a eksplicitna neprepoznata klasa boje mora i dalje je greška.

Gumb **Otvori DHMZ JSON** otvara warning endpoint za odabrani dan (Danas/Sutra/Prekosutra) uz osvježavanje podataka. Isti odabir dana koristi i **Test DHMZ**. **Provjeri API** zasebno otvara `/api/health`.

Nevaljan dan daje HTTP 400. Nedostupan izvor ili promijenjena/nepotpuna HTML struktura daje HTTP 502 s `ok:false` i `error`. Ne vraćaju se stari podaci kao da su novi. Status pamti zadnji uspjeh, a template treba sačuvati postojeće/ručne vrijednosti kad dohvat ne uspije.

DHMZ izvor: `https://meteo.hr/naslovnica-upozorenja-zupanije.php?tab=upozorenja&dan=N`. Parser čita `*_pointer` elemente i klase `fd-c-map__regija--green/yellow/orange/red`. Ne trebaju browser automation ni dodatni NuGet paketi.

## Razvoj, provjere i pakiranje

Windows i .NET 10 SDK:

```powershell
dotnet run
dotnet run --project tests/Helper.Tests -c Release
.\publish.ps1
```

ZIP-ovi nastaju u `artifacts/`. Provjere koriste snimljene DHMZ pointere i kontrolirani HTTP servis: svih 27 regija, boje, nepotpun HTML, cache, ponoć u Zagrebu, paralelno osvježavanje, HTTP grešku, otkazivanje i oporavak. Ne ovise o dostupnosti DHMZ-a.

## Kako nastaviti razvoj i dodavati nove izvore

Cilj je da JM Data Helper ostane **jedan lokalni data gateway za više Viz/Pilot templatea**, a ne da svaki template direktno razgovara s vanjskim API-jem. Template treba poznavati samo lokalni Helper endpoint i stabilan JSON ugovor.

### 1. Jedan izvor = zaseban modul

Za svaki novi vanjski izvor napravi zaseban folder:

```text
Sources/
  Dhmz/
  NoviIzvor/
    NoviIzvorParser.cs
    NoviIzvorModels.cs
```

Ako izvor vraća JSON i ne treba poseban parser, parser može biti vrlo tanak, ali mapiranje vanjskog formata u naš interni format i dalje treba ostati izvan `Program.cs`.

Servis za dohvat i cache drži u `Services/`, npr.:

```text
Services/
  DhmzService.cs
  WeatherService.cs
```

`Program.cs` treba ostati prvenstveno mjesto za registraciju dependencyja i definiranje HTTP endpointova, a ne mjesto za veliku količinu parsiranja i poslovne logike.

### 2. Registracija novog izvora

Novi servis registriraj u DI containeru u `Program.cs`:

```csharp
builder.Services.AddSingleton<WeatherService>();
```

Zatim dodaj zaseban endpoint, npr.:

```text
/api/weather/current
/api/weather/forecast
/api/weather/wind
```

Koristi pravilo:

```text
/api/<izvor-ili-domena>/<funkcija>
```

Ne mijenjaj postojeći `/api/dhmz/warnings` ugovor zbog novog izvora.

### 3. Vanjski API format ne smije curiti u template

Ako provider vraća npr. `temp_c`, `wind_kph` ili duboko ugniježđeni JSON, Helper to treba prevesti u naš stabilan format.

Primjer:

```json
{
  "ok": true,
  "location": "Zagreb",
  "temperature": 9.4,
  "humidity": 81,
  "wind": {
    "speed": 12,
    "direction": "NE"
  },
  "condition": "cloudy",
  "fetchedAt": "2026-11-16T15:00:00+01:00"
}
```

Ako se jednog dana promijeni provider, Pilot/Viz template ne bi trebao trebati izmjenu ako se naš JSON ugovor može zadržati.

### 4. API ključevi i tajne

API ključ **ne stavljati u Pilot HTML/JavaScript** i ne commitati stvarni ključ u GitHub.

Ako novi provider traži ključ, Helper treba biti jedino mjesto koje ga koristi. Za razvoj predvidi konfiguraciju preko `config.json`, zasebne lokalne konfiguracije ili environment varijable. U repo commitati samo primjer/naziv postavke, nikad stvarni produkcijski ključ.

Ako se dodaju tajne u `config.json`, production config treba ostati lokalno i ne smije završiti u repozitoriju.

### 5. Cache i broj poziva prema provideru

Prije svakog novog endpointa odluči koliko dugo podatak smije biti star. Ne dohvaćati isti podatak ponovno za svaki preview refresh ako to nije potrebno.

Tipični početni intervali:

```text
current / observations   1–5 min
hourly forecast          5–10 min
daily forecast           15–30 min
warnings                 5–10 min
```

To su početne vrijednosti, ne obavezna pravila. Za svaki izvor ih prilagodi stvarnoj dinamici podataka i ograničenjima providera.

Ako više endpointova koristi isti upstream response, poželjno je dohvatiti ga jednom i iz cachea izvesti više naših pogleda umjesto raditi više vanjskih requestova.

### 6. SourceRegistry i dijagnostika

Svaki vanjski izvor treba biti vidljiv u `/api/sources`.

Bilježi barem:

- zadnji pokušaj dohvaćanja,
- zadnji uspjeh,
- zadnju grešku,
- po mogućnosti vrijeme podatka ili starost cachea.

Helper UI i `/api/sources` trebaju omogućiti da se brzo razlikuje problem u Helperu od problema na vanjskom API-ju.

### 7. Error handling

Endpoint ne smije vratiti neispravan ili nepotpun podatak kao da je valjan.

Preporučeni princip:

- neispravan query → HTTP 400,
- provider/network/parser problem → HTTP 502,
- odgovor s greškom → `ok:false` i smislen `error`,
- template treba zadržati postojeće/ručne vrijednosti ako nema valjanog novog odgovora.

Ako se kasnije uvede vraćanje stale cachea, to mora biti eksplicitno označeno, npr. `stale:true` i starost podatka.

### 8. Testovi prije produkcije

Za svaki novi izvor dodaj testove u `tests/Helper.Tests`.

Minimalno provjeri:

- normalan valjani response,
- nedostajuća obavezna polja,
- nepoznate vrijednosti,
- HTTP grešku,
- timeout/otkazivanje,
- cache,
- paralelne zahtjeve prema istom podatku,
- oporavak nakon greške,
- stabilnost našeg izlaznog JSON ugovora.

Testovi ne bi trebali ovisiti o živom vanjskom API-ju. Spremi reprezentativni response/fixture i koristi kontrolirani HTTP servis, kao kod DHMZ testova.

### 9. Pravilo kompatibilnosti

Postojeći produkcijski endpoint ne mijenjati na način koji može razbiti postojeći template.

Sigurno je:

- dodati novo polje u odgovor ako ga stari template ignorira,
- dodati novi endpoint,
- dodati novi izvor.

Rizično je:

- preimenovati postojeće polje,
- promijeniti tip podatka,
- promijeniti značenje vrijednosti,
- ukloniti polje koje template koristi.

Ako je breaking change stvarno potreban, napravi novi endpoint ili verziju ugovora umjesto tihog mijenjanja starog.

### 10. Branch i PR workflow

Za novu funkcionalnost ne raditi direktno na `main`.

Primjer:

```powershell
git switch main
git pull --ff-only origin main
git switch -c feature/weather-api
```

Nakon razvoja i testova:

```powershell
dotnet run --project tests/Helper.Tests -c Release
git status
git add .
git commit -m "Add weather data source"
git push -u origin feature/weather-api
```

Otvori PR prema `main`, provjeri diff i testove, tek onda merge.

`main` treba uvijek predstavljati zadnju kompletnu verziju iz koje se može napraviti produkcijski build.

### 11. Verzija i publish

Kod nove produkcijske verzije uskladi broj verzije u projektu i aplikaciji te po potrebi default vrijednost u `publish.ps1`.

Build/pakiranje:

```powershell
.\publish.ps1
```

Provjeri oba paketa u `artifacts/`, ali je za računala na kojima ne želimo održavati runtime najjednostavniji **self-contained win-x64** paket.

Prije zamjene produkcijskog Helpera potpuno ugasi staru instancu preko tray izbornika **Izlaz**, jer X samo skriva prozor i stara instanca može i dalje držati port 8765.

### 12. Kratki checklist prije mergea

Prije mergea novog izvora provjeri:

- postojeći DHMZ endpoint i dalje radi,
- `/api/health` radi,
- `/api/sources` prikazuje novi izvor,
- novi endpoint vraća stabilan i razumljiv JSON,
- nema API ključeva u repou ili Pilot HTML-u,
- cache ne radi nepotrebne vanjske pozive,
- greške ne prepisuju valjane vrijednosti u templateu,
- testovi prolaze,
- `publish.ps1` uspješno radi,
- self-contained build se može pokrenuti na čistom Windows računalu.

Ovaj pristup omogućuje da se isti Helper postupno proširi na meteo, sportske i druge podatkovne izvore bez kopiranja mrežne logike i API ključeva u svaki pojedini Viz/Pilot template.

---

## Referentni Pilot Edge template

Konkretni Meteoalarm Pilot Edge v1.2 source nalazi se u:

`examples/pilot-edge/meteoalarm-zupanije-v1.2/`

To je referenca za stvarnu integraciju Helpera s Pilot Edge payloadom: field mapping, `payloadhosting` način rada, validacija odgovora i ponašanje kod greške. Kod budućih izmjena Helpera provjeri kompatibilnost i s tim templateom.

Napomena: razvojni folder u repou sadrži naše custom source datoteke i mapping. Originalni deployment paket dodatno sadrži standardni Vizrt `payloadhosting.js` i assete `1.png`–`7.png`.

## AI / worker handoff — pročitaj prije izmjena

Ovaj odjeljak postoji zato da drugi AI/worker može nastaviti razvoj bez ponovnog rekonstruiranja konteksta projekta.

### Trenutna produkcijska baza

- Repository: `joc16v/JM-Data-Helper`
- Glavna razvojna baza: **`main`**
- Trenutna verzija: **0.2.2**
- Platforma: **Windows x64, .NET 10, WinForms + lokalni ASP.NET Core API**
- Zadani lokalni API: **`http://127.0.0.1:8765`**
- Helper mora raditi na **istom Windows računalu / istoj korisničkoj sesiji** kao browser koji prikazuje Viz Pilot Edge.
- `127.0.0.1` se namjerno koristi; Helper trenutno nije zamišljen kao centralni mrežni servis.

Prije početka rada uvijek napravi:

```powershell
git fetch origin
git switch main
git pull --ff-only origin main
git status
```

Ne polazi od stare feature grane samo zato što izgleda novije. `main` treba biti izvor istine nakon mergeanih PR-ova.

### Što je projekt

JM Data Helper je lokalni **data gateway / adapter** između Viz Pilot HTML templatea i vanjskih izvora podataka.

Željeni smjer nije:

```text
Pilot template -> direktno svaki vanjski API
```

nego:

```text
Viz Pilot HTML
      |
      v
JM Data Helper
      |
      +---- DHMZ
      +---- Weather provider
      +---- drugi budući izvori
```

Helper rješava:

- dohvat vanjskih podataka,
- API ključeve i tajne,
- timeout i error handling,
- normalizaciju podataka,
- cache,
- status izvora,
- stabilan lokalni JSON ugovor prema Pilot templateima.

Pilot template treba ostati što jednostavniji: pozvati lokalni endpoint, provjeriti odgovor i upisati podatke u Viz payload.

### Funkcionalnosti koje se NE SMIJU slučajno razbiti

Kod svake izmjene zadrži sljedeće ponašanje osim ako zadatak izričito traži drugačije:

1. `GET /api/health` mora ostati dostupan.
2. `GET /api/sources` mora ostati dostupan.
3. `GET /api/dhmz/warnings?day=0|1|2` mora zadržati postojeći JSON ugovor.
4. DHMZ parser mora i dalje strogo validirati svih 21 županiju.
5. Poznatih 6 morskih područja i dalje mora biti prisutno u izlazu.
6. Neuspješan vanjski dohvat ne smije se predstavljati kao valjani novi podatak.
7. Pilot template mora moći ostati upotrebljiv ručno ako Helper ili vanjski izvor ne radi.
8. X/minimize skrivaju Helper u tray; potpuno gašenje je preko **Izlaz**.
9. Mora ostati jedna instanca Helpera po Windows korisniku.
10. Port i dopušteni origin moraju ostati konfigurabilni.
11. Produkcijski API ključ ili druga tajna ne smije završiti u GitHub repozitoriju ni u Pilot HTML-u.

### Kako pristupiti novom zadatku

Prije kodiranja:

1. pročitaj ovaj README,
2. pregledaj postojeću strukturu `Sources/`, `Services/`, `Infrastructure/`, `Program.cs` i testove,
3. identificiraj koristi li novi podatak postojeći upstream izvor ili treba novi source,
4. definiraj **naš izlazni JSON ugovor prije implementacije**,
5. provjeri može li više templatea dijeliti isti upstream dohvat/cache,
6. tek onda mijenjaj kod.

Ne stavljaj veliki parser, mapping ili business logiku direktno u `Program.cs`.

### Pravilo za nove providere

Za novi provider preferiraj:

```text
Sources/<Provider>/
    modeli / parser / mapper

Services/
    servis za HTTP dohvat, cache i SourceRegistry

Program.cs
    DI registracija + tanki endpointi
```

Ako pet templatea koristi isti provider, cilj je **jedan provider client + zajednički cache**, a ne pet zasebnih HTTP implementacija.

Primjer:

```text
Weather provider
       |
       v
jedan upstream response/cache
       |
       +--> /api/weather/current
       +--> /api/weather/forecast
       +--> /api/weather/wind
       +--> /api/weather/temperatures
```

### API key / credentials

Ako korisnik dostavi page koji trenutno u JavaScriptu ima API key:

- nemoj trajno zadržati ključ u HTML-u,
- nemoj ga commitati,
- premjesti pristup provideru u Helper,
- konfiguraciju napravi tako da se stvarni ključ može postaviti lokalno ili environment varijablom,
- u repou smije ostati samo primjer/naziv konfiguracijske stavke.

Ako nije jasno smije li se određeni credential commitati, tretiraj ga kao tajnu.

### JSON ugovori prema Pilot templateima

Preferiraj male, stabilne i čitljive DTO-e. Vanjski provider format ne smije biti javni ugovor Helpera.

Loše:

```text
Pilot ovisi o providerovim temp_c, forecast.forecastday[0]... itd.
```

Dobro:

```json
{
  "ok": true,
  "location": "Zagreb",
  "temperature": 9.4,
  "humidity": 81,
  "wind": {
    "speed": 12,
    "direction": "NE"
  },
  "condition": "cloudy",
  "fetchedAt": "..."
}
```

Time se provider kasnije može promijeniti bez prerade svih Viz templatea.

### Cache strategija

Nemoj automatski koristiti jedan globalni TTL za sve podatke.

Prvo odredi prirodu podatka:

- observation/current: kratki cache,
- hourly: srednji cache,
- daily: dulji cache,
- warnings: dovoljno često da bude aktualno, ali bez nepotrebnog bombardiranja izvora.

Ako više endpointova proizlazi iz istog upstream JSON-a, fetchaj ga jednom i dijeli cache.

### Error handling i fallback

Za novi endpoint zadrži konzistentan princip:

- neispravni argumenti -> 400,
- vanjski HTTP/parser/timeout problem -> 502,
- odgovor s greškom -> `ok:false`,
- ne vraćati nepotpun response kao uspjeh,
- Pilot ne smije automatski obrisati postojeće ručne vrijednosti zbog greške.

Ako se uvede stale-cache fallback, mora biti jasno označen u responseu, npr. `stale:true`, zajedno sa starošću podatka.

### SourceRegistry

Svaki stvarni vanjski izvor treba integrirati u zajednički statusni sustav tako da se iz `/api/sources` može vidjeti:

- zadnji pokušaj,
- zadnji uspjeh,
- zadnja greška,
- po mogućnosti stanje/starost cachea.

Kod dijagnostike prvo treba biti moguće odgovoriti na pitanje: **je li problem u lokalnom Helperu, mreži, provideru ili Pilot templateu?**

### Testovi su dio implementacije

Novi source nije završen samo zato što radi na jednom live requestu.

Dodaj fixture/testove za barem:

- valjani response,
- nepotpun response,
- nepoznatu vrijednost,
- HTTP grešku,
- timeout/cancellation,
- cache,
- paralelne requestove,
- oporavak nakon greške,
- stabilnost lokalnog JSON ugovora.

Testovi ne smiju zahtijevati da live provider bude dostupan.

### Git workflow za AI workera

Za stvarnu novu funkcionalnost napravi novu granu iz aktualnog `main`:

```powershell
git switch main
git pull --ff-only origin main
git switch -c feature/<kratak-opis>
```

Ne mergeaj samovoljno u `main` ako korisnik nije tražio merge.

Na kraju rada worker treba jasno prijaviti:

- naziv grane,
- commit SHA,
- što je promijenjeno,
- koje endpointove je dodao/promijenio,
- kako je testirao,
- ima li migration/config koraka,
- ima li nešto što korisnik još mora ručno postaviti.

### Versioning / publish

Ako se radi nova korisnička/produkcijska verzija, provjeri da su usklađeni:

- `Program.Version`,
- `JMDataHelper.csproj`,
- default verzija u `publish.ps1`,
- README naslov / release napomena ako je relevantno.

Za paket:

```powershell
.\publish.ps1
```

Produkcijski preferirani paket kada ne želimo instalirati runtime na ciljnom PC-u je **self-contained win-x64**.

### Trenutni smjer daljnjeg razvoja

Sljedeći logičan korak projekta je proširiti Helper iz DHMZ-only alata u zajednički meteo data gateway.

Postoji drugi set od približno pet meteoroloških Viz/Pilot templatea koji koristi druge podatke/provider API. Kada ti templatei/pageovi budu dostupni:

1. prvo pregledaj svih pet zajedno,
2. identificiraj provider(e), zajedničke requestove i fieldove,
3. ne kopiraj njihov postojeći fetch kod direktno u svaki template,
4. predloži zajednički model i skup Helper endpointova,
5. API ključeve prebaci u Helper,
6. uvedi zajednički cache gdje ima smisla,
7. zadrži postojeći DHMZ/Meteoalarm endpoint potpuno kompatibilnim.

Cilj je da se nakon toga novi meteo template najčešće dodaje tako da koristi već postojeći Helper endpoint ili se u Helper dodaje mali novi adapter, a ne nova paralelna infrastruktura.

