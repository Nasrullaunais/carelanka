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

**Two open, both in the same direction.** Common auth was never stubbed: it was built
and merged in PR #11. Rows 2 and 3 are Equipment waiting on Patient Management. **Row 1
is gone** — Patient Management now reads Equipment's real bed register; see Replaced below.

| # | What is faked | Where it lives | Standing in for | Owner of the real thing | Added |
| :-- | :--- | :--- | :--- | :--- | :--- |
| 2 | Ward names on a bed — every ward is called `Stub ward <id fragment>` | `api/Services/Equipment/Stubs/StubWardDirectory.cs` | `GET /wards` — `patient-spec.yaml` | **M4 Lochana** | 2026-09-09 |
| 3 | Is this bed occupied — always answers **yes** | `api/Services/Equipment/Stubs/StubBedOccupancyPort.cs` | `GET /beds/{id}/occupancy` — `patient-spec.yaml` | **M4 Lochana** | 2026-09-09 |

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

**Replacing either is one line each** — the two `AddSingleton` registrations in
`api/Program.cs`. Nothing else moves.

**Row 2 is retirable today; row 3 is not.** `IWardService` is real and merged, so
`StubWardDirectory` can become a delegating adapter whenever M3 wants it — exactly what
row 1 just did in the other direction. Row 3 cannot follow yet: occupancy is the presence
of a live `BedAssignment` row, and nothing writes those until step 6 of
`docs/build/patient.md`. Until then the fake keeps answering **occupied**, which is the
safe direction.

**Not a stub, but the same dependency — `AdmissionSummary.ward_name` and `bed_number`
are `null` today**, and `BedAssignment.ward_name` / `bed_number` are empty strings. All four
are published by `patient-spec.yaml`. Nothing can hold a bed until step 6, so there is no case
yet where those are the wrong answer and the mapper that would fill them never runs; they
become wrong the moment `BedAssignment` rows exist. No row above, because nothing invented is
being returned — the fields are honestly empty rather than plausibly wrong.

**Not stubs, but the same shape of gap, recorded here so nobody hunts for them.** Three
things `patient-spec.yaml` publishes are deliberately **not served** by the API today, and
each says so in the spec:

| What | Why | Arrives with |
| :--- | :--- | :--- |
| `AdmissionDetail.workflows` | `AgentWorkflow` is common and unbuilt (ADR 3) | Step 11 |
| `AdmissionDetail.discharge` | No discharge row is written yet | Step 7 |
| `wardId` filter on `GET /admissions` | A ward is reached through a live `BedAssignment` | Step 6 |
| **Nurses scoped to their own ward** on `GET /admissions` and `GET /admissions/{id}` | Blocked twice: a nurse's ward is Staff Management's data (**M2**, unbuilt) and an admission's ward needs a live `BedAssignment` | Step 6, and M2 |

The key is **omitted, not returned empty, and the parameter is unpublished rather than
accepted and ignored.** A missing key is visible to whoever generates a client; a key that
is always `[]` and a filter that silently does nothing are not.

**The last row is the one that matters, because it is a permission and not a convenience.**
`patient-spec.yaml` says "Nurses are scoped to their own ward"; today every ward nurse sees
every admission in the hospital. That is wider than the contract promises, and §16.1 of the
assignment grades access control. It is written here rather than left silently missing so it
is not discovered at the demo. Both halves have to exist first: **M2** has to publish which
ward a nurse works in, and a live `BedAssignment` has to say which ward an admission is in.

---

## Replaced

Move rows here when the real thing lands, with the commit that did it. Kept
rather than deleted, so "how long did we run on a fake, and what did we test
against" is answerable later.

| # | What it was | Replaced by | Commit | Date |
| :-- | :--- | :--- | :--- | :--- |
| 1 | Bed counts per ward — every ward reported exactly 6 beds | `api/Services/Patient/BedRegistryService.cs`, a delegating adapter over `IBedService.CountBedsByWardAsync` | `feat/patient-real-bed-counts` | 2026-09-10 |

**What ran on the fake, and what changed when it went.** Row 1 was live from 2026-09-08 to
2026-09-10 and fed exactly one field: `Ward.total_beds` on `GET /wards` and `POST /wards`.
Nothing reasoned over it — no rule, no agent, no screen branched on the number — so the
only visible change is that the number is now true. A ward with no beds reports **0** where
it used to report 6, which is why the ward test asserting the constant was replaced by two:
one for the empty ward, one that registers three real beds through `POST /api/beds` and
reads the count back.

Sethmin made this a small change on purpose: `IBedService.CountBedsByWardAsync` was given
the same signature and the same "a ward with no beds is absent from the result, not zero"
contract as the port it was replacing, so the swap is an adapter and a DI line.

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
| ~~M2, M3~~ | ~~Ward list — id, name, type~~ | ~~**M4**~~ | **BUILT 2026-09-09 — not a stub any more.** `GET /api/wards` is live and every staff role may read it; `POST /api/wards` is admin-only. Shape: `specs/patient-spec.yaml`. Filters: `?wardType=` and `?isActive=` (defaults true) |
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
