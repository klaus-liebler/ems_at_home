# 0001: EEBus-Anbindung über `EEBUS.Net`

Status: Accepted
Datum: 2026-08-30, zuletzt aktualisiert: 2026-08-31

## Kontext

Ziel ist milliampere-genaue Ladestromsteuerung der Elli Charger Connect 2 Wallbox. Modbus-Register
261 erlaubt nur 0,1A-Schritte. evcc.io erreicht echte mA-Genauigkeit über **EEBus** (SHIP-Transport
+ SPINE-Datenmodell, `MaxCurrentMillis(float64)` schreibt den Wert nativ als Fließkommazahl). Das
erfordert, selbst ein **EEBus-SHIP-Server (CEM)** zu sein, zu dem sich die Wallbox als Client
verbindet.

## Entscheidung

- **`EEBUS.Net`** (NuGet, MIT-Lizenz, `net10.0`) als SHIP/SPINE-Stack — unverändert, kein Fork/Patch.
  `EmsAtHome.Eebus` bindet es per `EebusHostedService` (`IHostedService`) ein; die Bibliothek betreibt
  ihren eigenen eingebetteten Kestrel für den SHIP-Endpunkt (Port 4712), unabhängig vom eigenen
  ASP.NET-Host.
- Implementiert aktuell nur **LPC/LPP** (§14a-Lastmanagement). **Kein OPEV** ("Overload Protection
  by EV Charging Current Curtailment", die tatsächliche mA-genaue Ladestrom-Use-Case — zuvor
  fälschlich als "OSCEV" bezeichnet, siehe [0002-opev-usecase-integration.md](0002-opev-usecase-integration.md))
  — eigenständiges, noch zu bauendes SPINE-Use-Case (Milestone 2).
- Zertifikat: **selbstsigniertes ECDSA-P256**, dessen privater Schlüssel über einen **explizit
  benannten, dauerhaft gespeicherten CNG-Schlüsselcontainer** erzeugt wird
  (`tools/EmsAtHome.CertTool`, Schlüsselname `ems-at-home-eebus`), exportiert als
  `{Eebus:BasePath}/ems-eebus.pfx` (Passwort `xcenter`, von `EEBUS.Net` vorgegeben) —
  `Eebus:BasePath` kommt aus der gitignorten `appsettings.Local.json`, nicht aus der committeten
  `appsettings.json` (bis 2026-08-31 stattdessen Umgebungsvariable `EMS_EEBUS_CERT_DIR`, jetzt ohne
  Wirkung).
- Läuft **nativ unter Windows** (ebenso unter Linux). Live gegen die echte Wallbox verifiziert:
  mDNS-Discovery, TLS-Handshake, gepaarte SHIP-Session und vollständige Use-Case-Discovery
  (`UseCaseDiscoveryCompleted`) mit laufenden Messwert-Notifies — siehe "Offene Punkte" für die drei
  Bugs, die dahin geführt haben.

## Rationale

- **EEBUS.Net statt Go-Sidecar (evcc-Bibliotheken)**: direkt am Quellcode verifiziert, dass es ein
  funktionierender, reiner .NET-SHIP/SPINE-Stack ist — kein Interop-Prozess nötig.
- **Benannter CNG-Schlüssel statt `ECDsa.Create()` oder einfacher PFX-Import**: Windows/.NET
  (Schannel) verwirft ein Client-Zertifikat beim TLS-Handshake still, wenn dessen privater
  Schlüssel entweder "ephemeral" (`ECDsa.Create()`) oder per PFX-Import erzeugt wurde — unabhängig
  von den `X509KeyStorageFlags`. Per vollständigem Handshake-Paketmitschnitt bestätigt: Der Client
  sendet dabei eine leere `Certificate`-Nachricht, obwohl der Schlüssel selbst nachweislich
  funktioniert (Signieren/Verifizieren erfolgreich) und die Zertifikats-EKU korrekt ist. Mit OpenSSL
  und mit .NET unter Linux funktioniert derselbe Schlüssel/dasselbe Zertifikat anstandslos — das
  grenzt die Ursache eindeutig auf Windows/Schannels interne Zertifikatsauswahl ein, nicht auf die
  Wallbox oder den Zertifikatsinhalt (Cipher-Suiten, TLS-Erweiterungen und Nachrichtengröße wurden
  einzeln per Paketmitschnitt und handgebautem `ClientHello` ausgeschlossen). Ein **benannter**
  `CngKey.Create()`-Container behebt es und übersteht auch den Export/Wiederimport als PFX mit
  `EEBUS.Net`s eigenen Lade-Flags — die Bibliothek selbst brauchte daher keinen Patch.
- **Selbstsigniert statt CA-signiert**: SHIP prüft ausschließlich die SKI (SHA1 des öffentlichen
  Schlüssels) der Gegenstelle, nie eine Zertifikatskette — CA-Signierung bringt keinen
  funktionalen Vorteil und ihr bisheriger Erzeugungsweg (OpenSSL-Import) war mit dem obigen Fix
  nicht kompatibel.

## Zertifikat erzeugen/erneuern

```powershell
dotnet run --project tools/EmsAtHome.CertTool -- generate --out <cert-verzeichnis> --subject ems-eebus --cn ems-at-home
```

Nutzt den vorhandenen benannten CNG-Schlüssel `ems-at-home-eebus` weiter (oder legt ihn neu an) und
gibt die SKI des erzeugten Zertifikats aus. Diese SKI muss in der Wallbox gepaart werden — entweder
über die EEBUS/HEMS-Oberfläche der Wallbox-Web-UI, oder per REST:

```
POST https://<wallbox-ip>/api/v2/connection/eebus/paired/<unsere-ski>
Login: technician / <Service-Passwort aus dem Elli-Setup-PDF>
```

PFX und `.key`-Dateien liegen außerhalb des Git-Repos (Pfad in `appsettings.Local.json` →
`Eebus:BasePath`, aktuell `C:\Users\mail\OneDrive - HSOS\certificates`), nie im Repo selbst.

## Offene Punkte

- OPEV/mA-Ladestromsteuerung ist noch nicht implementiert — eigenständiges Milestone 2, siehe
  [0002-opev-usecase-integration.md](0002-opev-usecase-integration.md) für die recherchierte
  Spezifikation und den Integrationsplan (erfordert entgegen der obigen "kein Fork"-Linie einen
  gezielten Fork von `EEBUS.Net`, siehe dort).
- **SHIP 10.4 (WebSocket-Ping/Pong-Keepalive) fehlt weiterhin, Fix versucht und wieder verworfen
  (2026-08-31).** Primärquelle (`specs/EEBus_SHIP_TS_Specification_v1.0.1.pdf`, Kapitel 10.4) fordert
  explizit echtes Ping/Pong (Intervall ≥ 50s, Pong-Timeout 10s) — `EEBUS.Net` implementiert dies
  bisher nirgends. Versuch: `WebSocketOptions.KeepAliveInterval`/`KeepAliveTimeout` beidseitig
  (Client in `EEBUSManager.TryConnectAsync`, Server in `SHIPListener.cs`) auf 50s/10s gesetzt — das
  ist laut .NET-Dokumentation der korrekte Weg zu echtem Ping/Pong statt der sonst verwendeten
  einseitigen "Unsolicited Pong"-Heuristik. Ein direkt anschließender Live-Test zeigte jedoch zwei
  aufeinanderfolgende, von der Wallbox initiierte Verbindungen, die exakt an der historischen
  Problemstelle (erste echte SPINE-Nutzdatennachricht) mit `4450 "SHIP id mismatch"` abgelehnt
  wurden — nicht eindeutig von der Änderung unterscheidbar von der bereits dokumentierten,
  ungeklärten Wallbox-seitigen Flakiness bei stark zyklisch genutzten Identitäten
  ([[feedback_limit_live_hardware_experiments]], dieselbe Identität wurde am selben Tag bereits
  über ein Dutzend Mal verbunden/getrennt). Änderung wieder verworfen, statt sie ungeklärt stehen zu
  lassen. **Vor einem erneuten Versuch:** mit einer frisch gepaarten oder länger nicht benutzten
  Identität testen, um Wallbox-seitige Flakiness als Störfaktor auszuschließen.
- ~~Verbindungen brechen ab und werden vom eingebauten Reconnect-Loop neu aufgebaut~~ — **gelöst,
  2026-08-31 (Milestone 2):** war keine Instabilität, sondern die Wallbox hat unsere Verbindung
  aktiv abgelehnt. Root Cause per temporär lokal instrumentiertem `EEBUS.Net`-Build (ein Debug-Log
  auf den WebSocket-Close-Status ergänzt, danach wieder auf das NuGet-Paket zurückgesetzt — kein
  dauerhafter Fork) gefunden: direkt nach unserem ersten SHIP-CMI-Init-Byte schließt die Wallbox die
  Verbindung mit WebSocket-Close-Code **4452 "Node rejected by application"**. Abgleich mit der
  Git-Historie zeigt die Ursache: die SKI, die tatsächlich bei der Wallbox gepaart wurde
  (`c5f4f069de979e626fb8968d72e540528c7d9798`, aus dem alten CA-signierten Zertifikat vor Commit
  909cf48), unterscheidet sich von der SKI des aktuellen, durch den benannten-CNG-Key-Fix (Commit
  909cf48) neu erzeugten Zertifikats (aktuell z. B. `7c30179fbf7fd8b70b4b889b141bf5209fffb551` —
  ändert sich bei jeder Neuerzeugung). Die frühere Aussage "SHIP-Verbindungsaufbau erfolgreich
  verifiziert" bezog sich offenbar nur auf den TLS-Handshake/WebSocket-Upgrade, nicht auf eine
  tatsächlich gepaarte SHIP-Session mit dem aktuellen Zertifikat. **Fix durchgeführt**: die alte
  Kopplung wurde über die Wallbox-Web-UI entfernt; danach löste ein erneuter Verbindungsversuch
  unsererseits eine reguläre SHIP-Kopplungsanfrage aus, die der Nutzer über die Web-UI bestätigt
  hat. Der Pairing-REST-Call (`POST /api/v2/connection/eebus/paired/{ski}`) wurde **nicht**
  verwendet — siehe nächster Punkt, warum.
- **Pairing-REST-Call per curl/Skript nicht erfolgreich reproduzierbar (2026-08-31)**: Die
  Wallbox-Weboberfläche (`https://wallbox-00029690.fritz.box`, "WLWB WebApp") authentifiziert nicht
  per HTTP-Basic-Auth, sondern per JWT: `POST /api/v2/jwt/login`
  (`application/x-www-form-urlencoded`-Body, Antwort-Token im Response-Header `authorization`,
  danach `Authorization: <Token>` auf Folgerequests). Das exakte Feldschema des Login-Bodies konnte
  nicht aus dem minifizierten JS-Bundle (`app.*.js`) zuverlässig rekonstruiert werden — drei
  Rateversuche (`role`/`password`, `Role`/`Password`, `username`/`password`) schlugen alle fehl.
  **Wichtig**: Die Wallbox führt einen Fehlversuchs-Zähler (`{"attempt": N, "maxAttempts": 5}`,
  danach vermutlich `RemainingPenaltyTime`-Lockout) — nach den drei Fehlversuchen waren nur noch 2
  von 5 übrig, deshalb wurde das Skripten abgebrochen zugunsten des manuellen Pairings über die
  Web-UI (siehe oben). **Nicht weiter automatisiert versuchen, ohne das echte Request-Schema zu
  kennen** (z. B. per Netzwerk-Mitschnitt aus dem Browser beim manuellen Login ermitteln).
- ~~Wallbox antwortet nach `NodeDiscoveryCompleted` nicht auf dynamische Anfragen~~ — **gelöst,
  2026-08-31.** `UseCaseDiscoveryCompleted` wird jetzt zuverlässig erreicht, mit echten,
  kontinuierlichen `measurementListData`-Notifies von der realen Wallbox. Drei unabhängige Bugs
  waren beteiligt und mussten alle drei behoben werden, bevor die Verbindung durchgängig
  funktionierte:

  1. **`4450 "SHIP id mismatch"` durch instabiles Secret**: `LocalDevice.GetSecret()` (in
     `EEBUS.Net`) erzeugt bei leerem `Settings.Secret` ein *zufälliges* 16-Byte-Secret pro
     Prozessstart, das in den `SPSEC`-Teil der `ShipId` einfließt. Unsere `EebusOptions` setzte
     `Secret = string.Empty`, sodass jeder Neustart unserer App eine andere `ShipId` erzeugte und
     die Wallbox jede neue Verbindung als Struktur-Mismatch der bereits gepaarten SKI ablehnte -
     das erklärt, warum praktisch jeder Testlauf während der Entwicklung (mit ständigen
     Neustarts) fehlschlug. Fix: `EebusOptions.Secret` fest auf einen generierten 32-Zeichen-Hex-
     Wert gesetzt.
  2. **Doppelte Registrierung von `monitoringOfPowerConsumption`**: unsere Deklaration registrierte
     dieselbe Use Case zweimal - als `MonitoringAppliance` (Client) *und* als `MonitoredUnit`
     (Server, für nie benötigte eigene Dummy-Messwerte). Das ließ die Wallbox `NodeDiscoveryCompleted`
     zwar erreichen, aber jede weitere Anfrage (`nodeManagementUseCaseData`, Heartbeat-Subscription)
     unbeantwortet - ohne jede Fehlermeldung. Gefunden durch einen Vergleichstest mit einer
     minimalen Deklaration (nur die Client-Rolle) auf einer brandneuen, nie zuvor genutzten
     Identität. Fix: `MonitoredUnit`-Registrierung entfernt.
  3. **`NetworkFeatureSet = string.Empty`**: unsere Deklaration setzte dieses Feld explizit auf
     einen leeren String, wodurch `networkFeatureSet:""` in unserer eigenen
     `deviceInformation`-Beschreibung landete. Der einzige erfolgreiche Referenztest ließ das Feld
     unverändert (führt zu `null`, das Feld fehlt dann ganz in der Nachricht). Fix: das explizite
     `= string.Empty` entfernt.

  Alle drei Fixes sind in `EebusOptions.cs` dokumentiert. Zusätzlich musste die produktive Identität
  einmalig auf ein neues Zertifikat (`ems-eebus-4`, Schlüsselname `ems-at-home-eebus-4`) umgestellt
  werden - die alte, während der Fehlersuche dutzendfach umgekoppelte SKI blieb auch nach
  Neukopplung mit der korrigierten Deklaration bei "`NodeDiscoveryCompleted`, dann Abbruch" hängen;
  eine frische Identität funktionierte mit derselben (jetzt korrekten) Deklaration sofort.

  ~~Zweiter/späterer Verbindungsversuch derselben Identität unzuverlässig (4450/4452/4500)~~ -
  **gelöst, 2026-08-31.** Root Cause: `EEBUS.Net` implementierte die in SHIP 12.2.2 vorgeschriebene
  Doppelverbindungs-Arbitrierung nicht - wenn für dieselbe Gegenstelle gleichzeitig eine eingehende
  (Server-Rolle, `SHIPListener`) und eine ausgehende (Client-Rolle, `EEBUSManager`s 10s-Reconnect-
  Loop) Verbindung existieren, gewinnt laut Spec ausschließlich die von der Seite mit der **höheren
  SKI** initiierte Verbindung. Die Wallbox-SKI (`7ce8...`) ist höher als unsere (`6df0...`), ihre
  eingehende Verbindung muss also strukturell immer gewinnen. Ohne SKI-Vergleich riss unser eigener
  10s-Reconnect-Loop jede erfolgreich aufgebaute Verbindung nach wenigen Sekunden wieder ab, sobald
  die Wallbox ihrerseits verband - beobachtbar als `4450`/`4452`/`4500` je nachdem, an welcher
  Stelle im Handshake der Abbruch traf. Verifiziert per Vergleich mit einer unabhängigen
  Referenzimplementierung (`enbility/devices-app`, Go/`eebus-go`), die dasselbe Doppelverbindungs-
  Handling bereits korrekt umsetzt (`ship-go`s `keepThisConnection`, SHIP 12.2.2 explizit im
  Quellcode zitiert).

  Fix in `c:/repos/playground/EEBUS.Net` (Fork von `kermigmbh/EEBUS.Net` 1.0.30, per
  `ProjectReference` in `EmsAtHome.Eebus` eingebunden): SKI-Vergleich in beide Richtungen
  nachgerüstet - `EEBUSManager.OnDeviceConnectionRequestAsync` lehnt eine eingehende Verbindung per
  HTTP 409 ab, falls die eigene bestehende Verbindung laut SKI-Vergleich gewinnen müsste (statt sie
  bedingungslos zu schließen); `TryConnectAsync` verwirft symmetrisch die eigene neue ausgehende
  Verbindung, falls eine bereits registrierte Verbindung laut SKI-Vergleich gewinnt. Zusätzlich
  bremst der Reconnect-Loop (`OutboundBackoffForWinningPeer`, 60s) das eigene Redialing gegen eine
  Gegenstelle mit strukturell höherer SKI, solange diese kürzlich aktiv war - verhindert das sinnlose
  Nachlegen alle 10s, erlaubt aber weiterhin einen ausgehenden Verbindungsversuch nach einer
  Ruhephase (wichtig für die Wiederherstellung nach einem eigenen Neustart, bei dem die Wallbox
  nichts von unserem Neustart weiß).

  Verifiziert: vor dem Fix rissen in unter einer Minute 5 aufeinanderfolgende Verbindungen jeweils
  ~2s nach vollständigem `UseCaseDiscoveryCompleted` wieder ab. Nach dem Fix blieb eine Verbindung
  70+ Sekunden durchgängig stabil (laufende Heartbeats/Messwert-Notifies), und ein echter
  Prozess-Neustart (hartes Beenden + Neustart) verband beim allerersten Versuch sofort und ohne
  jede manuelle Kopplungsbestätigung in der Wallbox-Web-UI. Praktische Konsequenz entfällt damit:
  `EmsAtHome.Host` muss nicht mehr durchlaufen statt neu gestartet zu werden.

  Eine TLS-Session-Resumption-Hypothese (unterschiedliches Resumption-Verhalten von .NET/Schannel
  vs. Go/`crypto-tls` als Erklärung für den Unterschied zwischen den Stacks) wurde in Betracht
  gezogen, aber nicht mehr weiterverfolgt, nachdem die SKI-Arbitrierung sich als tatsächliche und
  hinreichende Ursache bestätigte.
- Ein optionales Linux-Deployment (Dockerfile, `docker-compose.yml`) bleibt für einen späteren
  dedizierten Produktiv-Host gültig, ist für den Betrieb aber nicht mehr erforderlich.

## Commit-Gate (Folgeschritt, nicht Teil dieses ADRs)

Nutzer-Entscheidung: hartes Blockieren von `git commit` in diesem Repo, wenn Änderungen außerhalb
`Design/` ohne begleitende Änderung innerhalb `Design/` gestaged werden — Ausnahme über
Commit-Message-Trailer `[skip-design-check]`. Umsetzung erfolgt über den `update-config`-Skill,
nicht handgeschrieben hier.
