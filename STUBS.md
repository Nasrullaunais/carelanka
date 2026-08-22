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

Nothing yet — coding hasn't started. Add rows as they appear.

| # | What is faked | Where it lives | Standing in for | Owner of the real thing | Added |
| :-- | :--- | :--- | :--- | :--- | :--- |
| _(example)_ | Bed register returns 6 fake beds in 2 wards | `api/Services/Patient/Stubs/StubBedRegistryService.cs` | `GET /beds` — `equipment-spec.yaml` | **M3 Sethmin** | 2026-08-21 |

> Delete the example row when the first real one is added.

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
| all four | Agent workflow tables | **group** | Undecided — `integration_of_functions.md` §11.2 |

### One of these should not be stubbed for long

`GET /beds/{id}/occupancy` (M3 → M4) is the one place a stub is genuinely
dangerous rather than merely temporary. Equipment calls it before taking a bed
out of service, and a fake that answers "free" would let maintenance be scheduled
on an occupied bed. Stub it if you must, but make the fake answer **"occupied"**
so it fails safe, and replace it early.