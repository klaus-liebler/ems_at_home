# JKBMS (Batteriemanagement)

Status: Roadmap — noch keine Dokumentation gesammelt, kein Treiber implementiert.

Vermuteter Zugriff: RS485/UART oder Bluetooth LE, proprietäres Framing (unbestätigt, Modell z.B.
JK-PB2A16S15P / "JIKONG V19"). Sobald Protokolldokumentation vorliegt: über den Skill
`add-device-integration` als eigenes Projekt `src/EmsAtHome.Devices.Jkbms/` anlegen, implementiert
vermutlich `IBattery` aus `EmsAtHome.Abstractions`.
