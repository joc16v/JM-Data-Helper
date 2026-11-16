# Meteoalarm Pilot Edge — županije v1.2

Referentni Viz Pilot Edge custom HTML template koji koristi JM Data Helper endpoint:

`GET http://127.0.0.1:8765/api/dhmz/warnings?day=0|1|2&refresh=true`

Ovaj source služi kao konkretan primjer integracije Pilot Edgea i Helpera te kao referenca za buduće AI/workere.

## Glavne datoteke

- `meteo_zupanije.html` — Custom HTML ulazna stranica.
- `meteo.js` — Pilot payload binding, dohvat iz Helpera, validacija i upis fieldova.
- `config.js` — helper URL i mapping Viz fieldova.
- `meteo.css` — UI.
- `County_Field_Mapping.csv` — DHMZ ID -> Viz field mapping.
- `PROCITAJ.txt` — originalne upute za deployment.

Originalni produkcijski paket v1.2 također sadrži standardni Vizrt `payloadhosting.js` (MIT reference implementation) i vizualne legend assete `1.png`–`7.png`. Ovdje su spremljeni naši custom source i mapping kao razvojna referenca. Za stvarni deployment koristi kompletan Pilot Edge paket sa svim assetima.

## Važan ugovor

- Županije: fieldovi `501`–`521`.
- More: `040`–`045`.
- Ikone: visibility `0200`–`0215`, color `0300`–`0315`, symbol `0400`–`0415`.
- Tekst: `naslov`, `podnaslov`, `IZVOR`, `TXT-SJEVER`.
- Leveli upozorenja: 0 bez boje, 1 zelena, 2 žuta, 3 narančasta, 4 crvena.

Template namjerno zadržava ručni rad ako Helper ili DHMZ nisu dostupni. Neuspješan dohvat ne smije pregaziti postojeće vrijednosti.
