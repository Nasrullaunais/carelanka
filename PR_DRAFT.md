# PR draft — real bed counts

Scratch file, gitignored. Copy from here.

Branch: `feat/patient-real-bed-counts`, off `main`. Independent of PR #15 and the
patient CRUD PR — merge this one whenever.

---

## Commit message

```
feat: read real bed counts from Equipment's register

Retires STUBS.md row 1. Ward.total_beds came from a stub that reported
6 beds for every ward; it now comes from IBedService.CountBedsByWardAsync,
which landed on main in PR #13.

BedRegistryService is a delegating adapter rather than a direct call at
the use site, so this stays the one file Patient Management touches
Equipment's service through.

Also fixes the test suite, which is red on main today: 36 of 91 tests
fail on 429s because every test class shares one login budget of 20 a
minute. PermitLimit now reads RateLimits:AuthPerMinute, default 20, and
the test fixture sets 1000. Production behaviour is unchanged.

The ward test that asserted the stub's constant is replaced by two: one
for a ward with no beds (0, not a made-up number), one that registers
three real beds through POST /api/beds and reads the count back.

92 tests pass.
```

---

## PR title

`feat: read real bed counts from Equipment's register (retires STUBS.md row 1)`

## PR body

Off `main`, independent of my two open PRs. Merge whenever.

### What it does

`Ward.total_beds` was a lie — the stub reported 6 beds for every ward. Sethmin's register
landed in #13, so it now reports what Equipment actually has.

He made this deliberately small: `IBedService.CountBedsByWardAsync` was given the same
signature and the same *"a ward with no beds is absent from the result, not zero"* contract
as the port it replaces. So the swap is a delegating adapter plus one DI line, exactly as
his note in `Program.cs` predicted.

**`STUBS.md` row 1 is retired** and moved to Replaced, with a note on what ran on the fake
and what changed when it went.

### Heads up: `main` is red right now

Not caused by this branch. On `main` today:

```
dotnet test  →  36 failed, 55 passed, 91 total
```

They're 429s. Every test class shares one host, so the whole suite spends one budget of 20
logins a minute, and the equipment tests pushed it over. The failures read like broken
logins, which is why they're easy to misdiagnose.

Fixed here: `PermitLimit` reads `RateLimits:AuthPerMinute`, **default still 20**, and only
the test fixture raises it. `ProblemResponseTests` still asserts the 429 against its own
host at the default, so the limiter is still covered.

> This same fix is also on my `feat/patient-crud` branch, written byte-identically, so the
> two should merge without a conflict. If git does flag it, either side is correct.

### Rows 2 and 3 — yours, Sethmin, and one of them is ready

- **Row 2 (`StubWardDirectory`)** — retirable today. `IWardService` is real and merged, so
  it can become a delegating adapter the same way row 1 just did. I left it alone: your
  stub, your call.
- **Row 3 (`StubBedOccupancyPort`)** — **not** retirable yet, and please don't. Occupancy is
  the presence of a live `BedAssignment` row, and nothing writes those until step 6 of
  `docs/build/patient.md`. The fake answering "occupied" is the safe direction; flipping it
  to "free" would let maintenance be scheduled on a bed with a patient in it.

### Checks

- `dotnet test` — **92 passed, 0 failed** (main is 55/36 today).
- `npm run check:specs` — 5 specs valid, 0 duplicate routes, 0 duplicate `operationId`s,
  0 schema conflicts.
- No spec change, so no client regeneration needed.

---

# Still parked: fix 1, on `feat/patient-crud`

`git stash list` → `stash@{0}` is *"fix1: adopt main's PagedResult + empty-page test"*.

Two changes, waiting to go back on that branch once this one is committed:

- `api/DTOs/Common/PagedResult.cs` — replaced with main's copy, byte-identical, so the
  add/add conflict disappears. Equipment's version rounds an empty list up to one page;
  Patient Management adopted that rather than shipping a second, differing copy.
- One new test pinning it: an empty search is "page 1 of 1", not "page 1 of 0".

72 tests passed on that branch with it applied.
