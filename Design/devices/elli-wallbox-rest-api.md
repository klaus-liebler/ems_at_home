# Elli Wallbox — lokale REST-API (reverse-engineered)

Status: Dokumentiert (inoffiziell), kein Treiber implementiert. Grundlage für einen künftigen
`src/EmsAtHome.Devices.ElliWallbox/` (implementiert vermutlich `IMeter`/`IEvCharger` aus
`EmsAtHome.Abstractions`).

Getestetes Gerät: Elli Wallbox Pro/Connect 2 (`brand: "Elli"`, `partNumber: "139915003C"`,
`bundleVersion: "R04.004.045.303-elli"`). Andere Firmware-Stände können abweichen.

## Herkunft dieser Dokumentation

Es gibt **keine offizielle Spezifikation** dieser API (kein OpenAPI/Swagger, keine öffentliche
Doku von Elli/VW). Diese Seite fasst zwei Quellen zusammen:

1. **Eigene HAR-Aufnahme** (Chrome DevTools → Network, während die WebUI der Wallbox
   `https://wallbox-<seriennummer>.fritz.box` benutzt wurde) — liefert reale Endpunkte,
   Request/Response-Formen und tatsächliche Beispielwerte.
2. **Community-Reverse-Engineering** (Auth-Fluss und Phasenumschaltung waren in unserer eigenen
   Aufnahme nicht vollständig sichtbar):
   - [outspokende/elli-wallbox-home-assistant-ocpp](https://github.com/outspokende/elli-wallbox-home-assistant-ocpp)
   - [evcc-io/evcc Discussion #28443](https://github.com/evcc-io/evcc/discussions/28443)
   - [mawiak/hacs-elli-charger](https://github.com/mawiak/hacs-elli-charger) (nutzt intern ein
     Python-Paket `elli-client`, dessen Quellcode wir nicht einsehen konnten)
   - [Elli Charger Custom Integration – Home Assistant Community](https://community.home-assistant.io/t/elli-charger-custom-integration-monitor-your-elli-wallbox-charging-sessions/959921)
3. **Volltextsuche im WebUI-JavaScript** (Chrome DevTools → Sources → Strg+Umschalt+F nach
   `/api/v2`) — findet auch Pfade, die während einer HAR-Aufnahme nie tatsächlich aufgerufen
   wurden. Der JS-Code ist F#/Fable-kompiliert (minifiziert); wir hatten nur die reinen
   Pfad-Strings aus der Trefferliste zur Verfügung, keinen umgebenden Code (also keine
   HTTP-Methode, kein Response-Schema).

**Diese API kann sich mit jedem Firmware-Update ändern oder verschwinden** — sie ist nicht für
externe Nutzung gedacht. Ein Client sollte defensiv programmiert werden (fehlende Felder/Endpunkte
tolerieren, nicht hart failen).

## Zugriff

- Basis-URL: `https://<hostname-oder-ip>` — mDNS-Hostname beobachtet als
  `wallbox-<seriennummer>.fritz.box` (Fritz!Box-spezifisch; im allgemeinen LAN vermutlich per
  mDNS/`_https._tcp` oder direkt per IP).
- TLS mit selbstsigniertem Zertifikat (nginx) — ein Client muss das Zertifikat ignorieren/pinnen.
- **Kein API-Prefix-Login nötig für Lesen und die meisten Schreibaktionen.** In unserer Aufnahme
  antworteten alle GET-Endpunkte sowie die getesteten PUT-Endpunkte (`relais-switch/state`,
  `led/luminosity`, `led/automatic-luminosity-enabled`, `heart-beat`) mit `200`, ganz ohne
  `Authorization`-Header oder Cookie.
- Es existiert zusätzlich ein `technician`-Login (`POST /api/v2/jwt/login`,
  `application/x-www-form-urlencoded` mit Feldern `user`/`pass`) für vermutlich
  service-/werkseitige Funktionen — in unserer Aufnahme mit falschem Passwort versucht (401,
  `{"attempt": 1, "maxAttempts": 5}`). Erfolgreicher Response-Body und das exakte Bearer-Token-Format
  sind **nicht bestätigt** (Community-Quellen beschreiben ein JWT im `Authorization: Bearer <token>`-
  Header für nachfolgende Requests, aber wir konnten das nicht selbst verifizieren).
- `GET /api/v2/jwt/login/attempt-status` ohne aktive Session liefert `401`:
  `{"error": "missing JWT token or Authorization header"}`.

## Wie man an die Ladehistorie kommt (die ursprüngliche Fragestellung)

Über EEBUS/SPINE ist bei dieser Wallbox keine Energiehistorie abrufbar (siehe ADR-0003) — über
diese REST-API dagegen schon:

- **`GET /api/v2/charging/statistic`** — Array aller vergangenen Ladevorgänge (nicht nur der
  letzten!), mit `energyConsumption` (Wh) und `meterStart`/`meterEnd` (kWh) pro Session. Das ist
  der gesuchte Endpunkt für "wie viel wurde in der Vergangenheit geladen".
- **`GET /api/v2/charging/statistic/export`** — dieselben Daten als fertiges CSV, inkl.
  `ChargingCost`-Spalte.
- `GET /api/v2/charging/last-started-session` liefert nur die **eine** aktuellste/letzte Session
  (kein Array) — für einen Live-Status-Screen gedacht, nicht für Historie.
- `GET /api/v2/system/energy-meter` meldete bei diesem Gerät `"status": "notEquipped"` — es ist
  offenbar **kein geeichter Energiezähler-Chip verbaut**. Die `energyConsumption`/`meterStart`/
  `meterEnd`-Werte sind vermutlich aus Leistung×Zeit hochgerechnete Schätzungen, keine geeichten
  Messwerte (passt zur EEBUS-Bill-Spec, die genau das für ihr `chargingSummary` explizit erlaubt).

## Endpunkt-Katalog

Vollständige, maschinenlesbare Fassung mit Schemas: [`elli-wallbox-rest-api.openapi.yaml`](elli-wallbox-rest-api.openapi.yaml).
Hier nur eine kommentierte Übersicht, gruppiert nach Bereich. `?†` markiert durchgängig: nur der
Pfad-String per Volltextsuche im WebUI-JavaScript gefunden — Methode und Response-Form unbekannt,
kein Request/Response mitgeschnitten.

### Charging (`/api/v2/charging/*`)

| Methode | Pfad | Bedeutung |
|---|---|---|
| GET | `state` | Ladezustand, z. B. `"ChargingComplete"` |
| GET | `limits` | `maxCurrent` (A), `maxPower` (W, 0 = kein Limit gesetzt) |
| GET | `user-limit-current` | Vom Nutzer gesetztes Strom-Limit (A) |
| GET | `free-charging` | `{"enabled": bool}` — Laden ohne Autorisierung erlaubt |
| GET | `restrictions` | Array aktiver Ladebeschränkungen (in unserer Aufnahme leer) |
| GET | `pvoptimization/mode` | z. B. `"noPV"` — PV-Überschussladen-Modus |
| GET | `max-current-per-phase/required` | `{"required": bool}` |
| GET | `phase-type/required` | `{"required": bool}` |
| GET | **`last-started-session`** | Details zur aktuellen/letzten Session (Energie, Kosten, Fahrzeug, Zeiten) — **nur ein Objekt, keine Historie** |
| GET | `lifetime-stats` | `{"totalChargingTime": Sekunden, "totalEnergy": Wh}` (bei uns `totalEnergy: 0` — passt zum fehlenden Energiezähler) |
| GET | **`statistic`** | **Array** aller vergangenen Sessions — der Historie-Endpunkt |
| GET | **`statistic/export`** | Dieselben Daten als CSV (`text/plain`) |
| GET/PUT* | `phase-type`, `phase-type/selected` | Phasenkonfiguration (nur aus Community-Quellen, nicht selbst aufgenommen) |
| GET/PUT* | `max-current-per-phase`, `max-current-per-phase/selected` | Max. Strom pro Phase (nur aus Community-Quellen) |

`*` = laut Community-Doku, in unserer eigenen HAR-Aufnahme nicht enthalten.

### Allow-List (`/api/v2/allow-list/*`) — nur per JS-Suche gefunden, nicht verifiziert

| Methode | Pfad | Bedeutung |
|---|---|---|
| ?† | `allow-list/export` | Export einer Freigabeliste — vermutlich autorisierte RFID-Karten/Nutzer fürs Laden. Legt eine Basis-Ressource `/api/v2/allow-list` nahe (Lesen/Verwalten der Liste selbst), die als reiner String nicht im Suchtreffer auftauchte, aber plausibel existiert. |

### Dashboard

| Methode | Pfad | Bedeutung |
|---|---|---|
| GET | `dashboard/charging-curve` | Zeitreihe (`curvePoints[]`) mit Leistung/Limit pro Phase, PV-Anteil, Kosten — an eine `sessionId` gebunden |

### System (`/api/v2/system/*`)

| Methode | Pfad | Bedeutung |
|---|---|---|
| GET | `electronic-typeplate` | Typenschild: Marke, Seriennummer, Hardware-/Firmware-Version |
| GET | `device-state` | `systemState`, Laufzeit seit Start, Speicherbelegung |
| GET | `device-temperatures` | Sechs interne Temperatursensoren (°C) |
| GET | `errors` | Array aktiver/vergangener Fehlercodes (DTC) |
| ?† | `errors/export` | CSV-Export der Fehlerliste, vermutlich analog zu `charging/statistic/export` |
| ?† | `password/` | Passwortverwaltung — abschließender Slash im Suchtreffer deutet auf String-Konkatenation mit einem dynamischen Segment hin, exakter Pfad unbekannt |
| ?† | `user-config/import`, `user-config/export` | Komplettes Konfigurations-Backup/-Restore des Geräts |
| GET | `energy-meter` | Status des geeichten Energiezählers (bei uns `notEquipped`) |
| GET | `grid-related-information` | Netztyp, Strom-Grenzen (min/max A) |
| GET | `ground-monitoring`, `.../available` | Erdungsüberwachung |
| GET/PUT | `relais-switch/state`, `relais-switch/enabled` | 1-/3-phasige Umschaltung (`onePhase`/`threePhase`) |
| GET/PUT | `led/luminosity`, `led/automatic-luminosity-enabled` | LED-Helligkeit |
| GET | `certificates` | OCPP-Root-CA-Zertifikate (dekodiert) |
| GET | `date-time`, `date-time/date-format`, `date-time/time-sources/selected` | Uhrzeit/Zeitquelle (bei uns `"ocpp"` — Zeit kommt vom Backend) |
| GET | `enable-calibration-law/available`, `energy-saving`, `energy-saving/available`, `energy-management/enabled` | Feature-Flags |
| GET | `v2g/grid-regulations/available`, `v2g/grid-regulations/settings` | Vehicle-to-Grid (bei uns nicht verfügbar) |
| GET | `onboarding`, `languages/selected`, `countries/selected`, `distance-units/selected`, `temperature-units/selected`, `target-soc-time-units/selected` | UI-/Locale-Einstellungen |
| GET | `update/info` | Firmware-Update-Status |
| PUT | `heart-beat` | Von der WebUI periodisch aufgerufen (Session-Keepalive, kein Request-Body-Effekt beobachtet) |

### Connection (`/api/v2/connection/*`)

| Methode | Pfad | Bedeutung |
|---|---|---|
| GET | `eebus` | `{"name", "ski"}` — eigene EEBUS-Identität der Wallbox |
| GET | `eebus/available` | Im lokalen Netz per mDNS gefundene EEBUS-Partner (CEMs) |
| GET | `eebus/paired` | Aktuell gepaarte/verbundene EEBUS-Partner mit `connected`-Status |
| GET | `ethernet/connected`, `network/connected` | Verbindungsstatus |
| GET | `wlan/enabled`, `wlan-client/connected`, `wlan-client/available` | WLAN-Client-Status (inkl. SSID, IP, MAC, Signalstärke — **enthält personenbezogene Netzwerkdaten**, siehe Sicherheitshinweis) |
| GET | `hotspot`, `hotspot/enabled` | Eigener WLAN-Access-Point der Wallbox (**enthält das Klartext-Passwort!**) |
| GET | `lte/*` (`connected`, `status`, `enabled`, `connect`, `access-data`, `network-connections/*`, `network-type`, `auto-network-connections/enabled`) | Mobilfunk-Modul (bei diesem Gerät `notEquipped`/leer) |
| GET | `modbus/enabled`, `plc/enabled` | Weitere Schnittstellen (beide deaktiviert) |
| GET | `ocpp`, `ocpp/enabled`, `ocpp/connected`, `ocpp/remote-start-charging-enabled`, `ocpp/standard-versions`, `ocpp/vehicle-connection-timeout` | OCPP-Backend-Anbindung (Elli-Cloud) |

### Sonstiges

| Methode | Pfad | Bedeutung |
|---|---|---|
| GET | `vehicles/plugged` | `204 No Content` ohne Body — Steckerstatus vermutlich über HTTP-Status/SSE, nicht über den Response-Body kodiert |
| GET | `events` | Server-Sent-Events-Stream (`text/event-stream`) — Inhalt in unserer Aufnahme nicht mitgeschnitten, vermutlich Live-Push derselben Werte |
| POST | `jwt/login` | Form-Login (`user`, `pass`) für erweiterte Rechte |
| GET | `jwt/login/attempt-status` | Aktueller Login-/Sperr-Status |
| ?† | `jwt/logout` | Gegenstück zu `jwt/login`, vermutlich `POST` ohne Body |

## Sicherheitshinweis

Die eigene HAR-Aufnahme, aus der diese Doku stammt, enthielt reale, gerätespezifische Daten
(WLAN-Hotspot-Passwort der Wallbox, Heim-WLAN-SSID, MAC-/IP-Adressen, ein versuchtes
Technician-Login-Passwort, ein OCPP-Backend-Registry-Pfad). **Keiner dieser konkreten Werte ist in
dieser Doku oder der OpenAPI-Datei enthalten** — nur die Feldnamen/Formen. HAR-Dateien von
Wallbox-WebUIs generell nicht teilen/committen, ohne sie vorher zu bereinigen.

## Offene Fragen

- Ob `charging/statistic` paginiert oder nach Datum gefiltert werden kann (Query-Parameter) — in
  unserer Aufnahme nur ein parameterloser Aufruf beobachtet.
- Exaktes Erfolgsformat von `POST /jwt/login` (Bearer-Token-Shape) — nicht selbst verifiziert.
- Ob `meterStart`/`meterEnd` bei einer Wallbox mit geeichtem Zähler (`energy-meter.status !=
  "notEquipped"`) echte, kalibrierte Werte liefern statt der bei uns beobachteten Schätzung.
- Alle `?†`-markierten Pfade (siehe oben): HTTP-Methode und Response-/Request-Form unbekannt, nur
  der Pfad-String aus dem WebUI-JavaScript bekannt. Insbesondere `allow-list` (vermutlich RFID-
  Autorisierungsliste) und `user-config/import`/`export` (Geräte-Backup) könnten für ein HEMS
  relevant werden, sind aber noch nicht live verifiziert.
