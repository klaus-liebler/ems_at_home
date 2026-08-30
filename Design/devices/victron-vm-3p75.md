# Victron VM-3P75 (Energiezähler)

Status: Roadmap — noch keine Dokumentation gesammelt, kein Treiber implementiert.

Vermuteter Zugriff: Modbus TCP über einen Victron Cerbo/Venus GX (unbestätigt, evtl. auch
VE.Direct/VE.Can direkt). Sobald Protokolldokumentation vorliegt: über den Skill
`add-device-integration` als eigenes Projekt `src/EmsAtHome.Devices.VictronVm3p75/` anlegen,
implementiert vermutlich `IMeter` aus `EmsAtHome.Abstractions`.
