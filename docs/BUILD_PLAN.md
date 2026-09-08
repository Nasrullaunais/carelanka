# CareLanka Build Plan

**Status:** Phase 0 settled 2026-09-07 — see `docs/ADR.md`. Five build tracks, ready to start.
**Covers:** everything between "the design documents are finished" and "five tracks running
without breaking each other".

---

## 0. How to use this file

**This is the index. The work is in `docs/build/`.**

It used to be one file with everything in it, on the argument that when your agent reads
your section it also sees the other three. That argument was right, and it is why §5 below
exists — the cross-track dependencies stayed here rather than moving into the per-track
files. What moved out is the detail, which nobody was reading in full anyway.

| Track | File | Owner |
| :--- | :--- | :--- |
| **0 · Common** | `docs/build/common.md` | Common (group-owned) |
| 1 · Emergency / Ambulance | `docs/build/emergency.md` | Kaveesha |
| 2 · Staff Management | `docs/build/staff.md` | Nasrullah |
| 3 · Health Equipment | `docs/build/equipment.md` | Sethmin |
| 4 · Patient Management | `docs/build/patient.md` | Lochana |

**If you are an AI assistant working for one member:**

1. **Read your member's track file in full.**
2. **Read `docs/build/common.md` §7** — four things about auth that change how you build.
   You do not need the rest of that file unless you own it.
3. **Read §5 of this file** — who is waiting on you, and who you are waiting on.
4. **Read `STUBS.md`**, specifically rows where **Owner** is your member. Those are fakes
   somebody else built because your part did not exist yet. Replacing them is your job.
5. **Do not do another member's work**, even if it is blocking you. Stub it (§1.3) and
   record it.

These files say *what order to build in*. They do not redefine ownership, schemas or
endpoints — `integration_of_functions.md`, `docs/entity_diagram.md` and the five
`specs/*.yaml` do that, and they win on any conflict.

---

## 1. Ground rules

### 1.1 One writer per table

Settled in `integration_of_functions.md` §2 and §3. If you need to change a table you do
not own, call the owner's service. If their service does not exist yet, stub it (§1.3).

### 1.2 Anything that is not a specific member's is common

And common is built **once**, by the common track — not four times. Auth, the JWT, the
`DbContext`, base entity classes, the exception handler, the audit interceptor, the agent
workflow tables, the Coordinator Agent, CI.

Before building something that is not in your component's row of
`integration_of_functions.md` §3, check whether it is common. Building a common thing twice
is worse than stubbing it, because both copies look right.

### 1.3 Build the deterministic version first. The agent goes last.

In every component the agent is the **last** thing built.

The reason is practical: an agent proposes changes to a domain that has to already work. If
bed assignment does not work when a human does it by hand, an agent proposing beds is
untestable — you cannot tell an agent bug from a domain bug.

```
tables → CRUD → the state machine (test hard) → the manual version of
the agent's job → then the agent → then the approval UI
```

### 1.4 Stub what you do not own — and write it down

```
Need it → stub it → add a row to STUBS.md → keep going
Own it  → read STUBS.md rows where Owner is you → build the real thing →
          delete the row in the same commit
```

Stub at the **service interface**, not in a controller, so swapping in the real one is a DI
registration change and nothing else moves. A stub nobody wrote down is a lie in the
codebase that looks like working code, passes your tests, and quietly returns invented data
until someone notices at the demo.

**One exception: do not stub auth.** It is being built first specifically so nobody has to.

### 1.5 Regenerate clients in the same commit as the backend change

A stale generated client makes every downstream type error a red herring, so
`check:codegen` runs **before** `typecheck` in CI.

### 1.6 Branch and PR, don't push to `main`

§13 grades feature branches, pull requests and reviews, and individual Git evidence is part
of the individual mark. **Each member commits their own work under their own account.**

---

## 2. Phase 0 — done

Route and name collisions, spec validity, and the four blocking group decisions are all
settled. Nothing here is a task.

```
5 specs · 171 operations
0 duplicate routes · 0 duplicate operationIds · 0 schema conflicts
```

- **The decisions and their reasoning:** `docs/ADR.md` — 7 accepted, 1 open (deployment).
- **What was renamed and why, so it is not reintroduced:** `integration_of_functions.md` §11.6.
- **The gate that keeps it clean:** `bun run check:specs`, in CI.

---

## 3. Build order

```
   ┌──────────────────────────────────────────────────────────┐
   │ TRACK 0 · COMMON — auth, JWT, [Authorize]                 │
   │ docs/build/common.md §1–§2                                │
   │ Nothing else can start. Every endpoint in every spec      │
   │ sits behind [Authorize].                                  │
   └───────────────────────────┬──────────────────────────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        ▼                      ▼                      ▼
   ┌─────────┐          ┌─────────────┐        ┌──────────────┐
   │ Ward    │          │ Bed register│        │ POST /staff/ │
   │ [M4]    │          │ [M3]        │        │ lookup [M2]  │
   │         │          │             │        │              │
   │ unblocks│          │ unblocks    │        │ unblocks     │
   │ M1 M2 M3│          │ M4's agent  │        │ M1 M3 M4     │
   └────┬────┘          └──────┬──────┘        └──────┬───────┘
        └──────────────────────┴──────────────────────┘
                               ▼
              Four tracks in parallel, everything else
```

Three tables gate everybody else. **`Ward` is the most-depended-on table in the system** —
all four components reference it, so it is frozen once agreed. **`Bed` is Patient
Management's hardest dependency.** **`POST /staff/lookup` is small and deletes a stub in
three people's code**, which is a better return than anything else M2 can build in an hour.

---

## 4. The five tracks

Each file has the full step list, the traps specific to that component, its auth notes and
its dependencies.

| Track | Owns | Contract | Steps |
| :--- | :--- | :--- | :--- |
| **[Common](build/common.md)** | `StaffMember`, `PatientAccount`, `RefreshToken`, `AgentWorkflow`, `AgentProposedChange`, `AuditLog` | `common-spec.yaml` (12) | Bootstrap → **auth** → exceptions → audit → workflows → CI |
| **[Emergency](build/emergency.md)** | `EmergencyCall`, `Ambulance`, `Dispatch`, `DispatchCrew`, `RouteLog` | `emergency-spec.yaml` (33) | 11 |
| **[Staff](build/staff.md)** | `Skill`, `StaffMemberSkill`, `Shift`, `Allocation`, `LeaveRequest`, `WardStaffingRule` | `staff-spec.yaml` (32) | 12 |
| **[Equipment](build/equipment.md)** | `EquipmentCategory`, `EquipmentItem`, `Bed`, `Pharmacy*`, `MaintenanceSchedule`, `Warning`, `ActionRequest` | `equipment-spec.yaml` (28) | 11 |
| **[Patient](build/patient.md)** | `Patient`, `Admission`, `Ward`, `BedAssignment`, `BedReservation`, `Discharge`, `Appointment` | `patient-spec.yaml` (33) | 12 |

---

## 5. Who is waiting on whom

**This table is why the tracks were not split into four separate worlds.** Read your
column, then read your row.

| Provider ↓ / Needs → | **M1 Emergency** | **M2 Staff** | **M3 Equipment** | **M4 Patient** |
| :--- | :--- | :--- | :--- | :--- |
| **Common** | auth, JWT, `/workflows` | auth, JWT, `/workflows` | auth, JWT, `/workflows` | auth, JWT, `/workflows` |
| **M1 Emergency** | — | — | — | `POST /emergency-calls`, dispatch notification |
| **M2 Staff** | `POST /staff/lookup` | — | `POST /staff/lookup` | `POST /staff/lookup`, `doctor` claim |
| **M3 Equipment** | — | — | — | **`GET /beds`** ← the big one |
| **M4 Patient** | `GET /capacity/wards`, `POST /admissions/pre-admit` | `GET /wards`, `GET /wards/{id}/occupancy` | **`GET /beds/{id}/occupancy`**, `GET /wards`, admission summary | — |

**The two in bold are the ones that matter most.**

`GET /beds` (M3 → M4) is the deepest dependency in the project — M4's bed agent has nothing
to reason over without it.

`GET /beds/{id}/occupancy` (M4 → M3) is the one place a stub is genuinely dangerous rather
than merely temporary. Equipment calls it before taking a bed out of service, and a fake
that answers "free" would let maintenance be scheduled on an occupied bed. **Stub it
answering "occupied"** so it fails safe, and replace it early.
---

## 8. Member 4 — Patient Management (Lochana)

**Owns:** `Patient`, `PatientAccount`, `Admission`, **`Ward`**, `BedAssignment`, `BedReservation`, `Discharge`, `DischargeChecklistItem`, `Appointment`, **`CareRecommendation`**
**Contract:** `specs/patient-spec.yaml` (40 paths) · **Design:** `specs/patient-management-plan.md`
**Boundaries:** `integration_of_functions.md` §4–§11
**Two agents, not one** — see step 13. Added on the lecturer's direction at topic finalization; §8.10 of the design doc has the full reasoning.

| # | Step | Notes |
| :-- | :--- | :--- |
| 1 | **`Ward` first** | Three other components reference it. Get it in early and then treat the schema as frozen |
| 2 | Remaining entities + configurations + migration | Including `PatientAccount` (Rev 2.5) and its optional link `Patient.UserAccountId` |
| 3 | Patient + Admission CRUD | Including `temp_reference` for unidentified arrivals |
| 4 | **The 7-state admission status machine** | `awaiting_bed → awaiting_approval → bed_reserved → admitted → ready_for_discharge → discharged`, plus `cancelled`. Illegal transitions → 409. **This is the backbone — test it hardest** |
| 5 | `GET /capacity/wards` + `GET /wards/{id}/occupancy` | M1 and M2 are both blocked on these — build them before the agent |
| 6 | **Manual bed assignment, no AI** | Pick a bed by hand, with the 30-minute hold and the partial unique index. The concurrency guarantee lives in the index, not in code |
| 7 | Discharge checklist + confirmation | `clinical_clearance` gated on the `Doctor` role claim |
| 8 | Codegen gate | |
| 9 | React: admissions dashboard, bed board, occupancy report | |
| 10 | Flutter: nurse screens, then patient's own-stay screens | Local notifications on status change — the device feature |
| 11 | **The bed agent** | Hard rules H1–H5 in deterministic C#, soft rules rank. Re-check every hard rule under a row lock at approval time |
| 12 | React: bed approval + downgrade approval | The two human gates |
| 13 | `CareRecommendation` entity + configuration + migration | Independent of the bed workflow — no dependency on steps 1–12 beyond `Patient` and `Admission` existing |
| 14 | **The care advisory agent, last** | Deterministic red-flag keyword screen (§8.13) runs *before* the model, not after. Rules CR1–CR4 (§8.15) validated the same way H1–H5 are |
| 15 | React: Doctor's care recommendation queue, approve/reject | The third human gate in this component |
| 16 | Flutter: "ask about a symptom" + "my care recommendations" | Patient-facing; never renders `agent_message` or `rejection_reason` |

**You will need to stub:** Equipment's bed register (**your hardest dependency**); Staff's `POST /staff/lookup`; Emergency's dispatch notification.

**Others are waiting on you for:** `Ward` (all three), `GET /capacity/wards` (M1), ward occupancy (M2), `GetBedOccupancyAsync` (M3), `POST /admissions/pre-admit` (M1).

**Steps 13–16 are self-contained.** Nothing else in the group depends on the care advisory agent, and it depends on nothing outside this component beyond the `Doctor` role claim (already shared, via the bootstrap JWT). It can be built in parallel with the bed-agent track, or after — it does not gate anyone and nobody gates it.

---

## 6. Integration checkpoints

Four moments where the parts actually have to meet. Worth doing deliberately, together,
rather than discovering at the demo.

| # | Checkpoint | Who | What proves it |
| :-- | :--- | :--- | :--- |
| 0 | **Auth works for everyone** | Common → all | Each member logs in as a seeded account of their own role and calls one of their own protected endpoints |
| 1 | **First real cross-component read** | M3 → M4 | Equipment's bed register replaces M4's stub. Delete the `STUBS.md` row |
| 2 | **Staff lookup replaces three stubs** | M2 → M1, M3, M4 | "Approved by Dr. Perera" renders from real data in all three |
| 3 | **The pre-admission call** | M1 → M4 | A dispatch creates an `Admission` in `awaiting_bed`. Watch the urgency translation — `critical/high/medium/low` becomes `routine/urgent/emergency`, and a mismatch is a 400 |
| 4 | **The full emergency workflow** | all five | `CareLanka_Component_Plan.md` §6, end to end: call in Flutter → dispatch → pre-admission → bed proposal → staffing check → equipment check → one human approval → back to Flutter. **This is the assessed cross-platform workflow** (§9.1, §10) |

---

## 7. Still needs a person

| # | Thing | Owner | State |
| :-- | :--- | :--- | :--- |
| 1 | ADR | Common | **Done** — 7 of 8 accepted |
| 2 | Auth + workflow contract | Common | **Done** — `specs/common-spec.yaml` |
| 3 | Spec gate | Common | **Done** — `bun run check:specs` |
| 4 | Backend bootstrap + auth | Common | **Implemented in PR #11, verified 2026-09-08** — EF Core + PostgreSQL, base entities, `Common_AddIdentity`, login/registration/refresh/logout/`/auth/me`, policies, exception handler, `/health`. Setup: `api/README.md` |
| 4b | Audit interceptor + `AgentWorkflow` tables | Common | Not built. Nothing else is waiting on them — the three other tracks are unblocked by #4 |
| 5 | CI — `.github/` | Common | Not built. §13 grades it |
| 6 | Auth integration + generated-contract test project | Common | **Done in PR #11** — `CareLanka.Api.Tests`, 15 tests against disposable PostgreSQL |
| 7 | `web-ui/` scaffold | Common | Not built. Blocks all React work |
| 8 | `flutter create .` | **whoever has the SDK** | Not run. No `android/`, no APK without it |
| 9 | `swagger_parser` in `pubspec.yaml` | with #8 | Not added |
| 10 | `*.g.dart` — committed or CI-built? | with #8 | Open. `.gitignore` currently ignores it |
| 11 | **Deployment target** | **group** | Open — ADR 8, the last unmade decision |
| 12 | Equipment entities in `entity_diagram.md` | Sethmin | Stale — Open Decision 12. Graded artefact |
| 13 | `staff-management-plan.md` | Nasrullah | Does not exist; the other three components have one |

---

## 8. If something goes wrong

- **A conflict about who owns something** → `integration_of_functions.md` §11 Open Items,
  with your reasoning. Do not fix it in someone else's file.
- **A schema disagreement** → where the entity diagram and a member's own committed spec
  disagree, **the spec wins**, for whoever owns that entity.
- **Blocked on someone's unbuilt work** → stub it, record it, keep going (§1.4). Do not
  wait, and do not build it for them.
- **You broke something shared** — the app will not start, CI is red on `main` — say so
  immediately. A shared break blocks four other people, and the cost of mentioning it is
  far lower than four people debugging the same thing separately.
