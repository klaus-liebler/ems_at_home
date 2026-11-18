# 0002: OPEV-Use-Case (mA-genaue Ladestrombegrenzung) in `EEBUS.Net` integrieren

Status: Accepted (Fork-Strategie), Rest der Umsetzung noch offen
Datum: 2026-08-31

## Namenskorrektur: OPEV statt OSCEV

Der in ADR-0001 und der bisherigen Projekt-Roadmap verwendete Name **OSCEV** bezeichnet in der
SPINE-Spezifikation tatsächlich **"Optimization of Self-Consumption during EV Charging"** —
ein Use-Case, der dem EV *unverbindliche Empfehlungswerte* (`limitCategory: recommendation`) für
PV-Überschussladen mitteilt. Das hier verfolgte Ziel — Ladestrom hart und mA/A-genau begrenzen,
um eine Überlastung des Hausanschlusses zu verhindern — ist der davon getrennte Use-Case
**OPEV — "Overload Protection by EV Charging Current Curtailment"** (UC TS v1.0.1b), der
*verbindliche* Limits (`limitCategory: obligation`) schreibt. Alle folgenden Aussagen und die
Roadmap beziehen sich auf **OPEV**. OSCEV bleibt als späteres, optionales Zusatz-Use-Case (PV-Überschuss
möglichst weich ausnutzen) im Hinterkopf, ist aber nicht Teil von Milestone 2.

Quelle für die Spezifikationsdetails: keine PDF-Analyse (nicht lokal vorhanden), sondern die
offizielle Referenzimplementierung [enbility/eebus-go](https://github.com/enbility/eebus-go)
(`usecases/cem/opev`), abgeglichen mit dem bereits in `EEBUS.Net` vorhandenen LPC-Muster.

## Spezifikation von OPEV (aus der Referenzimplementierung destilliert)

- **Aktere**: lokal `CEM` (wir), remote `EV` (die Wallbox exponiert eine SPINE-Entity vom Typ `EV`
  für das angeschlossene Fahrzeug — nicht `EVSE`).
- **Szenario 1** (mandatory): `LoadControl` + `ElectricalConnection`, jeweils **server** auf der
  EV-Entity, **client** bei uns (CEM) — spiegelbildlich zu LPC. Wir lesen
  `loadControlLimitDescriptionListData` (welche `limitId`s existieren, mit welchem
  `measurementId`), lesen `electricalConnectionParameterDescriptionListData` (welches
  `measurementId` gehört zu welcher Phase `A`/`B`/`C`), und schreiben pro Phase ein
  `loadControlLimitListData` mit:
  - `limitType = "maxValueLimit"` (nicht `signDependentAbsValueLimit` wie bei LPC)
  - `limitCategory = "obligation"`
  - `scopeType = "overloadProtection"` (nicht `activePowerLimit` wie bei LPC)
  - `unit = "A"` (nicht `W` wie bei LPC)
  - drei separate `limitId`s (eine je Phase), nicht eine wie bei LPC.
- **Szenario 2** (mandatory): Heartbeat — identisch zum bereits vorhandenen `DeviceDiagnosis`-Mechanismus
  aus LPC (`DeviceDiagnosisHeartbeatData`, server-seitig bei uns).
- **Szenario 3** (mandatory): Operating State (`DeviceDiagnosisStateData`, server-seitig bei uns) —
  ebenfalls identisch zum LPC-Muster.
- Optional/empfohlen: `electricalConnectionPermittedValueSetListData` lesen, um geschriebene Werte
  auf den erlaubten Min/Max-Bereich zu clampen (die Referenzimplementierung nutzt das als Fallback,
  wenn ein Limit inaktiv ist, um den "maximal möglichen" Wert zu berichten).

## Kernbefund: `EEBUS.Net` erlaubt kein rein additives Plugin für OPEV

`EEBUS.Net` ist intern als **Registry-Pattern** aufgebaut: `UseCase`, `Feature`, `Entity` und
`SpineCmdPayloadBase` haben je ein `protected static Dictionary<string, Class>` plus eine
`Register(...)`-Methode. Da diese Methoden `protected static` (nicht `private`) sind, können
externe Assemblies (wie `EmsAtHome.Eebus`) eigene Unterklassen registrieren — **das funktioniert
tatsächlich für alles, dessen SPINE-Funktionsname noch nicht vom Kernpaket belegt ist** (z. B. ein
komplett neuer `UseCase` unter dem Schlüssel `"overloadProtectionByEvChargingCurrentCurtailment-CEM"`).

**Aber**: `EEBUSManager`s Konstruktor bootstrapped beim Start unbedingt alle Typen in
`EEBUS.SPINE.Commands`, `EEBUS.Features`, `EEBUS.UseCases.*` etc. **innerhalb der `EEBUS.Net`-Assembly
selbst** (`RuntimeHelpers.RunClassConstructor` über `GetTypesInNamespace(typeof(Settings).Assembly, ns)`,
siehe `EEBUSManager.cs` Zeilen 71–77). Das bedeutet, die im Kernpaket bereits vorhandenen Klassen
`LoadControlLimitListData` und `LoadControlLimitDescriptionListData` registrieren sich **immer**
unter den SPINE-Funktionsnamen `"loadControlLimitListData"` / `"loadControlLimitDescriptionListData"`.
Da `Register()` intern `Dictionary.Add()` verwendet (wirft bei Doppel-Key), kann eine eigene
Alternativimplementierung für dieselben Funktionsnamen **nicht** parallel registriert werden — und
da die Dictionaries keine "Replace"-Operation anbieten, kann eine externe Assembly die
Wire-Behandlung von LoadControl auch nicht überschreiben.

Das wäre unkritisch, wenn die vorhandene `LoadControlLimitDataStructure` generisch genug wäre, um
OPEV-Limits (statt nur LPC/LPP-Limits) korrekt zu repräsentieren — ist sie aber nicht:

- Ihr Konstruktor (`LoadControlLimitDataStructure(direction, value, scale, duration, active)`,
  siehe `EEBUS/Data/DataStructures/LoadControlLimitDataStructure.cs`) setzt `limitType`,
  `limitCategory`, `scopeType` **fest verdrahtet** auf `"signDependentAbsValueLimit"` /
  `"obligation"` / `"activePowerLimit"` — es gibt keinen Parameter, um `"maxValueLimit"` /
  `"overloadProtection"` zu setzen, und die Felder sind `private`.
- Beim Parsen einer *empfangenen* `loadControlLimitDescriptionListData`-Antwort
  (`LoadControlLimitDescriptionListData.EvaluateAsync`, Zeile 59) wird für unbekannte `limitId`s
  ebenfalls blind `new LoadControlLimitDataStructure(item.limitDirection, 0, 0, "", false)`
  aufgerufen — die tatsächlich vom Gerät gemeldeten Werte für `limitType`/`limitCategory`/
  `scopeType`/`unit`/`measurementId` werden verworfen und durch die LPC-Konstanten ersetzt. Für
  OPEV bräuchten wir gerade `measurementId` (zur Phasen-Zuordnung) und den echten `scopeType`
  (um OPEV-Limits von eventuell gleichzeitig vorhandenen LPC-Limits derselben Entity zu
  unterscheiden).
- `LoadControlLimitDataStructure.SendEventAsync` (Zeile 113) dispatcht ausschließlich nach
  `LimitDirection == "consume"/"produce"` an `LPCEvents`/`LPPEvents` — für OPEV bräuchte es einen
  dritten Zweig (neues `OPEVEvents`-Interface), aber diese Methode gehört zur selben, nicht
  erweiterbaren Kernklasse.

Zusätzlich fehlt die **komplette Client-Lese-Seite für `ElectricalConnection`** (Phasen ↔
`measurementId`-Zuordnung): `ElectricalConnectionClientFeature` ist eine leere Hülle ohne jede
Methode, und `ElectricalConnectionParameterDescriptionListData.CreateAnswerAsync`/`EvaluateAsync`
sind fest auf den Anwendungsfall "eigene `Measurement`-Server-Feature-Daten beantworten"
zugeschnitten (`MonitoringOfPowerConsumption`-Use-Case), nicht auf "von einer entfernten
`EV`-Entity lesen, um Phasen den `limitId`s zuzuordnen". Das ist zwar rein additiv nachrüstbar
(kein Bestandscode wird dafür verändert), aber es existiert aktuell **nichts** davon.

**Konsequenz**: Die in ADR-0001 festgelegte Leitlinie "`EEBUS.Net` unverändert, kein Fork/Patch"
lässt sich für OPEV nicht halten. ADR-0001 hat das unter "Offene Punkte" bereits angedeutet
("eigene SPINE-Datenstrukturen"), aber es geht über neue Dateien hinaus: mindestens
`LoadControlLimitDataStructure` und die beiden `LoadControlLimitListData`/
`LoadControlLimitDescriptionListData`-Klassen müssen im Kernpaket selbst geändert werden, damit sie
`limitType`/`scopeType`/`unit`/`measurementId` verlustfrei durchreichen und use-case-neutral
dispatchen.

## Vorgeschlagener Aufbau (nach Klärung der Fork-Frage, siehe unten)

1. **Im Fork (`EEBUS.Net`-Kernpaket)** — minimal-invasiv:
   - `LoadControlLimitDataStructure`: neuer Konstruktor-Überload mit `limitType`, `limitCategory`,
     `scopeType`, `unit`, `measurementId` als Parameter (Felder von `private` auf `protected`
     anheben); bestehender LPC/LPP-Konstruktor bleibt als Kompatibilitäts-Überload erhalten.
   - `LoadControlLimitDescriptionListData.EvaluateAsync`: beim Anlegen unbekannter `limitId`s die
     echten `limitType`/`limitCategory`/`scopeType`/`unit`/`measurementId` aus der Antwort
     übernehmen statt der LPC-Konstanten (behebt nebenbei einen Bug, der auch LPC/LPP beträfe,
     sobald ein Gerät mehrere Use-Cases gleichzeitig anbietet).
   - `LoadControlLimitDataStructure.SendEventAsync`: dritten Dispatch-Zweig für `scopeType ==
     "overloadProtection"` → neues `OPEVEvents`-Interface (analog `LPCEvents`).
2. **Additiv, in `EmsAtHome.Eebus` (kein Fork nötig)**:
   - Neue `UseCase`-Klasse `OverloadProtectionByEvChargingCurrentCurtailment` unter Actor `"CEM"`,
     registriert für Entity-Typ `"CEM"` (existiert bereits, `CEMEntity`), Features:
     `LoadControl`+`ElectricalConnection` client, `DeviceDiagnosis` server — 1:1 nach dem Muster
     von `EEBUS.UseCases.EnergyGuard.LimitationOfPowerConsumption`.
   - Neue Client-Lese-Logik für `ElectricalConnectionParameterDescriptionListData` gegen eine
     beliebige Remote-Entity (aktuell nicht vorhanden), um pro Phase (`acMeasuredPhases`) das
     zugehörige `measurementId` zu ermitteln und damit den passenden `limitId` aus Schritt 1
     aufzulösen — analog zu `internal/electricalConnection.go` + `internal/loadcontrol.go` aus
     der Go-Referenzimplementierung.
   - Eigenen Bootstrap-Aufruf (`RuntimeHelpers.RunClassConstructor` über die eigenen Typen in
     `EmsAtHome.Eebus`) vor `EEBUSManager`-Start ergänzen — die vorhandene Bootstrap-Schleife in
     `EEBUSManager` scannt ausschließlich die `EEBUS.Net`-Assembly selbst, nicht unsere.
   - `EebusOptions`/`Settings.Entities` um eine `UseCaseSettings`-Deklaration für OPEV auf unserer
     `"CEM"`-Entity erweitern (bisher bewusst leer, siehe Milestone-1b-Kommentar in
     `EebusOptions.cs`).
   - Kein neuer `Entity`-Subtyp nötig: eine unbekannte Remote-Entity vom Typ `"EV"` fällt bereits
     automatisch auf `EEBUS.Entities.GenericEntity` zurück.

## Entscheidung zum Fork (2026-08-31)

**Fork jetzt anlegen, Upstream-PR an `kermigmbh/EEBUS.Net` parallel vorbereiten.** Der
Parsing-Bugfix (verworfene `scopeType`/`measurementId` beim Empfang einer
`loadControlLimitDescriptionListData`-Antwort) ist auch für das Kernpaket selbst ein echter
Fehler und eigenständig contributable; OPEV als komplettes Use-Case könnte ebenfalls von Interesse
für das Upstream-Projekt sein. Ein rein additiver Ansatz ohne Fork (eigene Nachbildung des
LoadControl-Wire-Layers unter selbst gewählten SPINE-Funktionsnamen) ist technisch nicht möglich,
siehe Kernbefund oben — die SPINE-Funktionsnamen sind spezifikationsseitig fest vorgegeben und
bereits vom Kernpaket belegt.

## Nächste Schritte

1. Nutzer-Entscheidung zur Fork-Strategie (siehe oben).
2. Fork anlegen, die drei Kernpaket-Änderungen umsetzen, per lokalem NuGet-Feed oder
   `ProjectReference` statt der `PackageReference` auf `EEBUS.Net` 1.0.30 einbinden.
3. `EmsAtHome.Eebus`: OPEV-`UseCase`-Klasse, `ElectricalConnection`-Phasen-Lookup, Bootstrap-Aufruf,
   `EebusOptions`-Erweiterung.
4. Gegen die echte Wallbox verifizieren: Limit-Schreiben pro Phase, Reaktion der Wallbox
   (Ladestromreduktion), Heartbeat/Operating-State-Szenarien.
