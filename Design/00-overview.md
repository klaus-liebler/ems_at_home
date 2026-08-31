# ems_at_home — Architektur-Überblick

## Zweck

Home Energy Management System (HEMS) in .NET/ASP.NET Core. Ausgangspunkt war die Frage, ob sich
die Ladeleistung der Elli Charger Connect 2 Wallbox milliampere-genau statt nur in 0,1A-Schritten
(Modbus-Register 261) steuern lässt. Antwort: ja, aber nur über **EEBus** (SHIP-Transport +
SPINE-Datenmodell), da der zugrundeliegende SPINE-Wert dort nativ als Fließkommazahl übertragen
wird. Das ist der Auslöser für dieses Projekt; die vier weiter unten genannten Geräte sind das
langfristige Ziel.

## Projekte

```
src/
  EmsAtHome.Abstractions/   IEnergyDevice, IMeter, ICharger, IBattery, DeviceRegistry, MeasurementSample
                            — stabiler Erweiterungspunkt für künftige Gerätetreiber
  EmsAtHome.Eebus/          Wrapper um EEBUS.Net (SHIP/SPINE-Stack):
                            EebusHostedService, EebusOptions, CertificateProvisioning,
                            ITrustedSkiStore/JsonTrustedSkiStore
  EmsAtHome.Host/           ASP.NET-Core-Kompositionswurzel (Program.cs), registriert EebusHostedService,
                            Dockerfile (optionales Linux-Deployment - siehe Hinweis unten)
tools/
  EmsAtHome.CertTool/       Erzeugt das EEBus-Zertifikat mit korrekt benanntem, dauerhaftem
                            CNG-Schlüssel (siehe ADR-0001) - notwendig für den nativen Windows-Betrieb
test/
  EmsAtHome.Eebus.Tests/    xUnit-Tests für Eebus-Bausteine
```

**Läuft nativ unter Windows** (ebenso unter Linux, z.B. WSL2/Docker, falls später gewünscht). Das
normale `EEBUS.Net`-NuGet-Paket bleibt unverändert (kein Fork/Patch) — die Voraussetzung ist ein
korrekt erzeugtes Zertifikat, siehe ADR-0001 (Rationale) für die Begründung. Live gegen die echte
Wallbox verifiziert (mDNS-Discovery + TLS-Handshake + SHIP-Verbindung erfolgreich). Das Dockerfile
bleibt als Option für ein späteres Linux-Produktiv-Deployment gültig, ist aber nicht erforderlich.

Bewusst noch **nicht** angelegt: `EmsAtHome.Devices.*`-Projekte für die vier physischen Geräte,
jegliche EF-Core/SQLite-Persistenz, jeglicher InfluxDB-Schreibpfad — siehe
[0001-eebus-net-library-choice.md](decisions/0001-eebus-net-library-choice.md) und die
Geräte-Roadmap unten für die Begründung.

## EEBus-Architektur (Milestone 1)

`ems_at_home` **ist selbst der EEBus-SHIP-Server** (CEM = Customer Energy Management), genau wie
evcc.io das für seine eigene Instanz macht. `EmsAtHome.Eebus` bindet das NuGet-Paket
`EEBUS.Net` 1.0.30 ein: `EEBUSManager` läuft mit eigenem eingebettetem Kestrel für den
SHIP-WebSocket-Endpunkt (Standard-Port 4712), unabhängig von der Kestrel-Instanz des eigenen
ASP.NET-Hosts. Geräte (Wallbox, später ggf. weitere) verbinden sich als SHIP-Client zu uns,
Vertrauen wird per SKI-Pinning (SHA1 des öffentlichen Zertifikatsschlüssels) hergestellt, nicht
über eine klassische PKI-Zertifikatskette. Das Zertifikat ist daher bewusst **selbstsigniert**
(nicht mehr von der eigenen Root-CA signiert wie ursprünglich geplant) — die Root-CA-Signierung
brachte keinen funktionalen Vorteil (SHIP prüft nie die Kette) und stand dem für den nativen
Windows-Betrieb nötigen Fix (benannter CNG-Schlüssel statt PFX-Import, siehe ADR-0001) im Weg.

Details, Risiken und die konkrete Zertifikats-Erzeugung: siehe
[0001-eebus-net-library-choice.md](decisions/0001-eebus-net-library-choice.md).

## Konfiguration

- `EMS_EEBUS_CERT_DIR` (Umgebungsvariable, **nicht** in `appsettings.json`): Verzeichnis mit
  `{Certificate}.pfx` (Passwort `xcenter`, siehe ADR) und der `trusted-skis.json`. Aktuell:
  `C:\Users\mail\OneDrive - HSOS\certificates`.
- `appsettings.json` in `EmsAtHome.Host` enthält nur unkritische Defaults (Port, Gerätename) —
  keine Geheimnisse.

## Datenhaltung

- **InfluxDB** (Zeitreihen: Leistung/Strom/Spannung/SoC/Temperatur, ...) läuft als Docker-Container
  (`docker-compose.yml`), aktuell reine Infrastruktur ohne Anwendungscode (kein Gerät liefert bisher
  Messwerte).
- **SQLite/EF Core** (alles andere: Geräteregister, EEBus-SKI-Vertrauensliste, Limits, Audit-Log)
  ist bewusst noch nicht angelegt — siehe ADR-0001 für die Begründung. Einzige Ausnahme:
  `JsonTrustedSkiStore` (flache JSON-Datei), da EEBus-Pairing sonst jeden Neustart verlieren würde.

## Geräte-Roadmap (noch keine Treiber, keine Dokumentation vorhanden)

Details je Gerät: [devices/](devices/)

- Wechselrichter: Huawei SUN2000
- Energiezähler: Victron VM-3P75
- Batterieladegerät: Victron Multiplus
- Batteriemanagement: JKBMS

## Governance

- **Vor jedem Commit** muss `Design/` aktualisiert werden (Ausnahme: Commit-Message enthält
  `[skip-design-check]`) — technisch durchgesetzt über einen Git-Hook (wird über den
  `update-config`-Skill separat eingerichtet, siehe Hinweis unten).
- Regelmäßige Architektur-Selbstprüfung über den Skill `architecture-review`
  (`.claude/skills/architecture-review/`) — Ergebnisse landen datiert unter
  [reviews/](reviews/).
- Neue Geräteintegrationen folgen dem Skill `add-device-integration`
  (`.claude/skills/add-device-integration/`).

**Offener Folgeschritt**: Der commit-blockierende Hook selbst ist noch nicht eingerichtet — das
sollte in einer eigenen Sitzung über den `update-config`-Skill erfolgen (siehe ADR-0001, Abschnitt
"Commit-Gate").
