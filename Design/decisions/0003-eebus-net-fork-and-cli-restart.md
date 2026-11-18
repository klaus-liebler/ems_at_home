# 0003: Neustart — In-Repo-Fork von EEBUS.Net, SHIP/SPINE-Logging, CLI-Demo

Status: Accepted
Datum: 2026-09-02

## Kontext

ADR-0001/0002 dokumentieren einen funktionierenden, aber stark ad-hoc gepatchten externen Fork
(`c:/repos/playground/EEBUS.Net`) samt ASP.NET-Dashboard-Integration. Der Nutzer hat diese Session
bewusst als Neustart mit reduziertem Scope begonnen ("zu viel schiefgelaufen"): den Fork ins Repo
holen, SHIP-Transport- und SPINE-Anwendungsebene mit einem konfigurierbaren Logger sichtbar machen,
eine kleine CLI-Demo bauen, und den Code gegen die lokalen Spezifikationen (`specs/`) prüfen.

## Entscheidung

- **Fork-Basis**: unveränderter Upstream `kermigmbh/EEBUS.Net`, Commit `24d9cd9` (= NuGet-Version
  1.0.30, per Nutzerentscheidung — nicht der Upstream-HEAD `b9aeac2`, der nur einen
  PackageReference-Versionsbump ohne Codeänderung enthält). Keine der früher gefundenen Fixes
  (benannter CNG-Zertifikatsschlüssel, SKI-Doppelverbindungs-Arbitrierung, stabiles Secret) wurde
  als Code übernommen — nur als Wissen (siehe Spec-Review-Befunde unten).
- **Ort**: `src/vendor/EEBUS.Net/` (vorher eine leere Hülle ohne echten Quellcode — nur `obj/`ohne
  `.csproj`/`.cs`). `EmsAtHome.Eebus` wurde per Nutzerentscheidung auf diese In-Repo-Referenz
  umgestellt; die alte externe `ProjectReference` auf `c:/repos/playground/EEBUS.Net` existiert in
  diesem Projekt nicht mehr.
- **Logging**: zwei eigenständige, unabhängig konfigurierbare `Microsoft.Extensions.Logging`-
  Kategorien statt eines einzigen durchgereichten `ILogger`:
  - `EEBUS.Ship` — Transportebene: rohe SHIP-Frame-Bytes ein/aus (Funktionalität war schon
    vorhanden, jetzt eine eigene, filterbare Kategorie statt eines anonymen `ILogger`),
    Handshake-/Verbindungs-Lifecycle.
  - `EEBUS.Spine` — Anwendungsebene, neu: eine dekodierte Einzeiler-Zusammenfassung pro Datagramm
    (`SpineDatagramPayload.DescribeForLog()` — cmdClassifier, Funktion, Adressen, Zähler) an den
    beiden zentralen Sammelstellen in `Connection.cs` (`PushDataMessage`/`PushDataMessageAsync` für
    ausgehend, `ReceiveAsync` für eingehend), unabhängig von den rohen SHIP-Bytes filterbar.
  - Steuerung über Standard-`Logging:LogLevel`-Konfiguration (`EEBUS.Ship`/`EEBUS.Spine` als Keys),
    kein Custom-Config-System — siehe `src/EmsAtHome.EebusCli/appsettings.json`.
- **CLI-Demo**: `src/EmsAtHome.EebusCli/`, angelehnt an Upstreams eigenes `ConsoleDemo` (nicht
  mitvendort, nur als Vorlage genutzt), um drei Kommandos erweitert:
  - `usecases <index>` — Browsing der von einem Remote-Gerät entdeckten SPINE-Use-Cases
    (roh aus `NodeManagementUseCaseData`/`Entity.UseCaseInformations`, nicht die kuratierte
    `GetUseCaseSupport()`-Teilmenge).
  - `read <index>` — liest einen Zahlenwert (Energie in kWh, `Measurement.AcEnergyConsumed`) plus
    das aktuelle Verbrauchslimit.
  - `write <index> <Watt>` — schreibt ein neues Verbrauchslimit
    (`LimitationOfPowerConsumption`, Actor `EnergyGuard`) auf das Remote-Gerät.
  Eigene Identität (Port 4713, Zertifikatsname `ems-eebus-cli`), unabhängig von `EmsAtHome.Host`s
  Identität (Port 4712) — beide könnten parallel laufen. `Eebus:BasePath` weiterhin über die
  gitignorte `appsettings.Local.json` (Vorlage: `appsettings.Local.json.example`).

## Spec-Review-Befunde (gegen `specs/`, Stand 2026-09-02)

- **Bestätigt und behoben**: SHIP 12.2.3 "Prevent Double Connections with SKI Comparison" (12.2.2
  in SHIP TS v1.0.1) war im unveränderten Upstream nicht implementiert —
  `EEBUSManager.OnDeviceConnectionRequestAsync` riss bei *jeder* eingehenden Verbindungsanfrage
  bedingungslos eine bestehende Verbindung für dieselbe SKI ab, ohne SKI-Vergleich (ein
  auskommentierter, unvollständiger Lösungsversuch stand direkt daneben im Code). Das ist exakt der
  Bug, der in der vorherigen Session live als Verbindungs-Churn beobachtet und im externen Fork
  gelöst wurde (siehe ADR-0001) — hier unabhängig neu geschrieben, nicht der alte Patch übernommen:
  eingehende Anfragen werden jetzt per HTTP 409 abgelehnt, wenn die eigene SKI höher ist und bereits
  eine offene Verbindung besteht (`DeviceConnectionRequestEventArgs.Reject`, ausgewertet in
  `SHIPListener.cs` vor `AcceptWebSocketAsync`). **Nicht mitgelöst**: die Backoff-/Redial-
  Verfeinerung auf der ausgehenden Seite (`TryConnectAsync`s 10s-Reconnect-Loop legt weiterhin
  ungebremst nach) — das erfordert Live-Verifikation gegen echte Hardware, die in dieser Session
  nicht verfügbar war ([[feedback_limit_live_hardware_experiments]]), daher bewusst nicht blind
  nachgebaut.
- **Bestätigt, nicht behoben**: SHIP 10.4 (WebSocket-Ping/Pong-Keepalive, Intervall ≥50s,
  Pong-Timeout 10s) ist weiterhin nicht implementiert (kein `KeepAliveInterval`/`KeepAliveTimeout`
  im gesamten Vendor-Code). Ein früherer Fix-Versuch lieferte ein nicht eindeutiges
  Live-Testergebnis und wurde wieder verworfen (siehe ADR-0001) — hier bewusst nicht erneut
  versucht ohne Möglichkeit zur Live-Verifikation.
- **Nicht vertieft**: keine erschöpfende Zeile-für-Zeile-Prüfung der ~14.000 Zeilen Bibliothekscode
  gegen SPINE v1.3.0. Der Review konzentrierte sich auf die SHIP-Transportebene
  (Verbindungsmanagement) als höchstem Risiko, weil dort die einzigen bereits live beobachteten
  Fehler lagen. Eine tiefere SPINE-Feldebenen-Prüfung (z. B. die in ADR-0002 dokumentierten
  OPEV-Lücken) bleibt ein offener Folgeschritt.

## Architektonische Beobachtung

Das Muster "`ILogger? logger = null` als optionaler Parameter durch praktisch jeden Konstruktor
gereicht" (nie `ILoggerFactory`/`ILogger<T>`-Injektion) ist unüblich für modernes .NET, aber im
gesamten Code konsistent durchgehalten. Die neue `spineLogger`-Instrumentierung folgt bewusst
demselben Muster statt eine `ILoggerFactory`-Injektion einzuführen, um die Diff-Fläche gegenüber
Upstream klein zu halten.

## Live-Test gegen die echte Wallbox (2026-09-02, Folgesession)

Erfolgreich: `EmsAtHome.EebusCli` verbunden mit der echten Elli-Wallbox (`Elli-Wallbox-00029690`,
SKI `7ce815f7a92dbf1271c42a4997d5876b13bdd8c8`, Entity-Typ **EVSE**). Erstverbindung einer neuen
Identität erfordert wie erwartet eine manuelle Kopplungsbestätigung auf der Wallbox-Web-UI; danach
läuft `connect` ohne weitere Bestätigung durch bis `UseCaseDiscoveryCompleted`. `usecases 0` zeigt
`limitationOfPowerConsumption` (ControllableSystem) und `monitoringOfPowerConsumption`
(MonitoredUnit); `read 0` liest Messwerte/Limit korrekt (0 W / n/a im Leerlauf ohne Fahrzeug,
konsistent mit ADR-0001). `EEBUS.Ship`/`EEBUS.Spine`-Trennung funktioniert wie entworfen. `write`
wurde bewusst nicht automatisiert getestet (setzt ein echtes Ladelimit auf der Hardware).

Dabei gefundene, im CLI-Code behobene Bugs (nicht in der vendorten Bibliothek selbst):
- Default-`Secret` in `Program.cs` hatte eine ungerade Hex-Zeichenlänge (31 statt 32) →
  `FormatException` beim Start (`LocalDevice`-Konstruktor). Gefunden im allerersten,
  verbindungsfreien Testschritt, vor jedem Netzwerkkontakt.
- `Console.Out` ist bei umgeleitetem stdout gepuffert statt zeilenweise geflusht — betrifft nur
  automatisiertes/geskriptetes Ausführen (Testharness, CI), nicht die interaktive Nutzung. Fix:
  `Console.SetOut(new StreamWriter(...) { AutoFlush = true })` in `Program.cs`.
- **Neu, reproduzierbar über mehrere Testläufe**: der erste an die CLI gesendete Befehl kurz nach
  Programmstart geht verloren (erscheint als "Unknown command"). Betrifft nur automatisiertes
  Senden direkt nach dem Start (ein interaktiver Nutzer tippt ohnehin erst, wenn der `:>`-Prompt
  sichtbar ist) — nicht weiter untersucht, da ohne Auswirkung auf normale Nutzung.
- Kosmetisch: beim `exit`/Shutdown erscheinen harmlose `ObjectDisposedException`/
  `TaskCanceledException` im Log (Verbindung wird beim Schließen von zwei Pfaden angefasst) — kein
  Funktionsproblem, nicht behoben.

## Konfigurierbare vertrauenswürdige SKIs (2026-09-02, Folgesession)

`Eebus:TrustedSkis` (String-Array) in `appsettings.Local.json` wird beim Start automatisch per
`EEBUSManager.AddTrustedSki()` übernommen — vorher musste bei *jedem* Neustart der CLI manuell
`ski <ski>` eingegeben werden, weil diese Vertrauensliste nur im Prozessspeicher lebt (anders als
die Kopplung auf Wallbox-Seite, die persistent ist). Live verifiziert: `connect` funktioniert jetzt
ohne vorherigen manuellen `ski`-Befehl.

**Neuer, noch nicht untersuchter Befund dabei**: bei einer über ~20s offengehaltenen Verbindung trat
wiederholtes Verbindungs-Auf-und-Ab auf (`OperationCanceledException`, automatischer Reconnect alle
paar Sekunden). Log zeigt, dass die Wallbox ihrerseits Reads an uns richtet
(`deviceClassificationManufacturerData`, `deviceDiagnosisStateData`, eine
`DeviceDiagnosis`-Subscription), die unsere CLI nicht beantwortet ("Was waiting for Data"-Warnungen)
— vermutlich ein Timeout, der die Verbindung danach abreißen lässt. Nicht weiter verfolgt
([[feedback_limit_live_hardware_experiments]] - kein Grund für weitere Live-Experimente ohne
konkreten Bedarf); der vorherige erfolgreiche Testlauf (siehe oben) war kürzer offengehalten und
zeigte dieses Muster nicht auffällig.

## Drei echte Bugs behoben, live verifiziert (2026-09-02, Folgesession)

Der Nutzer öffnete ein reales CLI-Log und bat um eine gründliche Prüfung, ob die darin sichtbaren
`Warning`/`Error`-Zeilen echte Probleme sind. Alle drei waren real und sind jetzt behoben (nicht nur
das Log-Level gesenkt - siehe Details):

1. **Fehlende SPINE-Fehlerantwort bei nicht unterstützten Reads**: `DataMessage.NextServerState`
   (`src/vendor/EEBUS.Net/EEBUS/SHIP/DataMessage.cs`) beantwortete einen `read`/`write`/`call`, für
   den `CreateAnswerAsync` keinen Handler fand (z. B. die Wallbox fragt uns nach
   `deviceClassificationManufacturerData`/`deviceDiagnosisStateData`, die wir nicht implementieren),
   überhaupt nicht - nur `logger?.LogWarning("Was waiting for Data: ...")`, die Gegenstelle wartete
   bis zum eigenen Timeout auf eine nie kommende Antwort. Fix: sendet jetzt eine echte
   SPINE-`resultData`-Fehlerantwort mit `errorNumber=6` ("the command is not supported by the
   recipient") - Wert und Wortlaut aus
   `specs/EEBus_SPINE_IG_ProtocolAndResourceGuidelines_V1.0.0.pdf`, Abschnitt 3.5 ("Requirements for
   the usage of error numbers in a result message"), nicht geraten. Live verifiziert: die
   "Was waiting for Data"-Warnung ist verschwunden, stattdessen erscheinen korrekte
   `resultData`-Antworten mit `errorNumber:6` im Trace.
2. **Normale Verbindungsschließung wurde als `Error` geloggt**: `Connection.ReceiveAsync` wirft
   absichtlich `OperationCanceledException`, sowohl wenn die Gegenstelle eine reguläre
   WebSocket-Close-Nachricht sendet als auch bei unserer eigenen Abbruch-Anforderung (Shutdown) -
   `Client.RunInternalAsync`/`Server.Do` fingen das bisher im generischen `catch (Exception)` und
   loggten jeden einzelnen normalen Verbindungsabbau als `LogError`. Fix: eigener
   `catch (OperationCanceledException)`-Zweig vor dem generischen Handler, loggt jetzt bei `Debug`
   statt `Error` - ein Server-seitiger "Probe"-Connect der Wallbox, der nach wenigen Millisekunden
   wieder schließt (normal, siehe unten), erscheint nicht mehr als Fehler.
3. **`ObjectDisposedException` beim Trennen einer bereits geschlossenen Verbindung**:
   `EEBUSManager.DisconnectAsync`/`Connection.PushCloseMessageAsync` versuchten unbedingt, eine
   Abschlussnachricht über den WebSocket zu senden, auch wenn die eigene Receive-Loop die Verbindung
   (Fix 2 oben) bereits selbst geschlossen und disposed hatte - klassische Doppelt-Schließen-Race.
   Fix: `WebSocket.State` wird vorher geprüft (sicher auch nach Dispose lesbar), ist er nicht mehr
   `Open`/`CloseReceived`, wird nichts mehr gesendet, kein Fehler geloggt.

Live verifiziert (85s Verbindungszeit, `connect`→`usecases`→`read`→`exit`): **keine einzige
`Error`- oder `Warning`-Zeile mehr im vollständigen Trace-Log**, sauberer Shutdown
(`Debug: Client connection closed (remote closed or cancelled).`).

**Nachtrag, gleiche Sitzung**: beim nächsten `exit` durch den Nutzer tauchte noch eine rohe, nicht
über den Logger laufende Exception-Ausgabe direkt auf der Konsole auf
(`{Zeitstempel} - System.Threading.Tasks.TaskCanceledException...` aus `MDNSService.Run`, plus ein
`fail:`-Log aus `MDNSClient.RunInternalAsync`) - zwei weitere Stellen mit demselben Muster (`Task.Delay`
mit `CancellationToken` wirft beim Shutdown erwartungsgemäß `OperationCanceledException`/
`TaskCanceledException`, wurde aber ungefiltert als Fehler behandelt bzw. per rohem
`Console.WriteLine` ausgegeben, nicht über den Logger). Beide ebenfalls auf `catch
(OperationCanceledException)` umgestellt (in `MDNSClient.cs` neu geloggt bei `Debug`, in
`MDNSService.cs` gar nicht mehr ausgegeben, da erwartetes Verhalten). Live verifiziert: `exit` ist
jetzt auf der Konsole vollständig still bis auf den Prompt.

Nebenbefund, weiterhin ungeklärt: die im vorigen Abschnitt vermutete "Verbindungs-Instabilität"
konnte in einem echten, ununterbrochenen `dotnet run`-Lauf des Nutzers **nicht** reproduziert werden
(Reconnect-Loop zeigte durchgehend "Current connections: 1", keine Churn) - vermutlich ein Artefakt
des vorherigen automatisierten PowerShell-Testaufbaus (Prozess-Kill/Interferenz), nicht ein
Bibliotheksfehler. Die frühere Notiz dazu oben ist damit vermutlich überholt, aber nicht mit
letzter Sicherheit widerlegt.

## `usecases` durch automatischen Discovery-Ausdruck bei `connect` ersetzt (2026-09-02, Folgesession)

Auf Nutzerwunsch: `connect <index>` wartet jetzt bis `UseCaseDiscoveryCompleted` (oder 3s Timeout)
und druckt danach automatisch eine eingerückte Übersicht aller entdeckten Entities, Use Cases,
Features (mit Rolle) und Funktionen (mit read/write) plus der konkreten `LoadControl`-Limit-Einträge
(limitId, direction, active, value, changeable) - alles bereits während `connect` geladene Daten,
kein zusätzlicher Netzwerk-Roundtrip. Der eigenständige `usecases`-Befehl wurde entfernt (war damit
redundant). Nebenbei behoben: `connect` meldete "Failed to connect", wenn die Wallbox uns zuerst
inbound verbunden hatte (`TryConnectAsync` gibt `false` zurück, sobald irgendeine Verbindung für die
SKI existiert, auch eine eingehende) - erkennt das jetzt und druckt trotzdem die Discovery-Daten
("Already connected (remote connected to us first).").

Live verifiziert (mit einer zuverlässigen synchronen Test-Erfassung, nachdem ein erster Testlauf per
asynchroner PowerShell-Erfassung eine verdächtig durcheinandergewürfelte Zeilenreihenfolge zeigte,
die sich als reines Test-Erfassungsartefakt herausstellte, kein echter Bug): saubere, korrekt
eingerückte Ausgabe.

## Measurement-Kanäle im Detail + ein weiterer echter Bibliotheksbug (2026-09-02, Folgesession)

Auf Nutzerfrage ("kann man Feature Measurement genauer ergründen?"): `connect` zeigt jetzt pro
`Measurement`-Feature jeden Kanal einzeln (`measurementId`, physikalische Größe, Einheit, Scope,
aktueller Wert) - Daten, die bereits während `connect` automatisch gelesen werden
(`measurementListData`/`measurementDescriptionListData`), nur bisher nicht angezeigt.

Dabei fiel eine Ungereimtheit auf (Kanal 8 zeigte "phase=c" bei einem Wert, der laut Rohdaten
eindeutig "phase=abc" sein musste) - nachverfolgt bis zu einem echten, zuvor unentdeckten Bug in
`ElectricalConnectionParameterDescriptionListData.EvaluateAsync`: das Feature, auf das die
Phasen-/Spannungstyp-Information geschrieben werden soll, wurde per `f.Index ==
datagram.header.addressSource.feature` gesucht - das ist aber der Feature-Index des *sendenden*
`ElectricalConnection`-Features (z. B. 9), nicht des `Measurement`-Features, auf das sich die Daten
inhaltlich beziehen (z. B. 8). Der Vergleich kann praktisch nie zutreffen, der gesamte
Update-Pfad war also toter Code - jede reale Phasen-Antwort wurde stillschweigend verworfen. Fix:
Suche jetzt per Typ (`entity.Features.OfType<MeasurementServerFeature>().FirstOrDefault()`) statt
per (falschem) Index. Live verifiziert: vor dem Fix zeigte Kanal 8 "phase=c" (falsch), nach dem Fix
korrekt "phase=abc" (passend zu den rohen SPINE-Daten).

**Tieferliegendes Problem gefunden dabei (zunächst unterschätzt, siehe unten für die vollständige
Aufklärung und den endgültigen Fix)**: `EEBUS.UseCases.MonitoringAppliance`/`MonitoredUnit`'s
`MonitoringOfPowerConsumption`-Konstruktor säht beim Anlegen dieses Use Cases unbedingt 10 fest
einprogrammierte Platzhalter-Kanäle (inkl. geratener Phase A/B/C-Zuordnung) in dieselbe
`MeasurementServerFeature.measurementData`-Liste, die auch von echten SPINE-Antworten befüllt wird.

## Vollständige Aufklärung: erfundene Messkanäle sind kein Anzeige-, sondern ein Architekturproblem (2026-09-02, Folgesession)

Nutzerfrage nach wiederholtem erfolglosem `read 0`: "Energy consumed so far" erschien nie, obwohl der
Kanal in der `connect`-Ausgabe gelistet war. Log-Analyse (`eebus-cli-20260902-205239.log`,
`grep '"measurementId":4'` = 0 Treffer über die gesamte Session, Deduplizierung aller 13
`measurementDescriptionListData`-Replies zeigt nur **eine** eindeutige Payload mit ausschließlich
`measurementId=8`) bewies: nicht nur die Phase war erfunden (wie oben dokumentiert), sondern **9 von
10 der angezeigten Kanäle insgesamt** - `measurementType`, `unit`, `scopeType` und `value` eingeschlossen.
Die frühere Annahme, nur die Phase sei unzuverlässig, war falsch.

**Root Cause** (per Nutzeranfrage, "das kam schon zum zweiten Mal nur durch Zufall heraus" -
systematischer Codebase-Sweep aller `UseCase`-Konstruktoren durchgeführt): `Device.SetUseCaseData()`
ruft `UseCase.Create(settings, entity)` für **jeden von der Gegenstelle gemeldeten Use Case** auf, auch
für das **Remote**-Entity, um dessen Fähigkeiten zu modellieren. Derselbe Konstruktor dient aber auch
dazu, **unser eigenes lokales** Gerät mit Platzhaltern aus `appsettings.json`
(`usecaseSettings.InitMeasurements`) zu befüllen. Bei den LoadControl/Limit-Use-Cases
(`LimitationOfPowerConsumption`/`Production`, beide Actor-Varianten) ist diese Befüllung korrekt mit
`if (usecaseSettings.InitLimits != null)` abgesichert, und `UseCaseSettings.Create()` (der Pfad für
remote-entdeckte Use Cases) setzt `InitLimits` immer auf `null` - für Remote-Entities passiert dort also
nichts. Bei den **vier Measurement-Use-Cases** fehlte exakt diese Absicherung: sie prüften nur
`measurementClient/Server != null` und säten dann unbedingt alle Kanäle mit
`usecaseSettings.InitMeasurements?.AcXyz ?? 0`. Betroffen (Sweep über alle `UseCase`-Konstruktoren im
Repo bestätigt: dies sind die einzigen vier ohne das Gate):

- `EEBUS.UseCases.MonitoringAppliance.MonitoringOfPowerConsumption` (12 Kanäle)
- `EEBUS.UseCases.MonitoredUnit.MonitoringOfPowerConsumption` (13 Kanäle)
- `EEBUS.UseCases.MonitoringAppliance.MonitoringOfGridConnectionPoint` (10 Kanäle)
- `EEBUS.UseCases.GridConnectionPoint.MonitoringOfGridConnectionPoint` (10 Kanäle)

**Fix, zweistufig**:
1. Root Cause behoben: alle vier Konstruktoren zusätzlich mit `usecaseSettings.InitMeasurements != null`
   gegated - konsistent mit dem bereits vorhandenen Muster der Limit-Use-Cases. Für Remote-Entities
   bleibt `measurementData` jetzt leer, bis echte Wire-Daten eintreffen; für unser lokales Gerät
   (wo `InitMeasurements` tatsächlich gesetzt ist) bleibt das Verhalten unverändert.
2. Als zusätzliche Absicherung (defense in depth, deckt auch andere/künftige Konsumenten dieser Liste
   ab): neues `Confirmed`-Flag auf `MeasurementData.MeasurementData`, gesetzt in
   `MeasurementDescriptionListData.EvaluateAsync` und `MeasurementListData.EvaluateAsync`, sobald eine
   echte `reply`/`notify` der Gegenstelle eintrifft. `EebusDemo.PrintDiscoveryDetails` filtert die
   Kanalliste jetzt auf `Confirmed == true` und zeigt die Anzahl ausgeblendeter, nie bestätigter Kanäle.

Konsequenz für die ursprüngliche Nutzerfrage: "Energy consumed so far" (measurementId 4,
`acEnergyConsumed`) erscheint bei dieser Wallbox nie, weil die Wallbox diesen Kanal nachweislich nie
sendet - weder per `read` noch per `notify`. Das ist eine Eigenschaft der Wallbox-Firmware (sie meldet
offenbar nur einen einzigen Summenkanal, `acPowerTotal`), kein behebbarer Bug in diesem Code. Frühere,
inzwischen falsifizierte Vermutung ("könnte per notify nachkommen") wird hiermit zurückgezogen.

Build (`dotnet build EmsAtHome.slnx`, 0 Fehler) und Testsuite (`dotnet test
test/EmsAtHome.Eebus.Tests`, 5/5 grün) nach dem Fix verifiziert. Codebase-weiter Sweep (alle
`UseCase`-Konstruktoren unter `src/vendor/EEBUS.Net/EEBUS/UseCases/`, alle `Add`/`AddUnique`-Aufrufe auf
geteilten Feature-Datenlisten) ergab keine weiteren Instanzen dieses Musters - die vier Measurement-
Konstruktoren waren die einzigen ungegateten.

## Strukturelle Absicherung: `EntityOrigin` statt impliziter Null-Checks (2026-09-02, Folgesession, auf Nutzervorschlag)

Der obige Fix gatete korrekt, aber weiterhin implizit: `usecaseSettings.InitMeasurements != null` bzw.
`InitLimits != null` als *Proxy* dafür, ob ein Entity lokal oder remote ist - leicht zu vergessen (ist
ja schon zweimal passiert) und für niemanden, der eine neue `UseCase`-Klasse schreibt, offensichtlich.
Nutzervorschlag: die Local/Remote-Unterscheidung, die auf `Device`-Ebene mit `LocalDevice`/
`RemoteDevice` bereits existiert, auch für `Entity`/`UseCase` strukturell verfügbar machen - zunächst
als Frage nach einer vollen abstrakten Basisklassen-Hierarchie gestellt, gemeinsam auf ein leichteres
Mittel geeinigt: ein `EntityOrigin`-Enum statt einer Klassenhierarchie, da >90% der Konstruktor-Logik
jeder `UseCase`-Klasse (`GetFeatures`, `GetScenarios`, `Information`) für lokal/remote identisch ist
und eine volle Hierarchie diesen Teil dupliziert oder eine weitere Abstraktionsschicht erzwungen hätte
- im vendorten, bewusst upstream-nahen Fork ein unverhältnismäßig großer Diff für den Nutzen.

Umsetzung: neues `EEBUS.Models.EntityOrigin`-Enum (`Local`/`Remote`). `Entity` bekommt eine
`public EntityOrigin Origin { get; }`-Property, gesetzt in den beiden bestehenden Konstruktoren - beide
sind bereits eindeutig getrennt, kein Erraten nötig: der `EntitySettings`-Konstruktor wird
ausschließlich von `LocalDevice` aufgerufen (`Origin = Local`), der `EntityInformationType`/wire-Daten-
Konstruktor ausschließlich für `connection.Remote` in `Device.SetDiscoveryData` (`Origin = Remote`,
verifiziert per Grep aller Aufrufstellen). Alle acht betroffenen `UseCase`-Konstruktoren (die vier
Measurement-Klassen von oben plus `LimitationOfPowerConsumption`/`Production`, beide Actor-Varianten,
plus die `PvCurtailmentLimitFactor`-Seed-Stelle in `GridConnectionPoint.MonitoringOfGridConnectionPoint`)
prüfen jetzt zusätzlich `entity.Origin == EntityOrigin.Local`. Bei den vier Measurement-Klassen ersetzt
das den bisherigen `InitMeasurements != null`-Check vollständig (Werte werden ohnehin per
`?? 0`-Default gelesen). Bei den vier Limit-Klassen bleibt der `InitLimits != null`-Check zusätzlich
bestehen, da er dort echte, optionale Konfiguration prüft (auch ein lokales Entity kann ohne
konfigurierte Limits dastehen) und nicht nur ein Local/Remote-Proxy war.

Build (0 Fehler) und Testsuite (5/5 grün) nach diesem Refactor erneut verifiziert.

## Neuer Use Case: EV Charging Summary (evChargingSummary) implementiert (2026-09-02, Folgesession)

Nutzerfrage: "Wie komme ich über EEBUS an die letzten Ladevorgänge, die die WebUI zeigt?" Antwort per
rohem `nodeManagementUseCaseData`-Reply im Log: die Wallbox meldet 6 Use Cases, wovon `EEBUS.Net`
bisher nur 2 implementierte (`limitationOfPowerConsumption`, `monitoringOfPowerConsumption`) - die
anderen vier (`evChargingSummary`, `evseCommissioningAndConfiguration`,
`measurementOfElectricityDuringEvCharging`, `overloadProtectionByEvChargingCurrentCurtailment`,
`coordinatedEvCharging`, `evCommissioningAndConfiguration`) wurden von `Device.SetUseCaseData` still
übersprungen (`if (uc == null) continue;`), da keine `UseCase`-Klasse registriert war - nicht sichtbar
in `connect`, obwohl von der Wallbox angeboten.

`evChargingSummary` (Spezifikation `specs/EEBus_UC_TS_EVChargingSummary_V1.0.1.pdf`, vom Nutzer
bereitgestellt) passt genau: EVSE hält ein `Bill`-Feature (`billDescriptionListData`, `billListData`)
mit Gesamtenergie/-kosten sowie Positionen (Netz- vs. Eigenerzeugungsanteil) pro Ladevorgang. Laut Spec
schreibt eigentlich ein externer "Energy Broker" diese Daten in die Wallbox hinein (`billListData` ist
client-`\W`) - ob die Wallbox trotzdem von sich aus sinnvolle Daten aus ihrer eigenen Ladehistorie
liefert (wie z. B. Measurement teilweise selbst-populiert), lässt sich nur per echtem `read` klären.

**Implementiert** (nur Lesepfad, kein Schreiben/Senden eigener Summaries):
- `EEBUS.Features.BillServerFeature`/`BillClientFeature` (`src/vendor/EEBUS.Net/EEBUS/Features/`) -
  serverseitig mit `List<BillDescriptionDataType> descriptions` / `List<BillDataType> bills`, beide
  ausschließlich aus echten `reply`/`notify`-Antworten befüllt (kein Seeding, siehe oben - dieses
  Muster wurde hier von Anfang an vermieden).
- `EEBUS.SPINE.Commands.BillDescriptionListData`/`BillListData` (`src/vendor/EEBUS.Net/EEBUS/SPINE/`) -
  Datentypen 1:1 aus `EEBus_SPINE_TS_Bill.xsd` abgeleitet (lokal in `specs/`), `EvaluateAsync` folgt
  exakt dem Muster von `MeasurementDescriptionListData`/`MeasurementListData`.
- Zwei `UseCase`-Klassen für dieselbe `evChargingSummary`, je einen Actor: `EEBUS.UseCases.EVSE.
  EvChargingSummary` (registriert `"evChargingSummary-EVSE"`, modelliert die Wallbox als Bill-Server)
  und `EEBUS.UseCases.CEM.EvChargingSummary` (registriert `"evChargingSummary-CEM"`, unser eigenes
  lokales Bill-Client-Feature - Actor "CEM" statt "EnergyBroker", da laut Spec-Regel Energy Guard und
  Energy Broker dieselbe Entity sind, wenn - wie hier - nur eine lokale CEM-Entity existiert).
- `Program.cs`: neuer `UseCaseSettings`-Eintrag (`Type = "evChargingSummary", Actor = "CEM"`) auf der
  lokalen CEM-Entity, damit `connection.Local.GetFeatureAddress("Bill", false)` etwas findet.
- Kein manueller Read-Code nötig: `Connection.ReadAndSubscribe()` liest nach
  `UseCaseDiscoveryCompleted` automatisch jede `read`-fähige Function jedes bekannten Features - exakt
  derselbe Mechanismus, der auch die Measurement-Kanäle ohne CLI-Zutun befüllt. Es reichte, die neuen
  Functions mit `read=true` auf `BillServerFeature` zu deklarieren.
- `EebusDemo.PrintDiscoveryDetails`: neuer Anzeige-Block direkt nach dem Measurement-Kanal-Block, zeigt
  pro `bill`-Eintrag Gesamtenergie (kWh), Gesamtkosten, Zeitraum sowie jede `position` (Netz-/
  Eigenerzeugungsanteil in %).

Build (0 Fehler) und Testsuite (5/5 grün) verifiziert.

**Live gegen die echte Wallbox getestet, Ergebnis eindeutig (2026-09-02, gleiche Session)**: Zwei
Bugs vor dem ersten funktionierenden Test gefunden und behoben:
1. `evChargingSummary` tauchte trotz korrekter Registrierung nicht in `connect`s Use-Case-Liste auf.
   Ursache: `EEBUSManager`s Konstruktor "berührt" beim Start eine fest verdrahtete Liste von
   Namespaces per Reflection (`RuntimeHelpers.RunClassConstructor`), damit die `static`-Konstruktoren
   aller `UseCase`/`Feature`-Klassen laufen und sich selbst registrieren - die neuen Namespaces
   `EEBUS.UseCases.EVSE`/`EEBUS.UseCases.CEM` fehlten in dieser Liste. `Device.SetUseCaseData` hat den
   Use Case deshalb still übersprungen (`if (uc == null) continue;`) - dieselbe Stille wie beim
   ursprünglichen "6 statt 2 Use Cases"-Fund, nur diesmal selbst verursacht. Fix: beide Namespaces zur
   Liste in `EEBUSManager.cs` ergänzt. **Bekannter Footgun**: diese Liste muss bei jeder neuen
   `UseCase`/`Feature`-Namespace manuell gepflegt werden, sonst genau dieser stille Fehlschlag.
2. CLI-Anzeige loopte über `billFeature.bills` statt `billFeature.descriptions` - eine reale, aber
   leere `billData`-Antwort (siehe unten) führte dazu, dass gar keine Zeile gedruckt wurde. Fix:
   loopt jetzt über `descriptions`, zeigt auch eine leere Bill-Antwort informativ an.

**Nach beiden Fixes, echte Antwort der Wallbox**: `billDescriptionListData` liefert real
`billId=1, billWriteable=true, updateRequired=true, supportedBillType=["chargingSummary"]`;
`billListData` liefert einen leeren `billData`-Eintrag ohne Inhalt. `updateRequired=true` ist laut
Spec (Abschnitt 3.2.1.2.2.1, Table 6) das explizite Signal: "the server can request an update of
writeable ... data from a client. To request an update the server SHALL set updateRequired to true" -
die Wallbox sagt damit unmissverständlich: sie hat selbst keine Ladehistorie-Daten und wartet auf
einen externen Energy Broker, der sie hineinschreibt. **Endgültiger Befund**: diese Wallbox
self-populiert `Bill` NICHT aus ihrer eigenen Ladehistorie (anders als bei Measurement teilweise
beobachtet) - die "letzte 3 Ladevorgänge"-Ansicht der WebUI kommt aus einem separaten, nicht über
SPINE exponierten Mechanismus (eigene Firmware-Logik oder Cloud-Backend der Wallbox). Um echte
Ladezusammenfassungen über EEBUS zu bekommen, müsste dieses Projekt selbst die "Energy Broker"-Rolle
übernehmen: aus den bereits gelesenen Measurement-Daten eigene Zusammenfassungen berechnen und aktiv
per `billListData write` in die Wallbox schreiben - eine grundsätzlich andere, zusätzliche Funktion
(Schreiben statt Lesen), nicht Teil dieser Implementierung.

## Verbindungs-Instabilität doch real - Ursache gefunden (2026-09-02, Folgesession, während einer echten Ladesession)

Die frühere Notiz ("konnte nicht reproduziert werden, vermutlich Testaufbau-Artefakt") war falsch -
während einer echten Ladesession trat das Verbindungs-Auf-und-Ab erneut auf, diesmal in einem vom
Nutzer selbst interaktiv bedienten `dotnet run` (kein automatisiertes Testskript mehr im Spiel).
Log-Beleg: Verbindung hält konsistent ~3,5-4s, bricht mit "Client connection closed" ab, ca. 6-10s
später neuer Reconnect-Zyklus - wiederholt sich alle ~10s. `read 0`-Befehle des Nutzers, die in eine
Abbruchphase fielen, liefen prompt in den 5s-internen Timeout (`Connection.PushDataMessageAsync`).

**Wahrscheinliche Ursache gefunden**: `Client.RunInternalAsync`/`Server.Do` senden das eigene
`deviceDiagnosisHeartbeatData`-Notify exakt alle 4000ms (`new Timer(..., 4000, 4000)`), während
gleichzeitig `heartbeatTimeout="PT4S"` deklariert wird (`DeviceDiagnosisHeartbeatData.cs`) - null
Sicherheitsmarge. Laut SPINE TS Resource Specification (Element-Beschreibung `heartbeatTimeout`,
Beispiel in Abschnitt 5.3.9.3.4.1 zeigt `PT15M` = 15 Minuten) ist das Feld frei wählbar, aber ein
strikt prüfender Peer, der "keine Heartbeat-Notify innerhalb der deklarierten Periode = tote
Verbindung" durchsetzt, hat bei uns keinerlei Puffer gegen Scheduling-Jitter (Timer-Ungenauigkeit,
GC, Warteschlangen-Verzögerung in `DataMessageQueue`) - jede kleine Verzögerung reißt die
Verbindung. Fix: Sendeintervall auf 1500ms verkürzt (deklarierter Timeout unverändert `PT4S`), gibt
durchgängig ~2,5s Marge.

**Live verifiziert, erfolgreich (2026-09-02, nach Neustart durch den Nutzer)**: Verbindung blieb
über einen durchgängigen 80+-Sekunden-Beobachtungszeitraum stabil (`Running Reconnect Loop. Current
connections: 1` bei jeder der 8 Prüfungen im 10s-Takt, keine einzige zwischenzeitliche
"connection closed"-Meldung) - vorher riss die Verbindung zuverlässig alle ~4s ab. `read 0` lief
nach der langen Wartezeit sofort durch, kein Timeout mehr. Die Heartbeat-Sendeintervall-Vermutung
war damit die richtige Ursache.

## Offene Punkte

- `write`-Kommando (Ladelimit setzen) noch nicht gegen die echte Wallbox getestet — bewusst
  zurückgestellt, da es aktiv in die Hardware eingreift; auf Nutzerwunsch nachholen.
- Die Platzhalter-Kanal-Verschmutzung in `MeasurementServerFeature.measurementData` ist behoben (siehe
  oben, "Vollständige Aufklärung"). `DeviceConfiguration`-Key-Values (`failsafeConsumptionActivePowerLimit`
  etc.) sind von diesem Bug nicht betroffen (Sweep bestätigt: nur die vier Measurement-Konstruktoren
  waren ungegated) - Browsing dieser Werte in der CLI ist trotzdem noch nicht implementiert, bleibt
  offener Folgeschritt für die Anzeige (kein Datenkorrektheitsproblem).
- OPEV (ADR-0002) bleibt unverändert offen, war nicht Teil dieses Neustarts.
- `tools/EmsAtHome.CertTool`s Zertifikatserzeugung (benannter CNG-Schlüssel) ist laut ADR-0001 die
  bekannte Voraussetzung für funktionierende Client-Auth unter Windows. **Update nach dem Live-Test**:
  die CLI nutzt `EEBUS.Net`s eigene, unbenannte Zertifikatserzeugung (wie Upstreams `ConsoleDemo`)
  und die TLS-Client-Authentifizierung funktionierte im Live-Test anstandslos — der in ADR-0001
  dokumentierte Schannel-Bug trat hier nicht auf. Möglich, dass er nur unter bestimmten Bedingungen
  (z. B. bestimmte .NET-/Windows-Patch-Stände oder PFX-Re-Import-Pfade) auftritt; `EmsAtHome.CertTool`
  bleibt als bekannter Workaround verfügbar, falls er doch einmal auftritt.

## `read` gibt jeden bekannten Wert aus, nicht nur Energie/Leistung/Limit (2026-09-02, gleiche Session)

Nutzerwunsch: "gib mir bei read 0 doch einfach mal JEDEN EINZELNEN Wert mit einer Beschreibung aus".
Vier weitere, bisher ungenutzte oder fehlende SPINE-Funktionen implementiert bzw. angebunden (Feldformen
aus den lokalen SPINE-XSDs `EEBus_SPINE_TS_DeviceClassification.xsd`/`_DeviceDiagnosis.xsd`/
`_ElectricalConnection.xsd`):

- `DeviceClassificationManufacturerData` (neu) - Hersteller-Info (Marke, Modell, Seriennummer,
  Firmware-/Hardware-Revision, Power Source). Einmalige Funktion (keine Liste).
- `DeviceDiagnosisStateData` (neu) - Betriebszustand (`operatingState`), letzter Fehlercode,
  Power-Supply-Zustand, Betriebsdauer.
- `ElectricalConnectionPermittedValueSetListData` (neu) - zulässige Wertebereiche/-listen pro
  `electricalConnectionId`/`parameterId` (z. B. min/max Ladestrom oder -leistung).
- `ElectricalConnectionDescriptionListData` (bereits vorhanden, aber ohne `ReadDataAsync`) - jetzt
  ergänzt: Versorgungsart, Phasenzahl, Energierichtung.

Alle vier laufen NICHT über den generischen Auto-Read-Mechanismus (`Connection.ReadAndSubscribe`) -
der liest nur Functions von Features, die über `useCase.Features` (also `GetFeatures()` einer
registrierten `UseCase`-Klasse) erreichbar sind. `DeviceClassification` gehört bei keinem der hier
relevanten Use Cases dazu (nur über die rohen Discovery-Daten sichtbar, dort aber nie mit einer
`UseCase` verknüpft) - der generische Read-Mechanismus hätte es nie automatisch abgefragt, egal welche
Functions auf dem Feature deklariert sind. Stattdessen: gezielter On-Demand-Read über das bereits
etablierte `DeviceData`/`ReadDataAsync`-Muster (wie bei `ElectricalConnectionCharacteristicListData`/
`DeviceConfigurationKeyValueListData`), ausgelöst durch den `read`-Befehl. Für `DeviceClassification`
war zusätzlich ein neues lokales `DeviceClassificationClientFeature` sowie dessen Eintrag in
`MonitoringOfPowerConsumption`s (`MonitoringAppliance`) `GetFeatures()` nötig, damit
`GetFeatureAddress("DeviceClassification", false)` überhaupt etwas findet.

`EebusDemo.cs` refaktoriert: `PrintMeasurementChannels`/`PrintBillEntries`/`PrintLoadControlLimits`/
`PrintElectricalConnectionCharacteristics`/`PrintDeviceConfigurationKeyValues` als wiederverwendbare
Methoden extrahiert (vorher inline in `PrintDiscoveryDetails`), so dass `read <index>` sowohl die
frisch gelesenen Werte (Measurement, LPC, Hersteller, Diagnosezustand, zulässige Wertebereiche,
ElectricalConnection-Beschreibung) als auch alles bereits aus `connect` bekannte (Measurement-Kanäle,
Bill-Einträge, LoadControl-Limits, ElectricalConnection-Charakteristiken, DeviceConfiguration-
Key-Values) zeilenweise ausgibt - nicht mehr nur die drei ursprünglichen Werte.

Build (0 Fehler) und Testsuite (5/5 grün) verifiziert. Noch nicht live gegen die echte Wallbox
getestet - der Rebuild ist durch die laufende Nutzer-CLI-Session blockiert (bekanntes Muster, siehe
oben).
