# Commit + PR draft — `feat/patient-admission-entities`

Scratch file to copy from. **Delete it before committing** — it is tracked, so it
will show up in `git status`.

Branch is created and checked out. Nothing is staged.

---

## Two commits

Split this way because the repo's own rule forces it: a schema change and the
document that describes it move together. So `entity_diagram.md` is not its own
commit — it belongs with the code that changed it.

### 1 — the entities, the migration, and the diagram

```
git add api/Data/Enums api/Data/Entities/Patient api/Data/Configurations/Patient \
        api/Data/Migrations api/Data/CareLankaDbContext.cs \
        docs/entity_diagram.md

git commit -m "feat(patient): add admission, appointment, bed and discharge tables"
```

Message body if you want one:

```
Adds the six remaining Patient Management entities in one migration:
Patient, Appointment, Admission, BedAssignment, Discharge and
DischargeChecklistItem, plus twelve enums.

One migration rather than six, because every EF migration snapshots the
whole model and six would be six chances to collide with the other tracks.

Where entity_diagram.md and patient-spec.yaml disagreed, the spec won, per
CLAUDE.md. The diagram is updated to match as Revision 2.9.
```

### 2 — the plan documents that named a table that no longer exists

```
git add docs/build/patient.md docs/BUILD_PLAN.md \
        docs/CareLanka_Component_Plan.md docs/build/equipment.md

git commit -m "docs: drop BedReservation from the plan documents"
```

---

## Push and open the PR

```
git push -u origin feat/patient-admission-entities
gh pr create --base main --title "feat(patient): admission, bed and discharge tables"
```

---

## PR description

### What this adds

The six remaining Patient Management tables, in one migration
(`Patient_AddAdmission`). No endpoints, no DTOs, no services — CRUD is the next
PR. This one is schema only, so it stays reviewable.

| Table | What it holds |
| :--- | :--- |
| `patients` | One row per human being, reused across visits |
| `appointments` | A booked visit, before it becomes an admission |
| `admissions` | One row per hospital visit, with the 7-state status |
| `bed_assignments` | Who is in which bed, and the 30-minute hold on one |
| `discharges` | The 1:1 companion row, created at admission time |
| `discharge_checklist_items` | The five tickable boxes |

Twelve enums, all stored as their snake_case wire values with a `CHECK`
constraint per column, matching how `Ward` already does it.

### The concurrency guarantee

Two nurses assigning the same bed at the same instant both pass an
application-level "is it free?" check. These are what actually stop it:

```sql
CREATE UNIQUE INDEX ux_bed_assignments_live_bed ON bed_assignments (bed_id)
    WHERE status IN ('reserved', 'occupied');
CREATE UNIQUE INDEX ux_bed_assignments_live_admission ON bed_assignments (admission_id)
    WHERE status IN ('reserved', 'occupied');
```

A hold and an occupancy both claim the bed, so one pair of indexes covers both.

### Five places the diagram and the spec disagreed

`CLAUDE.md`: on an entity Member 4 owns, the committed spec beats
`entity_diagram.md`. All five went the spec's way, and the diagram is updated in
the same commit as **Revision 2.9**.

1. **`BedReservation` is dropped.** The hold is a `bed_assignments` row with
   `status = 'reserved'` and a `reserved_until`. The spec publishes one bed row
   and has no reservation schema and no reservation path. Two tables would have
   put "this bed is claimed" in two places — the same drift the diagram rejects
   `Bed.Status` for.
2. **`Patient` has one `full_name`**, plus `nic`, `phone` and a new `address`
   column. `address` was already in `PatientDetailField` with nothing behind it.
3. **`Discharge` has no `readiness_status`.** Readiness is counted from the
   checklist rows, so the flag and the boxes cannot disagree. Five item types
   rather than three, each with an `is_mandatory` flag.
4. **`Admission` drops `emergency_call_id` and `appointment_id`.** `dispatch_id`
   is a string, Emergency's own reference rather than a foreign key — so this
   table does not wait on Member 1's `dispatches` existing. The appointment link
   is `appointments.admission_id`: one link, one owner.
5. **`DischargedAt` added** to `admissions`, which the spec returns and the
   diagram had nowhere for.

### Cross-component references are FKs with no navigation property

Every `*_staff_member_id` and both `patient_account` links are configured with
`HasOne<StaffMember>()` and no navigation. That is
`integration_of_functions.md` §5.1 — *we store the id and nothing else* — enforced
by the model rather than by discipline, because there is no `Include()` to reach
for.

It also removes a real bug. `StaffMember` is soft-deletable and
`Admission.CategorySetByStaffMemberId` is required, so a navigation would have
dropped every admission a retired clinician ever categorised out of every query.
EF Core warned about exactly this; it is fixed rather than suppressed.

### What is deliberately not a foreign key

- **`bed_assignments.bed_id`** — `beds` is Health Equipment's table (M3) and does
  not exist yet. `STUBS.md` row 109 still stands. The constraint goes in when
  their table lands.
- **`bed_assignments.workflow_id`** — `AgentWorkflow` is common and unbuilt.
- **`admissions.dispatch_id`** — Emergency's reference, carried as their string.

### Not in this PR

Endpoints, DTOs and services (step 3), the state-machine transition guard
(step 4), and `CareRecommendation` (step 13, self-contained and gating nobody).

### Documents changed

`entity_diagram.md` (Rev 2.9), `docs/build/patient.md`, `docs/BUILD_PLAN.md`,
`docs/CareLanka_Component_Plan.md`, and one line of `docs/build/equipment.md` —
that file named `BedReservation`, and leaving it would have had Equipment build
against a table that no longer exists.

### Checks

- `dotnet build` — clean, 0 warnings
- `dotnet test` — 35/35 pass, against a real PostgreSQL. The suite runs
  `Database.MigrateAsync()`, so the new migration is applied and verified on every
  run: the generated column, both partial unique indexes, the `text[]` default and
  all fifteen check constraints.
- `npm run check:specs` — 5 specs, 0 duplicate routes, 0 duplicate operationIds,
  0 schema conflicts. No spec file changed in this PR.
