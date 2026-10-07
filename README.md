# JM Data Helper

Mali lokalni Windows helper za Viz Pilot Edge. Pilot Edge ostaje na centralnom PDS-u, a JavaScript u browseru dohvaća podatke s helpera na istom računalu (`127.0.0.1`).

## v0.1

- .NET 8 WinForms aplikacija
- lokalni Kestrel API na `http://127.0.0.1:8765`
- CORS dopušten za `http://viz-plt-mos-gwy:8177`
- Private Network Access preflight header
- `GET /api/health`
- `GET /api/dhmz/warnings?day=0|1|2`
- DHMZ scraper čita finalnu boju 21 županije + 6 morskih područja iz SVG karte
- 45 s cache
- jednostavan Test DHMZ gumb
- bez Playwrighta, browser automationa i dodatnih NuGet paketa

## JSON leveli

- `1` green
- `2` yellow
- `3` orange
- `4` red
- `0` je rezervirano za ručno "bez boje" u Viz/Pilot workflowu

## Pokretanje iz sourcea

Na Windows računalu s .NET 8 SDK:

```powershell
dotnet run
```

## Single-file EXE

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

EXE će biti u `publish\JMDataHelper.exe`.

## Pilot Edge test

U Template Builder skriptu privremeno dodaj:

```ts
vizrt.onClick = async (name: string) => {
    if (name !== "testHelper") return;

    try {
        const r = await fetch("http://127.0.0.1:8765/api/dhmz/warnings?day=1", {
            cache: "no-store"
        });

        const data = await r.json();
        console.log("JM DATA HELPER:", data);

        if (!r.ok || !data.ok) {
            throw new Error(data.error || `HTTP ${r.status}`);
        }

        app.notify("success", `Helper radi - ${data.counties.length} županija`);
    }
    catch (e) {
        console.error(e);
        app.notify("error", "JM Data Helper nije dostupan");
    }
};
```

HTML:

```html
<button vizrt-click-name="testHelper">TEST HELPER</button>
```

Ako Chromium prijavi Local Network Access / Private Network Access blokadu unatoč CORS-u, prvo provjeriti browser permission za lokalnu mrežu. Helper već vraća `Access-Control-Allow-Private-Network: true` na PNA preflight.

## DHMZ source

Helper trenutno dohvaća:

`https://meteo.hr/naslovnica-upozorenja-zupanije.php?tab=upozorenja&dan=N`

Iz HTML-a čita samo elemente `*_pointer` i finalne klase:

- `fd-c-map__regija--green`
- `fd-c-map__regija--yellow`
- `fd-c-map__regija--orange`
- `fd-c-map__regija--red`

Ako DHMZ promijeni strukturu stranice, endpoint namjerno vraća grešku umjesto pogrešnih/zelenih podataka.
