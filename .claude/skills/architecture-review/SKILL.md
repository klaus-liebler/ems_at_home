---
name: architecture-review
description: Review whether ems_at_home's current architecture still fits reality and log the verdict under Design/reviews/.
---

Run this review whenever invoked (periodically, or before a significant new change):

1. Read `Design/00-overview.md` and every file under `Design/decisions/` to recall the currently
   stated architecture and its rationale.
2. Enumerate the actual `src/*` projects and their real responsibilities via Glob/Grep — not from
   memory or from what the ADRs claim. Diff that against what the ADRs describe.
3. Explicitly check:
   - Has any device integration (see `Design/devices/`) needed something the current
     `IEnergyDevice`/`IMeter`/`ICharger`/`IBattery` abstractions in `EmsAtHome.Abstractions` don't
     support?
   - Has the EEBus/OSCEV work in `EmsAtHome.Eebus` grown beyond what fits cleanly there, or beyond
     what a thin wrapper around `EEBUS.Net` should own?
   - Does the SQLite/EF Core boundary (once it exists) still match real usage, or has it been
     bent to fit something it wasn't designed for?
   - Is anything duplicated across device projects that should move into `Abstractions`?
4. Render a concrete verdict — one of: "kein Refactoring nötig" / "kleinere Aufräumarbeiten
   empfohlen" / "größeres Refactoring überfällig" — with specific reasoning tied to what you found
   in step 2/3, not vague sentiment.
5. Write a new file `Design/reviews/YYYY-MM-DD-architecture-review.md` (today's date) with the
   verdict and reasoning. Never overwrite or delete a prior review — the review history itself is
   a signal of how the architecture has evolved.
6. If the verdict is "größeres Refactoring überfällig": propose the refactor as a plan (use
   `EnterPlanMode` if available) and stop there — do not execute it without the user's approval.
