# CareLanka Build Plan

**Status:** proposed — needs the group to agree before Phase 0 starts
**Covers:** everything between "the design documents are finished" and "four people are building in parallel without breaking each other"

---

## 0. How to use this file

**This file is written to be read by an AI coding assistant.** All four of us use
Claude Code, and none of us is going to re-read a long plan every session — our
agents will. So there is **one file with all four parts in it**, not four files.
When your agent reads your section it also sees the other three, which is the
whole point: you find out that somebody else is already depending on your work
*before* you change it.

If you are an AI assistant working for one member:

1. **Read that member's section in full**, and skim the other three.
2. **Read `STUBS.md`** — specifically the rows where **Owner** is your member.
   Those are fakes somebody else built because your part didn't exist yet, and
   replacing them is your job.
3. **Read the documents this file points at.** It deliberately does not restate
   them. `CLAUDE.md` has the table of which document answers what.
4. **Do not do another member's work**, even if it is blocking you. Stub it
   (§1.3) and record it.

This file says *what order to build in*. It does not redefine ownership,
schemas, or endpoints — `integration_of_functions.md`, `docs/entity_diagram.md`
and the four `specs/*-spec.yaml` already do that and they win on any conflict.

---

## 1. Ground rules

### 1.1 One writer per table

Already settled in `integration_of_functions.md` §2 and §3. Not restated here.
The short version: if you need to change a table you don't own, call the owner's
service. If the owner's service doesn't exist yet, stub it (§1.3).

### 1.2 Build the deterministic version first. The agent goes last.

Every component has an AI agent, and in every component the agent is the **last**
thing built, not the first.

The reason is practical, not stylistic: an agent proposes changes to a domain
that has to already work. If bed assignment doesn't work when a human does it by
hand, an agent proposing beds is untestable — you cannot tell an agent bug from a
domain bug. Build the manual path, prove it, then put the agent on top of it.

This is the order that actually worked for Patient Management:

```
tables → CRUD → the state machine (test hard) → the manual version of
the agent's job → then the agent → then the approval UI
```

### 1.3 Stub anything you don't own — and write it down

You will constantly need something a teammate hasn't built. Do not wait, and do
not build it for them. Fake it so your part is testable, then **record it in
`STUBS.md` in the same commit.**

That last part is the bit that matters. A stub nobody wrote down is a lie in the
codebase that looks like working code. `STUBS.md` is how the owner's agent finds
out there is something waiting for them.

```
Need it → stub it → add a row to STUBS.md → keep going
Own it  → read STUBS.md rows where Owner is you → build the real thing →
          delete the row in the same commit
```

Stub at the **service interface**, not in a controller — write
`IBedRegistryService` with a fake implementation, so swapping in the real one
later is a DI registration change and nothing else moves.

### 1.4 Regenerate clients in the same commit as the backend change

`CLAUDE.md` covers this in full. The one thing worth repeating: a stale generated
client makes every downstream type error a red herring, so `check:codegen` runs
before `typecheck` in CI.

### 1.5 Branch and PR, don't push to `main`

Assignment §13 grades feature branches, pull requests and reviews. Individual Git
evidence is also part of the individual mark, so **each member commits their own
work under their own account.**

---

## 2. Phase 0 — before anyone writes code

These are cheap now and expensive later. Two of them will physically stop the
application from starting once controllers exist.

### 2.1 Route and name collisions — **these become startup crashes**

The four specs describe **one** ASP.NET application. Two controllers mapping the
same route throw `AmbiguousMatchException` at startup, which means the app won't
boot for anybody, not just the person who caused it. Live list is
`integration_of_functions.md` §11.6.

| # | Problem | Owner | Fix |
| :-- | :--- | :--- | :--- |
| 1 | `GET /reports/agent-performance` in **patient**, **equipment**, **staff** | all three | Namespace it: `/reports/patient/...`, `/reports/equipment/...`, `/reports/staff/...`. Emergency already did this |
| 2 | `operationId: getAgentPerformanceReport` in **patient**, **staff** | both | Follows the rename above |
| 3 | Schema `AgentPerformanceReport` — one name, three different shapes | patient, equipment, staff | Prefix each: `PatientAgentPerformanceReport`, etc. Emergency already did this |
| 4 | `GET /workflows/{workflowId}` in **equipment**, **patient** | group | Blocked on §2.3 — one workflow endpoint, not four |
| 5 | `Bed`, `WorkflowSummary`, `WorkflowAccepted` — shared names | equipment + patient | Either make them byte-identical (allowed) or rename. `Bed` is reportedly already identical — **verify before renaming anything** |

### 2.2 `staff-spec.yaml` is not valid OpenAPI

A stray `'leave type': None` in the `LeaveReport` schema. **This blocks client
generation for both frontends**, not just Staff's — the generators read the whole
document. Small fix, but it is on the critical path for all four of us.

Also in the same file: `PagedResult` uses `total_count` where the other three use
`total_items`. Two-vs-one, and the odd one out generates a differently-shaped
type. **Owner: Member 2.**

### 2.3 Group decisions that change code if settled late

| Decision | Why it can't wait | Where it's written up |
| :--- | :--- | :--- |
| **Enum storage** — native PG enum vs `int` vs string conversion | Affects **every migration**. Deciding after the migrations exist means rewriting all of them | `entity_diagram.md` Open Decision 3, which already recommends `HasConversion<string>()` + a CHECK |
| **Facade layer, or not** | `CLAUDE.md` is blunt: a facade that exists in three components and not the fourth is worse than neither. Decides where transactions and throwing live | `CLAUDE.md` → Backend architecture |
| **Who owns the agent-workflow tables** | All four agents write workflow state; four separate schemas make the one required cross-component trace a four-way join. Also unblocks §2.1 row 4 | `integration_of_functions.md` §11.2 |
| **Flutter state management** | All four of us must use the same one; switching after screens exist is painful | `provider` is already in `pubspec.yaml` — this needs **confirming**, not deciding |

Record the reasoning in the ADR (§9) as each one lands. The assignment asks for
an ADR and these are exactly the decisions it wants in it.

---

## 3. The shared bootstrap — one person, first

There is a chunk of work that **is not any component's domain** and that all four
of us are equally blocked on. Nobody can write a single `[Authorize]` endpoint
until it exists.

**One person builds it, before the four tracks start.** It costs nobody their
individual marks — `integration_of_functions.md` §3 already lists this as
"Group — shared plumbing, built once, whoever takes it on."

**Owner: _to be agreed_ — this is the first thing the group needs to assign.**

What is in it:

| Piece | Notes |
| :--- | :--- |
| `Program.cs` wiring | DI, EF Core + Npgsql, Swagger, auth, CORS |
| `CareLankaDbContext` | Empty of entities — see §3.1 |
| Base entity classes | `Entity` → `AuditedEntity` → `SoftDeletableEntity`, exactly as `entity_diagram.md` defines them |
| `StaffMember` + `RefreshToken` + login + JWT issuing | Every other endpoint depends on the role claim |
| Central exception handling | One `IExceptionHandler`, `ProblemDetails`, the `ApiException` hierarchy and `MessageCode` enum — all specified in `CLAUDE.md` |
| Audit interceptor | `ISaveChangesInterceptor` writing `AuditLog`, staff id from the JWT |
| `appsettings` + connection string | Plus a documented local Postgres setup so four machines can all run it |
| Global conventions | `JsonNamingPolicy.SnakeCaseLower`, `<Nullable>enable</Nullable>`, `DateTimeOffset` UTC |

### 3.1 How four people share one DbContext without merge hell

**Nobody edits a central `OnModelCreating`.** If four people add entity
configuration to one method, every single one of us gets a merge conflict on
every migration.

Instead, each member writes their own configuration classes:

```
api/Data/Configurations/Emergency/EmergencyCallConfiguration.cs
api/Data/Configurations/Patient/AdmissionConfiguration.cs
...
```

and the DbContext picks them all up in one line:

```csharp
protected override void OnModelCreating(ModelBuilder b)
    => b.ApplyConfigurationsFromAssembly(typeof(CareLankaDbContext).Assembly);
```

Each member owns their own files. Two people adding entities on the same day
touch zero shared lines. `DbSet<T>` properties are the one shared surface — keep
them grouped by component with a comment header, and add yours at the end of your
group.

### 3.2 Migration convention

Migrations conflict badly, because each one records a snapshot of the whole
model. Two people generating migrations from the same snapshot produces a mess
that is genuinely hard to unpick.

- **Name them `{Component}_{What}`** — `Patient_AddAdmission`, `Equipment_AddBed`
- **Pull `main` immediately before generating one**, and push it promptly after
- If you get a snapshot conflict: delete your migration, pull, regenerate. Do not
  hand-merge the snapshot file

---

## 4. Build order across the team

Three tables are depended on by everybody, and they gate everyone else's
progress. Build these **first**, before the four tracks run properly in parallel:

```
1. Bootstrap + StaffMember + JWT      (§3)          unblocks: every [Authorize] endpoint
        │
        ├─► 2. Ward            [Member 4, Patient]  unblocks: Shift.WardId (M2),
        │                                            EquipmentItem.WardId (M3),
        │                                            Dispatch.DestinationWardId (M1)
        │
        └─► 3. Bed register    [Member 3, Equipment] unblocks: Patient's bed agent (M4)
                │
                └─► 4. Everything else, four tracks in parallel
```

**`Ward` is the most-depended-on table in the system** — all four components
reference it. `CareLanka_Component_Plan.md` §5 already says to treat its schema as
frozen once agreed, because changing it breaks three other people.

**`Bed` is Patient Management's hardest dependency** (`integration_of_functions.md`
§10). Until it exists, M4 works against a stub — which is fine, but the sooner the
real one lands the sooner M4's agent is testable for real.

Everything below §5–§8 assumes steps 1–3 are done or stubbed.

---

## 5. Member 1 — Emergency / Ambulance (Kaveesha)

**Owns:** `EmergencyCall`, `Ambulance`, `Dispatch`, `DispatchCrew`, `RouteLog`
**Contract:** `specs/emergency-spec.yaml` (33 paths) · **Design:** `specs/emergency-management-plan.md`
**Boundaries:** `integration_of_functions.md` §22–§26

| # | Step | Notes |
| :-- | :--- | :--- |
| 1 | Entities + configurations + migration | Five entities. Mind the partial unique index `UNIQUE(ambulance_id) WHERE status IN ('assigned','en_route')` — it is the real guarantee against double-booking, not an application check |
| 2 | Ambulance CRUD end to end | Simplest entity. Proves the stack: service → controller → Swagger → one test |
| 3 | Call intake + the three status enums | `CallStatus`, `DispatchStatus`, `AmbulanceStatus`. Write them in one transaction so they can't disagree |
| 4 | **Manual dispatch, no AI** | Assign an ambulance to a call by hand. This is the thing the agent will later propose — get it right first |
| 5 | The divertibility rule | `at_scene`/`transporting` are never divertible. Enforced in C# in three places (plan §5.2). **Test this hard** — it is the safety rule of the whole component |
| 6 | Maps API integration | Third-party integration for the **whole project** (assignment §11). ETA ranking, `RouteLog`, reverse geocode, and the crew's live navigation. Include the failure path — a dispatch must never block on the provider being down |
| 7 | Remaining endpoints + codegen gate | |
| 8 | React: call board, fleet map, ambulance register | |
| 9 | Flutter: crew screens, then patient tracking | Two distinct audiences — worth pointing at in the demo |
| 10 | **The agent, last** | Ranks by real ETA, proposes; both human gates on top of the manual path from step 4 |
| 11 | React: dispatch queue + diversion review | The approval UI. Needs the agent to exist |

**You will need to stub:** Patient's `GET /capacity/wards` and `POST /admissions/pre-admit`; Staff's `POST /staff/lookup`; the maps API itself while developing offline.

**Others are waiting on you for:** `POST /emergency-calls` (M4's patient app posts to it), and the dispatch notification that triggers M4's pre-admission.

---

## 6. Member 2 — Staff Management (Nasrullah)

**Owns:** `Skill`, `StaffMemberSkill`, `Shift`, `Allocation`, `LeaveRequest`, `WardStaffingRule`
**Contract:** `specs/staff-spec.yaml` (32 paths) · **Design:** no plan document yet — see below
**Boundaries:** `integration_of_functions.md` §17–§21

> **Two things to clear first.** `staff-spec.yaml` does not currently validate as
> OpenAPI (§2.2), which blocks codegen for the whole team. And there is no
> `staff-management-plan.md`, unlike the other three components — the spec is
> good and detailed, so this is a gap in documentation rather than design.

| # | Step | Notes |
| :-- | :--- | :--- |
| 0 | **Fix `staff-spec.yaml` validation** | `LeaveReport` typo + `PagedResult.total_count` → `total_items`. Critical path for all four of us |
| 1 | Entities + configurations + migration | `Allocation` needs `clocked_in_at`/`clocked_out_at`, which the spec publishes but `entity_diagram.md` does not have yet — add to the diagram in the same commit |
| 2 | `POST /staff/lookup` — **build this early** | Every other component needs it to render "Approved by …". It is small, and it deletes a stub in three other people's code |
| 3 | Staff CRUD + skills | Includes deactivate/reactivate — soft delete, since allocations reference the row |
| 4 | Shifts + the overnight-shift rule | `end_time < start_time` means it ends next day. Every overlap query has to apply it |
| 5 | Allocations + coverage calculation | `CoverageStatus` derived from confirmed allocations vs `minimum_headcount` |
| 6 | Leave requests + approval | Approving releases allocations and can drop a shift below minimum |
| 7 | **Manual reallocation, no AI** | Move a nurse between shifts by hand. The agent will propose this later |
| 8 | Codegen gate | |
| 9 | React: staff CRUD, roster grid, coverage dashboard, leave approval | |
| 10 | Flutter: my shifts, clock in/out, request leave/swap | |
| 11 | **The agent, last** | The cascading swap. Deterministic validation in plain C#, never the agent checking itself |
| 12 | React: roster proposal approval | |

**You will need to stub:** Patient's ward occupancy and ward list.

**Others are waiting on you for:** `POST /staff/lookup` (all three), and `ambulance_crew` / `doctor` role claims on the JWT.

---

## 7. Member 3 — Health Equipment (Sethmin)

**Owns:** `EquipmentCategory`, `EquipmentItem`, **`Bed`**, `PharmacyCategory`, `PharmacyItem`, `PharmacyTransaction`, `MaintenanceSchedule`, `Warning`, `ActionRequest`
**Contract:** `specs/equipment-spec.yaml` (28 paths) · **Design:** `specs/equipment-management-plan.md`
**Boundaries:** `integration_of_functions.md` §13–§16

| # | Step | Notes |
| :-- | :--- | :--- |
| 1 | Entities + configurations + migration | Nine entities — the largest set. Consider splitting into two migrations, equipment and pharmacy |
| 2 | **`Bed` register + `GET /beds` — build this early** | This is Patient Management's hardest dependency (`integration_of_functions.md` §10). Until it exists M4 is working against a fake |
| 3 | Categories + equipment items CRUD | |
| 4 | Pharmacy items + the atomic stock decrement | `UPDATE ... WHERE quantity_on_hand >= :qty` — one conditional update, never read-then-write, or two people dispensing at once both succeed past zero |
| 5 | Maintenance schedules | Polymorphic over `equipment_item` and `bed` |
| 6 | **The bed-occupancy check before touching a bed** | Call M4's `GetBedOccupancyAsync` inside the same request, before committing. Occupied or held → 409, nothing written. **Maintenance never evicts a patient** |
| 7 | Deterministic warnings | Threshold sweep — low stock, expiring medicine, overdue maintenance. **Not agent-generated**; the agent reviews these |
| 8 | Codegen gate | |
| 9 | React: stock dashboard, warning queue, approval queue | |
| 10 | Flutter: report a fault, ward stock, take a bed out of service | |
| 11 | **The agent, last** | Reviews open warnings, produces a prioritised list with recommended actions |

**You will need to stub:** Patient's `GetBedOccupancyAsync`, ward list, admission summary; Staff's `POST /staff/lookup`.

**Others are waiting on you for:** the bed register (M4 — this is the big one).

---

## 8. Member 4 — Patient Management (Lochana)

**Owns:** `Patient`, `PatientAccount`, `Admission`, **`Ward`**, `BedAssignment`, `BedReservation`, `Discharge`, `DischargeChecklistItem`, `Appointment`
**Contract:** `specs/patient-spec.yaml` (33 paths) · **Design:** `specs/patient-management-plan.md`
**Boundaries:** `integration_of_functions.md` §4–§11

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
| 11 | **The bed agent, last** | Hard rules H1–H5 in deterministic C#, soft rules rank. Re-check every hard rule under a row lock at approval time |
| 12 | React: bed approval + downgrade approval | The two human gates |

**You will need to stub:** Equipment's bed register (**your hardest dependency**); Staff's `POST /staff/lookup`; Emergency's dispatch notification.

**Others are waiting on you for:** `Ward` (all three), `GET /capacity/wards` (M1), ward occupancy (M2), `GetBedOccupancyAsync` (M3), `POST /admissions/pre-admit` (M1).

---

## 9. Integration checkpoints

Four moments where the parts actually have to meet. Each one is worth doing
deliberately, together, rather than discovering at the demo.

| # | Checkpoint | Who | What proves it |
| :-- | :--- | :--- | :--- |
| 1 | **First real cross-component read** | M3 → M4 | Equipment's bed register replaces M4's stub. Delete the `STUBS.md` row |
| 2 | **Staff lookup replaces three stubs** | M2 → M1, M3, M4 | "Approved by Dr. Perera" renders from real data in all three |
| 3 | **The pre-admission call** | M1 → M4 | A dispatch creates an `Admission` in `awaiting_bed`. Watch the `urgency` translation here — Emergency's `critical/high/medium/low` becomes Patient's `routine/urgent/emergency`, and a mismatch is a 400 |
| 4 | **The full emergency workflow** | all four | `CareLanka_Component_Plan.md` §6, end to end: call in Flutter → dispatch → pre-admission → bed proposal → staffing check → equipment check → one human approval → back to Flutter. This is the assessed cross-platform workflow |

---

## 10. Things nobody owns yet

None of these belong to a component, and all of them are graded. They will not
happen by themselves.

| # | Thing | Notes | Suggested owner |
| :-- | :--- | :--- | :--- |
| 1 | **CI** — `.github/` does not exist | `CLAUDE.md` already specifies the pipeline: `dotnet build` + `dotnet test`, `check:codegen` → `typecheck`, `flutter analyze` + `flutter test`, spec validation and uniqueness checks. Assignment §13 grades it | whoever does §3 |
| 2 | **ADR** — does not exist | Assignment requires it. The §2.3 decisions are exactly its content | group, as decisions land |
| 3 | **`web-ui/` is empty** — one `.gitkeep`, no Vite project | Someone scaffolds React + Vite + TanStack Query + the generator once and commits it. Everyone builds screens on top | one person, before any React work |
| 4 | **`flutter create .` never run** — no `android/`/`ios/` | Needs the Flutter SDK installed. Documented in `mobile-ui/README.md` §5 | whoever has the SDK |
| 5 | **`pubspec.yaml` has no `swagger_parser` / `build_runner`** | `CLAUDE.md` requires the Dart client to be generated, and nothing in `pubspec.yaml` can generate it yet | with #4 |
| 6 | **`.gitignore` ignores `mobile-ui/**/*.g.dart`** | Which is what the Dart generator emits. Forces `CLAUDE.md`'s open question — is the generated client committed, or built in CI? — to be answered **before** the first Flutter build | with #4 |
| 7 | **Deployment target** | Where API, database and React app are hosted | group |

---

## 11. If something goes wrong

It will. The useful distinction is between problems that are one person's to fix
and problems that need two or three people in a room.

- **A conflict about who owns something** → `integration_of_functions.md` §11
  Open Items, with your reasoning. Do not fix it in someone else's file.
- **A schema disagreement** → the rule is already written down: where the entity
  diagram and a member's own committed spec disagree, **the spec wins**, for
  whoever owns that entity.
- **You are blocked on someone's unbuilt work** → stub it, record it, keep going
  (§1.3). Do not wait, and do not build it for them.
- **You broke something shared** — the app won't start, CI is red on `main` —
  say so immediately in the group. A shared break blocks three other people, and
  the cost of mentioning it is far lower than the cost of three people debugging
  the same thing separately.