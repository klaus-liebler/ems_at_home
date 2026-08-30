# Victron Multiplus (Batterieladegerät)

Status: Roadmap — noch keine Dokumentation gesammelt, kein Treiber implementiert.

Vermuteter Zugriff: VE.Bus, wahrscheinlich nur über die Modbus-TCP/D-Bus-Bridge eines Cerbo/Venus GX
erreichbar (unbestätigt). Sobald Protokolldokumentation vorliegt: über den Skill
`add-device-integration` als eigenes Projekt `src/EmsAtHome.Devices.VictronMultiplus/` anlegen,
implementiert vermutlich `IBattery` aus `EmsAtHome.Abstractions`.
