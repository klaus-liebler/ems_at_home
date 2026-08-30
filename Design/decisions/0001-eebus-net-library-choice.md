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

**Erste Hypothese widerlegt**: Direkt am geklonten Quellcode geprüft — `TryConnectAsync` hängt das
Client-Zertifikat korrekt an (`wsClient.Options.ClientCertificates.Add(_cert)`). Das war nicht die
Ursache.

**Tatsächliche Ursache (verifiziert am Quellcode, `EEBUSManager.cs:808-891`)**: `SHIPListener.cs`
pinnt für unsere **Server**-Rolle (eingehende Verbindungen) explizit
`SslProtocols.Tls12`/`EnabledSslProtocols = SslProtocols.Tls12`. Für unsere **Client**-Rolle
(`TryConnectAsync`, ausgehende Verbindung zur Wallbox) passiert das nicht — der Code ruft schlicht
`wsClient.ConnectAsync(uri, _cts.Token)` auf, den einfachen Zwei-Parameter-Overload ohne eigenen
`HttpMessageInvoker`. Per Reflection geprüft: `System.Net.WebSockets.ClientWebSocketOptions` besitzt
in .NET 10 **gar keine** `SslProtocols`-Eigenschaft mehr (anders als in älteren .NET-Framework-APIs) —
die TLS-Version/Cipher-Suite-Aushandlung läuft komplett über OS-Defaults (Windows Schannel), sofern
nicht ein eigener `SocketsHttpHandler` mit gesetzten `SslClientAuthenticationOptions`
(`EnabledSslProtocols`, ggf. `CipherSuitesPolicy`) über den *anderen* `ConnectAsync`-Overload
(`ConnectAsync(uri, HttpMessageInvoker, CancellationToken)`) durchgereicht wird. Das erklärt exakt
den beobachteten TLS-`HandshakeFailure`: Die eingebettete Ghost-Plattform der Wallbox verlangt
vermutlich strikt die SHIP-Spec-Cipher-Suite `TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256` auf TLS 1.2 —
Windows' Standard-Verhandlung für ausgehende Verbindungen bietet diese Kombination offenbar nicht
zuverlässig an.

**Erster (verworfener) Fix-Versuch**: `TryConnectAsync` per lokalem Quellcode-Fork
(`src/vendor/EEBUS.Net/`) so zu patchen, dass es einen eigenen `HttpMessageInvoker` mit
`SslClientAuthenticationOptions.EnabledSslProtocols = Tls12` und `CipherSuitesPolicy` verwendet.
**Das war unnötig und wurde wieder entfernt** — siehe nächster Abschnitt. Festgehalten hier, damit
der Denkfehler nachvollziehbar bleibt: Der Patch wurde gebaut, *bevor* die naheliegende Kontrollprobe
(unveränderter Original-Code unter Linux) gemacht wurde. Das war die falsche Reihenfolge — die
Kontrollprobe hätte zuerst kommen müssen, dann wäre der Fork nie nötig gewesen.

## Tatsächliche Ursache — kein Bibliotheks-Bug, reine Windows/.NET-Plattformgrenze (30.08.2026)

Zwei Kontrolltests am selben Tag, beide gegen die echte Wallbox:

1. `openssl s_client -connect 192.168.2.24:4711 -tls1_2 -cipher ECDHE-ECDSA-AES128-SHA256 -cert
   ems-eebus.pem.crt -key ems-eebus.key` → **voller TLS-1.2-Handshake erfolgreich** ("New, TLSv1.2,
   Cipher is ECDHE-ECDSA-AES128-SHA256"). Beweist: Unser Zertifikat, TLS 1.2 und genau diese
   Cipher-Suite sind grundsätzlich in Ordnung.
2. Ein minimaler .NET-10-Repro mit dem **unveränderten** `EEBUSManager.TryConnectAsync`-Codepfad
   (einfacher `ClientWebSocket`, `ConnectAsync(uri, token)`-Overload, **kein** eigener
   `SocketsHttpHandler`, **kein** `EnabledSslProtocols`, **kein** `CipherSuitesPolicy`) unter
   **Linux** (Docker-Container `mcr.microsoft.com/dotnet/sdk:10.0`, Ubuntu 24.04) →
   **`SUCCESS (unpatched code path) - WebSocket state: Open, SubProtocol: ship`**.

Test 2 ist der entscheidende Beweis: **Die unveränderte NuGet-Bibliothek funktioniert unter Linux
einwandfrei, ganz ohne Patch.** Der zuvor gebaute Vendoring-Fork war also nicht die Lösung — er hat
lediglich zufällig mitgeholfen, weil er auf einer Plattform (Linux) getestet wurde, die das Problem
gar nicht hat. Unter **Windows** hingegen scheitert selbst die gepatchte Variante: `new
CipherSuitesPolicy(...)` wirft dort sofort `PlatformNotSupportedException` — offiziell von Microsoft
dokumentiert: *"Platform is not a Linux system with OpenSSL 1.1.1 or higher or a macOS."*
([CipherSuitesPolicy-Konstruktor](https://learn.microsoft.com/en-us/dotnet/api/system.net.security.ciphersuitespolicy.-ctor)).
Windows/Schannel erlaubt .NET-Programmen grundsätzlich keine feingranulare
Cipher-Suite-Kontrolle pro Verbindung — das ist unabhängig vom eigenen Code eine reine
.NET/Windows-Plattformgrenze.

**Präzisierung nach weiterer Recherche (30.08.2026, Nachtrag)**: Die anfängliche Vermutung, Windows
habe `TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256` generell deaktiviert/zurückgedrängt, ist **so nicht
belegt** und laut offizieller Microsoft-Referenz sogar falsch — die Suite ist auf Windows 11 v22H2+
und Windows Server 2022 standardmäßig aktiviert (Platz 8 von ~20 in der Prioritätsliste, unterhalb
der GCM-Suiten, aber nicht deaktiviert;
[TLS Cipher Suites in Windows 11 v22H2+](https://learn.microsoft.com/en-us/windows/win32/secauthn/tls-cipher-suites-in-windows-11-v22h2)).
Die tatsächliche Windows-interne Fehlerursache bleibt daher offen zwischen zwei plausiblen,
offiziell dokumentierten Möglichkeiten (Quelle:
[.NET SslStream-Troubleshooting, Abschnitt "Client and server do not possess a common algorithm"](https://learn.microsoft.com/en-us/dotnet/core/extensions/sslstream-troubleshooting) —
Stand 24.08.2026):

1. Windows bietet die Suite zwar an, aber die eingebettete eSystems-Ghost-Plattform kommt mit der
   vollen Windows-ClientHello-Liste (viele weitere Suiten/Erweiterungen) nicht klar und lehnt pauschal
   mit `handshake_failure` ab, obwohl ein gemeinsamer Nenner eigentlich vorhanden wäre.
2. **Wahrscheinlicher**, da unser allererster .NET-Fehler exakt den dort beschriebenen Code zeigte:
   der dokumentierte Abschnitt *"Handshake message exceeds the Schannel limit"* — Schannel bricht
   mit `Win32Exception 0x80090326` (`SEC_E_ILLEGAL_MESSAGE`, "Das Format der empfangenen Nachricht
   war unerwartet oder fehlerhaft") ab, u.a. wenn *"a peer sends a long list of acceptable
   certificate issuers during mutual TLS authentication"* — genau unser Szenario (SHIP verlangt
   Client-Zertifikat, mutual TLS).

Eine abschließende Klärung, welche der beiden es ist, würde einen Wireshark/tcpdump-Mitschnitt des
Windows-ClientHello im Vergleich zum erfolgreichen OpenSSL-ClientHello erfordern (von Microsofts
eigener Troubleshooting-Doku als Vorgehen empfohlen) — nicht gemacht, da für die Konsequenz
(Linux-Deployment) ohnehin irrelevant: **Ob Ursache 1 oder 2, keine ist von .NET-Anwendungscode aus
unter Windows behebbar**, nur über OS-Konfiguration (`Enable-TlsCipherSuite`-PowerShell-Cmdlet,
Gruppenrichtlinie, oder `SCHANNEL\Messaging`-Registry-Werte) — was den Umzug auf Linux als saubere
Lösung nicht ändert.

**Entscheidung**: `EEBUS.Net` bleibt das normale **NuGet-Paket** (kein Vendoring, kein Patch, keine
Wartungslast für einen eigenen Fork). Stattdessen: **Der EEBus-Teil von `ems_at_home` muss unter
Linux laufen**, nicht unter Windows — z.B. als Docker-Container (Dockerfile liegt bei
`src/EmsAtHome.Host/Dockerfile`) oder auf einem dedizierten Linux-Host (Raspberry Pi, NAS, kleiner
Server). Das passt ohnehin zum Charakter eines dauerhaft laufenden HEMS-Dienstes und zur
bestehenden Docker-Anbindung von InfluxDB. Entwickelt/gebaut wird weiterhin bequem unter Windows.

**Offener Punkt (kein TLS-Problem mehr, sondern Netzwerk-Deployment)**: Ein Versuch, den *echten*
`EmsAtHome.Host` unter Docker Desktop (Windows, `--network host`) laufen zu lassen, zeigte kein
mDNS-Discovery der Wallbox — Docker Desktop auf Windows leitet Multicast-Verkehr aus dem physischen
LAN offenbar nicht zuverlässig in seine interne Linux-VM durch. Das ist eine reine
Docker-Desktop-Netzwerk-Einschränkung, kein Problem des eigentlichen Codes (der TLS-Verbindungsaufbau
selbst wurde bereits unabhängig auf echtem Linux bewiesen, s.o.). Für einen vollständigen
End-to-End-Test (mDNS-Discovery + Pairing + Datenverbindung in einem Prozess) braucht es entweder
einen echten Linux-Host im selben LAN, oder eine Docker-Netzwerkkonfiguration mit echtem
LAN-Zugriff (z.B. macvlan) statt Docker Desktops NAT/VM-Bridging.

## Commit-Gate (Folgeschritt, nicht Teil dieses ADRs)

Nutzer-Entscheidung: hartes Blockieren von `git commit` in diesem Repo, wenn Änderungen außerhalb
`Design/` ohne begleitende Änderung innerhalb `Design/` gestaged werden — Ausnahme über
Commit-Message-Trailer `[skip-design-check]`. Umsetzung erfolgt über den `update-config`-Skill,
nicht handgeschrieben hier.
