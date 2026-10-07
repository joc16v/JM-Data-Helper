# JM Data Helper v0.2.1

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
- Provjere svih 21 županije i 6 morskih područja; nepotpun odgovor je greška, nikada automatski zeleno.
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

Warning odgovor ostaje kompatibilan: `ok`, `day`, `sourceUrl`, `fetchedAt`, `counties`, `sea`. Svaka regija ima `id`, `name`, `color`, `level`. `fetchedAt` je vrijeme uspješnog dohvaćanja, a ne objave DHMZ-a. Leveli: **1 green, 2 yellow, 3 orange, 4 red**. Nula je rezervirana za ručni Viz workflow.

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

Za novi izvor dodaj zaseban parser u `Sources/<Izvor>` i servis, registriraj ih u `Program.cs` te dodaj zaseban `/api/<izvor>/...` endpoint. Za zajednički prikaz statusa koristi `SourceRegistry`. Novi izvor ne mijenja postojeći DHMZ JSON ugovor. Dodaj snimljeni ulaz i provjere prije korištenja u produkciji.
