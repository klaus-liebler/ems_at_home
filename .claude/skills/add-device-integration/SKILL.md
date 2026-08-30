---
name: add-device-integration
description: Scaffold a new physical device integration project (inverter/meter/charger/battery) under src/EmsAtHome.Devices.<Name>/.
---

Invoked with a device name (e.g. "Huawei SUN2000"). Steps:

1. If not already given, ask the user:
   - Which `EmsAtHome.Abstractions` interface(s) the device implements (`IMeter`, `ICharger`,
     `IBattery`, or a combination).
   - Its wire protocol (Modbus TCP/RTU, VE.Direct/VE.Can, BLE, REST, ...) and whether any
     protocol documentation (PDF, register list, manual) is already available.
2. Create `src/EmsAtHome.Devices.<Name>/EmsAtHome.Devices.<Name>.csproj` — `net10.0`,
   `Nullable`/`ImplicitUsings` enabled, `ProjectReference` to `EmsAtHome.Abstractions` — and a
   matching `test/EmsAtHome.Devices.<Name>.Tests/` xUnit project (mirror the shape of
   `test/EmsAtHome.Eebus.Tests`). Add both to `EmsAtHome.slnx`.
3. Create `Design/devices/<name>.md` from the existing stubs in that folder as a template:
   protocol, source of protocol documentation (or "unbestätigt" if none yet), known unknowns, and
   a link back to the `EmsAtHome.Abstractions` interface member(s) it implements.
4. Remind the user/yourself explicitly: per the repo's governance rule, this device's Design doc
   (and any ADR updates) must be staged in the *same* commit as the code — the commit-gating hook
   is a backstop, not a substitute for writing the doc well.
5. Do **not** invent register values, commands, or protocol framing. If no documentation exists
   yet (true for all four originally-planned devices as of Milestone 1), scaffold the project
   structure only and stub the driver with `throw new NotImplementedException()` — implement for
   real only once real documentation is available.
