# 0001: EEBus-Anbindung über die Bibliothek `EEBUS.Net`

Datum: 2026-08-30
Status: Accepted (Spike — Milestone 1)

## Kontext

Ziel ist milliampere-genaue Ladestromsteuerung der Elli Charger Connect 2 Wallbox. Modbus-Register
261 erlaubt nur 0,1A-Schritte. evcc.io erreicht echte mA-Genauigkeit über `MaxCurrentMillis(float64)`,
das den Wert direkt in eine EEBus/SPINE-Nachricht (`LoadLimitsPhase`, SPINE nativ als Fließkommazahl)
schreibt. Das erfordert, selbst ein **EEBus-SHIP-Server (CEM)** zu sein, zu dem sich die Wallbox als
Client verbindet — analog zu evccs eigenem eingebettetem Go-SHIP-Server (Port 4712, mDNS-Announcement).

## Entscheidung

**`EEBUS.Net`** (NuGet, github.com/kermigmbh/EEBUS.Net, Fork von vollautomat/EEBUS.Net, MIT-Lizenz,
`net10.0`) als SHIP/SPINE-Stack für .NET. Verifiziert direkt am Quellcode (nicht nur README):

- Implementiert aktuell nur die SPINE-Use-Cases **LPC/LPP** (§14a-Lastmanagement für z.B.
  Wärmepumpen) — **kein OSCEV** (EV-Ladestromsteuerung). mA-genaue Steuerung ist damit ein
  eigenständiges, später zu bauendes SPINE-Use-Case (Milestone 2+), keine Konfigurationssache.
- `EEBUSManager` ist ein einfaches Objekt (`new`, `.Start()`, `.Dispose()`) mit eigenem
  eingebettetem Kestrel für den SHIP-Endpunkt — als schlanker `IHostedService`-Wrapper eingebunden
  (`EebusHostedService`), keine tiefere ASP.NET-Core-Integration nötig.
- **Zertifikats-Ersetzung erfordert keinen Fork/Patch**: `CertificateGenerator.GenerateCert(basePath, subject)`
  lädt `{basePath}/{subject}.pfx` direkt, falls vorhanden (Passwort hartkodiert: `xcenter`).
  SHIP-Vertrauen ist reines SKI-Pinning (`SHA1(PublicKey)`), Zertifikatsketten-Prüfung wird nie
  durchgeführt — ein von der eigenen Root-CA signiertes Leaf-Zertifikat funktioniert daher.
- Referenz-Zertifikatserzeugung nutzt ECDSA P-256 (SHIP-Cipher-Suite) — unser Leaf entsprechend
  als ECDSA P-256 erzeugt, obwohl die Root-CA RSA ist (unproblematisch, Standard-X.509).

## Live-Verifikation gegen die echte Wallbox

Das Web-App-Bundle der aktuellen Firmware enthält eine vollständige EEBus/HEMS-Pairing-UI
(`navigation.EEBUSHEMS`, Endpunkte `/api/v2/connection/eebus`, `/available`, `/paired`):

- `GET /api/v2/connection/eebus/paired` → `200 OK`, `[]` (noch kein HEMS gekoppelt).
- `GET /api/v2/connection/eebus` (mit erhöhten Rechten) → liefert Name/SKI der Wallbox selbst:
  `{"name": "Elli-Wallbox-00029690", "ski": "7ce815f7a92dbf1271c42a4997d5876b13bdd8c8"}`.
- Erhöhte Rechte: Benutzername **`technician`** (nicht `service`/`user`, beide ergaben 401) mit dem
  aus dem Elli-PDF bekannten "Service-Passwort" — gefunden über evccs eigenes Template
  `templates/definition/charger/ghost.yaml`, das genau diesen Benutzernamen hartkodiert
  ("Techniker-Passwort — erforderlich für Phasenumschaltung und RFID-Identifikation").

Das entkräftet die Sorge, EEBus-Unterstützung sei in neueren Firmware-Versionen entfallen (die
Elli-HEMS-Whitelist-PDF nennt "Solar Manager, bis Software R04.004.041.009-elli" — das bezieht sich
auf Solar Managers eigene Zertifizierung, nicht auf EEBus-Support generell).

## Abweichung vom ursprünglichen Plan: keine LPC-Use-Case-Deklaration in Milestone 1b

Der ursprüngliche Plan sah vor, das eigene CEM mit dem bereits implementierten LPC-Use-Case zu
konfigurieren, um "die Pipeline End-to-End zu beweisen". Bei der Umsetzung wurde das bewusst
vereinfacht: **SHIP-Pairing ist Geräte-/Entity-Ebene und setzt keinen übereinstimmenden Use-Case
voraus** (Use-Case-Aushandlung passiert erst nach dem Pairing, per "Detailed Discovery"/Bindings).
Eine LPC-Deklaration, die nie tatsächlich bedient wird (kein Wärmepumpen-Szenario existiert),
wäre reine Attrappe gewesen. `EebusOptions.ToEebusSettings()` deklariert daher aktuell **keine**
Entities/Use-Cases — die ehrlichere, minimalere Konfiguration für den reinen Pairing-Test. Sobald
ein echtes LPC-Szenario ansteht (z.B. Wärmepumpen-Integration), wird das entsprechend ergänzt.

## Konsequenzen

- OSCEV/mA-Steuerung muss selbst implementiert werden (eigene SPINE-Datenstrukturen,
  `LoadControlLimit`/`ElectricalConnection`, angelehnt an `LimitationOfPowerConsumption.cs` als
  nächstliegendes vorhandenes Use-Case-Muster) — eigenständiges, größeres Milestone 2.
- Bibliothek ist jung (Fork mit 1 Star, 0 Issues) und ohne fremde Validierung — Fehler sind zu
  erwarten; README des Original-Repos (vollautomat) bestätigt, dass reale Hardware zum Testen fehlt.
- `Makaretu.Dns.Multicast.New` (mDNS) unter Windows: Multicast-UDP kann an Windows-Firewall/NIC-
  Konfiguration scheitern — bei ausbleibendem mDNS-Announcement zuerst dort prüfen, bevor ein
  SHIP-Protokollfehler vermutet wird.
- **Fallback bei Scheitern der Pairing-Spike**: (a) Wallbox bleibt auf 0,1A-Modbus-Präzision, HEMS
  wird nur für die übrigen vier Geräte genutzt, oder (b) Go-Sidecar-Ansatz mit evccs eigenen
  SHIP/SPINE-Bibliotheken statt nativer C#-Implementierung — nicht stillschweigend verwerfen,
  sondern explizit zurückmelden.

## Zertifikatserzeugung (Milestone 1b)

```
openssl ecparam -name prime256v1 -genkey -noout -out ems-eebus.key
openssl req -new -key ems-eebus.key -out ems-eebus.csr -subj "/C=DE/ST=NRW/L=Greven/O=Klaus Liebler personal/CN=ems-at-home"
openssl x509 -req -in ems-eebus.csr -CA rootCA.pem.crt -CAkey rootCA.pem.key -CAcreateserial -out ems-eebus.pem.crt -days 3000 -sha256 -extfile openssl_eebus.cnf
openssl pkcs12 -export -in ems-eebus.pem.crt -inkey ems-eebus.key -certfile rootCA.pem.crt -out ems-eebus.pfx -password pass:xcenter
```

PFX (und `.key`) liegen in `C:\Users\mail\OneDrive - HSOS\certificates\` — nie im Git-Repo.
`BasePath` wird über die Umgebungsvariable `EMS_EEBUS_CERT_DIR` gebunden, nicht über
`appsettings.json`.

## Ergebnis der Live-Spike-Testung (30.08.2026)

Mit dem in Milestone 1b beschriebenen Setup (CA-signiertes ECDSA-P256-PFX, `EebusHostedService`,
leere Entities/Use-Cases) tatsächlich gegen die echte Wallbox getestet:

1. **mDNS-Discovery funktioniert bidirektional**: Unser CEM-Host loggt beim Start
   `Remote device found: Elli-Wallbox-00029690._ship._tcp.local (7ce815f7a92dbf1271c42a4997d5876b13bdd8c8)`
   — die SKI stimmt exakt mit der zuvor per REST (`GET /api/v2/connection/eebus`) ausgelesenen SKI
   der Wallbox überein. Umgekehrt listet `GET /api/v2/connection/eebus/available` auf der Wallbox
   unser CEM (`ems-at-home-cem-1`, SKI `c5f4f069de979e626fb8968d72e540528c7d9798`).
2. **Pairing über die Wallbox-REST-API funktioniert**: `POST /api/v2/connection/eebus/paired/{unsere-SKI}`
   (Login als `technician`) liefert `200 OK`; `GET /api/v2/connection/eebus/paired` zeigt danach
   `{"connected": true, "name": "Elli-Wallbox-00029690", "ski": "..."}` — **ohne** dass eine
   Bestätigung auf unserer Seite (Web-UI/PIN) nötig war, sobald die Wallbox-SKI vorab in unserem
   `JsonTrustedSkiStore` als vertrauenswürdig eingetragen war.
3. **Aber: unser ausgehender Verbindungsaufbau (`EEBUSManager.TryConnectAsync`) schlägt fehl** mit
   einer TLS-`HandshakeFailure` ("Das Format der empfangenen Nachricht war unerwartet oder
   fehlerhaft") beim Aufbau des WebSocket/TLS-Kanals zur Wallbox. Wallbox-seitig steht trotzdem
   `"connected": true` — vermutlich optimistisch gesetzt, sobald das Pairing angenommen wurde,
   nicht erst nach erfolgreichem SHIP-Handshake.

**Hypothese** (noch nicht verifiziert): `EEBUSManager.TryConnectAsync` hängt beim ausgehenden
`ClientWebSocket` vermutlich kein eigenes Client-Zertifikat an (`ClientWebSocketOptions.ClientCertificates`).
SHIP verlangt beidseitige TLS-Client-Zertifikats-Authentifizierung (mutual TLS) — die
`SHIPListener`-Serverseite ist zwar mit `ClientCertificateRequired = true` konfiguriert, die
Client-Rolle beim ausgehenden Verbindungsaufbau wurde in der bisherigen Quellcode-Analyse nicht
geprüft. Das wäre, da die Bibliothek MIT-lizenziert ist, mit einem lokalen Patch behebbar.

**Status**: Bidirektionale SHIP-Discovery und -Pairing sind bewiesen funktionsfähig — der
grundsätzliche Ansatz (eigener CEM-Server, CA-signiertes Zertifikat, SKI-Pinning) trägt. Der
tatsächliche Datenaustausch (und damit der Weg zu OSCEV/mA-Steuerung) scheitert aktuell an diesem
mutmaßlichen Client-TLS-Problem in `EEBUS.Net` — das ist der nächste zu klärende Schritt, bevor
Milestone 2 (OSCEV) sinnvoll angegangen werden kann.

## Commit-Gate (Folgeschritt, nicht Teil dieses ADRs)

Nutzer-Entscheidung: hartes Blockieren von `git commit` in diesem Repo, wenn Änderungen außerhalb
`Design/` ohne begleitende Änderung innerhalb `Design/` gestaged werden — Ausnahme über
Commit-Message-Trailer `[skip-design-check]`. Umsetzung erfolgt über den `update-config`-Skill,
nicht handgeschrieben hier.
