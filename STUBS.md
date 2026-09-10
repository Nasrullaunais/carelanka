# STUBS — fakes standing in for someone else's unbuilt work

**Every stub in this repository has a row in this file. No exceptions.**

Four people are building four components that depend on each other, and nobody
can wait for everybody else. So when you need something a teammate hasn't built,
you fake it and keep going — that part is expected and fine.

What is *not* fine is a fake nobody wrote down. It looks exactly like working
code, it passes your tests, and it quietly returns invented data until someone
notices at the demo. This file is the difference.

---

## How this works

**If you are an AI assistant: read this file at the start of every session, and
check the rows where `Owner` is your member.**

| When | Do this |
| :--- | :--- |
| **You need something you don't own** | Stub it, add a row here, commit both together |
| **You start work on your component** | Read the rows where **Owner is you**. Somebody is already depending on those |
| **You build the real thing** | Delete the row, in the same commit that replaces the stub |
| **You are unsure whether something is a stub** | Search the code for `// STUB` — every stub carries that marker and a pointer back here |

**Update this file. Never `CLAUDE.md`.** `CLAUDE.md` loads on every turn of every
session and is for standing conventions; this file changes constantly and is for
current state. Putting stub notes in `CLAUDE.md` makes it grow without limit and
makes the stubs harder to find, not easier.

### Stub at the interface, not in the controller

Write the interface the real thing will implement, and give it a fake
implementation. Swapping in the real one is then a one-line DI change and nothing
else in your code moves.

```csharp
// api/Services/Patient/Stubs/StubBedRegistryService.cs
// STUB — standing in for Equipment (M3). See STUBS.md row 1.
// Replace with the real IBedRegistryService when GET /beds exists.
public class StubBedRegistryService : IBedRegistryService
{
    public Task<IReadOnlyList<Bed>> GetBedsAsync(Guid wardId) => ...
}
```

Three rules for the fake itself:

1. **Mark it.** `// STUB` in the file, and the row number here.
2. **Make it obviously fake in the data**, not in the shape — return
   `"Ward A, Bed 1"`, not realistic-looking invented patient names that could be
   mistaken for real seeded data.
3. **Match the published contract exactly.** The stub returns what the owner's
   `*-spec.yaml` says it will. If you invent a different shape, swapping in the
   real service breaks your code and you will blame their code.

### Do not stub your way around a disagreement

If you think a teammate's contract is wrong, a stub of *your preferred version*
is not the answer — you will have built against something that never arrives.
Raise it in `integration_of_functions.md` §11 Open Items and stub what they
actually published.

---

## Open stubs

**Three open, one in each direction.** Common auth was never stubbed: it was built
and merged in PR #11. Rows 2 and 3 are Equipment waiting on Patient Management; row 1
is Patient Management waiting on Equipment.

| # | What is faked | Where it lives | Standing in for | Owner of the real thing | Added |
| :-- | :--- | :--- | :--- | :--- | :--- |
| 1 | Bed counts per ward — every ward reports exactly 6 beds | `api/Services/Patient/Stubs/StubBedRegistryService.cs` | `GET /beds` — `equipment-spec.yaml` | **M3 Sethmin** | 2026-09-08 |
| 2 | Ward names on a bed — every ward is called `Stub ward <id fragment>` | `api/Services/Equipment/Stubs/StubWardDirectory.cs` | `GET /wards` — `patient-spec.yaml` | **M4 Lochana** | 2026-09-09 |
| 3 | Is this bed occupied — always answers **yes** | `api/Services/Equipment/Stubs/StubBedOccupancyPort.cs` | `GET /beds/{id}/occupancy` — `patient-spec.yaml` | **M4 Lochana** | 2026-09-09 |

**Row 1 — what it feeds and how far it goes.** Only `Ward.total_beds` on `GET /wards` and
`POST /wards` reads it today. `IBedRegistryService` is deliberately one method wide
(`CountBedsByWardAsync`) because counting is all the ward endpoints need; the bed agent's
candidate list widens the interface later, and that is when the fake starts mattering.

**Why a constant and not zero.** Six beds in every ward is visibly not a real hospital.
Zero would have been indistinguishable from the genuine "no beds recorded in this ward yet"
state, which is exactly the kind of fake that survives to a demo.

**Row 2 — why the name looks broken on purpose.** `Bed.ward_name` is Patient Management's
to answer, and a plausible invented name like "Intensive Care" would be indistinguishable
from a real one on screen and would still be there at the demo. `ward_id` is real
throughout; only the display name is faked, so nothing downstream is reasoning over it.

**Row 3 — this is the dangerous one, and it fails safe.** `PATCH /beds/{id}` moving a bed
to `out_of_service`, and `POST /beds/{id}/retire`, both ask this before writing anything.
The fake answers **occupied**, so both are refused with `cl_equ_003` until the real
endpoint lands. That is deliberate: a stub answering "free" would let maintenance be
scheduled on a bed with a patient in it and would pass every test we wrote. The cost is
that withdrawing a bed cannot be exercised end to end yet, which is visible immediately
rather than at the demo.

**Replacing any of them is one line each** — the three `AddSingleton` registrations in
`api/Program.cs`. Nothing else moves.

**All three are now retirable, and none has been retired yet.** Before this merge each
side was blocked on code that did not exist on `main`. Both sides are now on this branch,
so row 1 can become a delegating adapter over `IBedService.CountBedsByWardAsync`, which
was given deliberately the same signature and the same "a ward with no beds is absent,
not zero" shape as `IBedRegistryService.CountBedsByWardAsync`. Rows 2 and 3 can point at
the real ward and occupancy work in the same way. Swapping any of them changes behaviour,
so each belongs in its own commit with its own tests rather than in a merge.

---

## Replaced

Move rows here when the real thing lands, with the commit that did it. Kept
rather than deleted, so "how long did we run on a fake, and what did we test
against" is answerable later.

| # | What it was | Replaced by | Commit | Date |
| :-- | :--- | :--- | :--- | :--- |
| — | — | — | — | — |

---

## Cross-component dependencies that will probably need stubbing

Not stubs yet — this is the predictable list, taken from the "what each component
needs from others" sections of `integration_of_functions.md` (§10, §16, §21,
§26). Useful for knowing what is coming.

| Needed by | What | From | Contract |
| :--- | :--- | :--- | :--- |
| M4 Patient | Bed register — id, ward, number, condition, isolation, distance | **M3** | `GET /beds`. Patient's **hardest dependency** — the bed agent has nothing to reason over without it |
| M1, M3, M4 | Staff name and role by ID | **M2** | `POST /staff/lookup`. Needed by three people to render "Approved by …" — small, high value, worth building early |
| M1 Emergency | Free bed counts per ward | **M4** | `GET /capacity/wards` |
| M1 Emergency | Create a pre-admission from a dispatch | **M4** | `POST /admissions/pre-admit` |
| M1 Emergency | Maps / routing | *third party* | Not a teammate, but stub it anyway so you can develop offline and test the provider-down path |
| M2 Staff | Ward occupancy and care mix | **M4** | `GET /wards/{id}/occupancy` |
| M2, M3 | Ward list — id, name, type | **M4** | `GET /wards`. Build early; three components reference `Ward` |
| M3 Equipment | Is this bed occupied or held? | **M4** | `GET /beds/{id}/occupancy` — **must** be real before Equipment can service any bed. Maintenance never evicts a patient, and a stub that always answers "free" would let it |
| M3 Equipment | Admission summary by ID | **M4** | For displaying who an assigned item belongs to |
| ~~all four~~ | ~~Agent workflow tables~~ | ~~group~~ | **Not a stub — DECIDED 2026-09-07.** Common, built once. Contract: `specs/common-spec.yaml` (`GET /workflows`, `GET /workflows/{workflowId}`, the approve/reject/revise gate). Reasoning: `docs/ADR.md` ADR 3 |
| ~~all four~~ | ~~Login + a JWT with your role claim~~ | ~~**Common (group-owned)**~~ | **BUILT 2026-09-07 — not a stub, and never was one.** `POST /auth/login`, `POST /auth/patient/register`, `POST /auth/patient/login`, `POST /auth/refresh`, `POST /auth/logout`, `GET /auth/me`. Write `[Authorize(Policy = Policies.X)]` against the real thing. Setup and test accounts: `api/README.md` |

### One of these should not be stubbed for long

`GET /beds/{id}/occupancy` (M3 → M4) is the one place a stub is genuinely
dangerous rather than merely temporary. Equipment calls it before taking a bed
out of service, and a fake that answers "free" would let maintenance be scheduled
on an occupied bed. Stub it if you must, but make the fake answer **"occupied"**
so it fails safe, and replace it early.