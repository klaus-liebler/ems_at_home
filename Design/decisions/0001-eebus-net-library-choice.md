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
- Implementiert aktuell nur **LPC/LPP** (§14a-Lastmanagement). **Kein OSCEV** (EV-Ladestromsteuerung)
  — mA-genaue Steuerung ist ein eigenständiges, noch zu bauendes SPINE-Use-Case (Milestone 2).
- Zertifikat: **selbstsigniertes ECDSA-P256**, dessen privater Schlüssel über einen **explizit
  benannten, dauerhaft gespeicherten CNG-Schlüsselcontainer** erzeugt wird
  (`tools/EmsAtHome.CertTool`, Schlüsselname `ems-at-home-eebus`), exportiert als
  `{EMS_EEBUS_CERT_DIR}/ems-eebus.pfx` (Passwort `xcenter`, von `EEBUS.Net` vorgegeben).
- Läuft **nativ unter Windows** (ebenso unter Linux). Live gegen die echte Wallbox verifiziert:
  mDNS-Discovery, TLS-Handshake und SHIP-Verbindungsaufbau erfolgreich.

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

PFX und `.key`-Dateien liegen außerhalb des Git-Repos (`EMS_EEBUS_CERT_DIR`, aktuell
`C:\Users\mail\OneDrive - HSOS\certificates`), nie im Repo selbst.

## Offene Punkte

- OSCEV/mA-Ladestromsteuerung ist noch nicht implementiert — eigenständiges Milestone 2 (eigene
  SPINE-Datenstrukturen, angelehnt an `LimitationOfPowerConsumption.cs` als nächstliegendes
  vorhandenes Use-Case-Muster in `EEBUS.Net`).
- Verbindungen brechen nach einiger Zeit ab und werden vom eingebauten Reconnect-Loop neu
  aufgebaut — vermutlich ein Keepalive-/Timeout-Verhalten, das erst mit einem echten SPINE-Use-Case
  relevant wird; nicht TLS-bezogen, noch nicht weiter untersucht.
- Ein optionales Linux-Deployment (Dockerfile, `docker-compose.yml`) bleibt für einen späteren
  dedizierten Produktiv-Host gültig, ist für den Betrieb aber nicht mehr erforderlich.

## Commit-Gate (Folgeschritt, nicht Teil dieses ADRs)

Nutzer-Entscheidung: hartes Blockieren von `git commit` in diesem Repo, wenn Änderungen außerhalb
`Design/` ohne begleitende Änderung innerhalb `Design/` gestaged werden — Ausnahme über
Commit-Message-Trailer `[skip-design-check]`. Umsetzung erfolgt über den `update-config`-Skill,
nicht handgeschrieben hier.
