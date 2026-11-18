# Origin

Vendored from [kermigmbh/EEBUS.Net](https://github.com/kermigmbh/EEBUS.Net), commit
`24d9cd931102ca1ce64c6ebafafd4a625b4b9f2d` (NuGet version 1.0.30, MIT license, see `LICENSE.txt`).

Only the `EEBUS.Net` library project was vendored - upstream also ships `ConsoleDemo`,
`EEBUS.vue` and `TestProject1`, not copied here. `src/EmsAtHome.EebusCli` is our own console
app based on upstream's `ConsoleDemo`.

This is a deliberate fresh start from unmodified upstream (2026-09-02) - previous sessions had
forked and patched an out-of-repo copy (client/server double-connection arbitration, CNG key
handling, etc., see `Design/decisions/0001-eebus-net-library-choice.md`); none of those patches
are carried forward here. See `Design/decisions/0003-eebus-net-fork-and-cli-restart.md` for why.
