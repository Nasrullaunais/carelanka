# Build Track 4 — Patient Management

**Owner: Lochana (Member 4)** · **Index:** `docs/BUILD_PLAN.md`

**Owns:** `Patient`, `PatientAccount`, `Admission`, **`Ward`**, `BedAssignment`,
`Discharge`, `DischargeChecklistItem`, `Appointment`, **`CareRecommendation`**
**Contract:** `specs/patient-spec.yaml` (40 paths) · **Design:** `specs/patient-management-plan.md`
**Boundaries:** `specs/integration_of_functions.md` §4–§11

> **Two agents, not one** — see steps 13–16. Added on the lecturer's direction at topic
> finalization; §8.10 of the design doc has the full reasoning. The bed agent decides
> *where to put someone*; the care advisory agent *drafts a note for a doctor to check*.
> Neither decides what care a patient needs.

> **`Ward` is the most-depended-on table in the system.** All four components reference it
> (`Shift.WardId`, `EquipmentItem.WardId`, `Dispatch.DestinationWardId`). Build it first
> and then treat its schema as frozen — changing it breaks three other people.

> **`PatientAccount` — the table is yours, the login is common.** Registration, login and
> the JWT for a patient are built in `docs/build/common.md` §2.7. You own the table, the
> `Patient.UserAccountId` link, and `POST /patients/{id}/link-account`.

---

## Steps

| # | Step | Notes |
| :-- | :--- | :--- |
| 1 | **`Ward` first** | Three other components are blocked behind it. Then freeze the schema |
| 2 | Remaining entities + configurations + migration | Including `PatientAccount` (Rev 2.5) and the optional link `Patient.UserAccountId` |
| 3 | Patient + Admission CRUD | Including `temp_reference` for unidentified arrivals |
| 4 | **The 7-state admission status machine** | **Done.** `awaiting_bed → awaiting_approval → bed_reserved → admitted → ready_for_discharge → discharged`, plus `cancelled`. Illegal transitions → 409 |
| 5 | `GET /capacity/wards` + `GET /wards/{id}/occupancy` | **Done.** M1 and M2 are unblocked. Hold expiry lives in `CapacityService` and nowhere else |
| 6 | **Manual bed assignment, no AI** | **Done.** Pick a bed by hand, with the 30-minute hold. The concurrency guarantee lives in the partial unique index, not in code |
| 6b | **Visits that need no bed (H0) + the patients board** | **Done.** An `outpatient` never enters `awaiting_bed`. `GET /api/patient-worklist` unions bookings and visits so the board can say "not arrived". **No migration** |
| 7 | Discharge checklist + confirmation | `clinical_clearance` gated on the `doctor` role claim |
| 8 | Codegen gate | |
| 9 | React: admissions dashboard, bed board, occupancy report | |
| 10 | Flutter: nurse screens, then the patient's own-stay screens | Local notifications on status change is your device feature |
| 11 | **The bed agent** | Hard rules H1–H5 in deterministic C#, soft rules rank. Re-check every hard rule under a row lock at approval time |
| 12 | React: bed approval + downgrade approval | The two human gates |
| 13 | `CareRecommendation` entity + configuration + migration | Independent of the bed workflow — no dependency on steps 1–12 beyond `Patient` and `Admission` existing |
| 14 | **The care advisory agent, last** | Deterministic red-flag keyword screen (§8.13) runs *before* the model, not after. Rules CR1–CR4 (§8.15) validated the same way H1–H5 are |
| 15 | React: Doctor's care recommendation queue, approve/reject | The third human gate in this component |
| 16 | Flutter: "ask about a symptom" + "my care recommendations" | Patient-facing; never renders `agent_message` or `rejection_reason` |

**Steps 1, 2 and 3 are done.** `Ward` landed in `Patient_AddWard` (PR #12). `Patient`,
`Admission`, `Appointment`, `BedAssignment`, `Discharge` and `DischargeChecklistItem`
landed in `Patient_AddAdmission`, with their configurations and the partial unique indexes.
Building them settled five disagreements between `entity_diagram.md` and
`patient-spec.yaml`, all resolved in the spec's favour and written up as Revision 2.9 of the
diagram. The one worth knowing before step 6: **`BedReservation` no longer exists.** The
30-minute hold is a `BedAssignment` row with `status = 'reserved'` and a `reserved_until`,
which is what the spec has always published.

**Step 5 is complete, and appointments with it.** `GET /api/capacity/wards` and
`GET /api/wards/{id}/occupancy` are live for every staff role, so Kaveesha and Nasrullah are
no longer waiting. Channeling followed: `GET /api/appointments`, `POST /api/appointments` and
`POST /api/appointments/{id}/check-in`. **No migration** — every table and column those five
endpoints read was already on `main`. Four things settled while building them:

- **Nothing in this project has a timezone.** `?date=` on the appointments worklist filters
  whole **UTC** days, which is what the column stores — so "today" starts at half past five in
  the morning in Colombo. Inventing a timezone inside one filter would make that list disagree
  with every report, so it is an open decision rather than a local fix.
- **`incoming_next_2h` needed a hand-written wire name.** The snake_case policy does not break
  before a digit, so `IncomingNext2h` serialised as `incoming_next2h` and the contract says
  `incoming_next_2h`. The only property in this component with a number in it, and
  `PatientOpenApiContractTests` is what found it.
- **The check-in role split reads the body, not the route.** A ward nurse may check somebody in
  as `outpatient`, `day_case` or `inpatient`; `icu` and `hdu` are the duty manager's. That
  cannot be a policy on the action, so it is a check in `AppointmentService` returning
  `cl_pat_011`.
- **"One open booking at a time" is a read with no index behind it**, unlike the open-admission
  rule. Closing it properly is a partial unique index and therefore a migration. Left open on
  purpose: the damage is two rows on a worklist, and the second check-in is still refused by
  the admission index that does exist.

**Steps 3 and 4 are complete.** Patients half: `POST /patients`, `GET /patients`,
`GET /patients/{id}`, `PUT /patients/{id}`, `POST /patients/lookup`,
`POST /patients/{id}/link-account`. Admissions half: `POST /admissions`, `GET /admissions`,
`GET /admissions/{id}`, `PATCH /admissions/{id}/details`, and from step 4
`POST /admissions/{id}/arrive` and `POST /admissions/{id}/cancel`. **Step 5, the capacity
endpoints Kaveesha and Nasrullah are both blocked on, is next.** Things settled while building
step 3:

- **`temp_reference` is generated, not requested.** Register with no NIC and no phone and
  the server allocates `UNKNOWN-2026-0001`, numbered per year. Supplying a NIC later never
  clears it.
- **Three new policies.** `PatientRegistrar`, `PatientReader` and `PatientEditor` in
  `Common/Auth/Policies.cs`, because the role combinations this spec publishes did not
  exist. Additive, flagged for the group.
- **A required enum in a request body has to be nullable to actually be required** — see the
  step 4 notes below, where this was found and swept across all four request bodies.
- **A query-string enum needs a type converter.** `?sortBy=full_name` does not bind to
  `FullName` without one; the JSON converter only covers request and response bodies. See
  `SnakeCaseEnumTypeConverter`. `AdmissionStatus`, `AdmissionSource` and `AdmissionCategory`
  carry the attribute for the same reason — all three appear in a query string.
- **`missing_fields` is computed at admission, not asked for.** It is the closed
  `PatientDetailField` vocabulary, read off the patient row. `details_complete` is a stored
  generated column over it (`cardinality(missing_fields) = 0`), so the two cannot disagree
  and neither is ever set by hand.
- **Completeness is not part of `status`.** A patient can be `admitted` with paperwork
  still outstanding. One field cannot express both without ambiguity.
- **`AdmissionDetail` publishes less than the spec describes.** `workflows` needs the common
  `AgentWorkflow` tables (ADR 3) and `discharge` needs step 7, so both keys are omitted
  rather than returned empty, and `patient-spec.yaml` says NOT SERVED YET on each. The
  `wardId` filter on `GET /admissions` is unpublished for the same reason — a ward is
  reached through a live `BedAssignment`, which arrives at step 6.
- **Sorting by urgency is a ranked CASE, not the column.** The stored value is a
  snake_case string, so ordering it alphabetically gives emergency, routine, urgent — which
  reads like a sort and is not one. Write the rank inline: a helper method inside the lambda
  is not something EF can turn into SQL, and it fails at run time.

**Step 4 is complete.** The workflow lives in `Services/Patient/AdmissionStatusMachine.cs` and
nowhere else, and two endpoints go through it: `POST /admissions/{id}/arrive` and
`POST /admissions/{id}/cancel`. 29 new tests — 13 unit tests on the table itself, 16
integration tests through HTTP. Things settled while building it:

- **The table is one question and an endpoint is a narrower one, and both get asked.**
  `ready_for_discharge -> admitted` is a legal move, but `/arrive` is not what makes it —
  arriving stamps `admitted_at` and a nurse un-flagging a discharge must not. So every
  endpoint names the states *it* starts from as well as going through the table. Checking only
  the table lets one endpoint do another's job; checking only the endpoint's own list lets a
  typo invent a move the workflow does not have.
- **The 49 from/to pairs are tested with no database at all.** `AdmissionStatusMachineTests`
  sweeps every pair, including the ones no endpoint can reach until steps 6 and 7 — which is
  exactly where a mistake would sit unnoticed until something was built on top of it. The
  expectation is written as the plain text of `patient-management-plan.md` §4.2, not as a copy
  of the machine's own C# dictionary: a test that asks the code what it does agrees with every
  bug it has.
- **A second drift gate holds the machine against the published contract.**
  `patient-spec.yaml` prints the whole workflow in the description of its `IllegalTransition`
  response, so `PatientOpenApiContractTests` parses that text and compares it with the table,
  both directions. A move added to one and not the other fails there rather than at the viva.
- **The race is real and an index cannot catch it.** A manager cancelling and a nurse marking
  arrival both read `bed_reserved`, both pass the check, and the later write wins — a cancelled
  patient ending up admitted. Both rows are legal on their own, so there is no unique index to
  lean on. `SELECT ... FOR UPDATE` on the admission row makes the second request wait, re-read
  and get the honest 409. Same row lock step 11 already plans to use at approval time.
- **`cancel_note` needed a column.** The spec's cancel body has always published an optional
  `note` and nothing stored it. Accepting and dropping it would have been a field that looks
  saved and is not, so: `Patient_AddCancelNote`, published back on the `Admission` response,
  entity diagram Rev 2.10.
- **Every required enum on a request body is nullable, on purpose.** `[Required]` on a plain
  enum always passes: the binder has already turned an absent key into the first declared
  member, so there is nothing left for validation to object to. Making the property nullable is
  what turns a missing key into a 400. Found on `CancelAdmissionRequest` and then swept: all
  seven properties across four request bodies are fixed, with a test each that omits the key.
  **Two of them were not cosmetic.** `admission_category` defaulted to `icu`, which is the top
  of the downgrade ladder and the input to hard rule H2 — a body missing the key filed the
  patient at the most acute care level in the hospital with no error. `gender` defaulted to
  `male`, which defeats something deliberate: `Gender.Unknown` exists precisely so hard rule H3
  behaves deterministically for an unidentified arrival, and omitting the key recorded that
  patient as male — exactly the case `Unknown` was added to handle. **None of it changed the
  published contract**: Swashbuckle emits a `$ref` with the property still in `required`, so the
  generated client is byte-identical and this is a server-side validation fix only.
  Equipment's request DTOs were checked and do not have the hole — its only two enum properties
  are already nullable, on PATCH-style updates where nullable means "not supplied".
- **`/arrive` is `WardNurse` and `/cancel` is `DutyManager`**, both single-role, taken straight
  from the `Roles:` line on each operation. No new entry in the group-owned `Policies.cs`: a
  policy per staff role already exists, and only a *combination* needs naming.
- **Tests reach `bed_reserved` by writing the rows directly.** Nothing can reserve a bed until
  step 6, and waiting would have left the only happy path through the backbone untested.
  `AdmissionEndpointTests.ReserveABedAsync` writes exactly what step 6 will write, so the tests
  should keep passing when it lands and the helper can then be deleted.

**Step 6b: an outpatient does not need a bed, and the board is two tables.** Added after
step 6, because using the screen found a bug step 6 could not see.

**What was wrong.** `CreateAsync` started *every* admission at `awaiting_bed`, and the only
edge into `admitted` runs `awaiting_bed → awaiting_approval → bed_reserved → admitted`. Every
one of those steps is about a bed. So a patient here for a scan or a blood test — who is never
going to be given one — sat on the bed board indefinitely, was offered an "Assign bed" button,
and had no route to `discharged` at all. The only way out of their visit was to cancel it.

**Four changes, and no migration**, because the rule is derived rather than stored:

- **`BedPlacementRules.RequiresBed(category)` — hard rule H0.** `outpatient` needs no bed;
  every other care level does, `day_case` included (they are on a real bed for hours).
  Published as `requires_bed` on `AdmissionSummary` so no client re-derives it.
- **A visit needing no bed is born `admitted`,** with `admitted_at` stamped and
  `expected_arrival` dropped — opening the record *is* the arrival. `assign-bed` refuses it
  with `cl_pat_021` before the transition check, because "cannot move from admitted to
  awaiting_approval" tells a nurse nothing.
- **`POST /admissions/{id}/complete`** ends it. Two hops through `ready_for_discharge`, the way
  `assign-bed` goes through `awaiting_approval`. **Deliberately not step 7:** a visit holding a
  bed is refused with `cl_pat_020`, because a discharge has a checklist, a summary note, an
  approver and a bed to give back, and this endpoint does none of those.
- **`GET /api/patient-worklist`** unions scheduled `Appointment`s with `Admission`s, so the
  patients screen can show somebody who has booked and not turned up. An admission is created
  by arriving, so a list of admissions could never say "not arrived" about anybody. A
  checked-in booking appears once, as its visit.

Two traps the union set, both worth not repeating:

- **A flat projection of both tables fails at run time**, not at compile time:
  *"reading as Int32 is not supported for character varying"*. A `UNION` takes each column's
  type from its first branch, and every enum is a bare `null` on the booking branch and a
  converted string on the visit branch. The union now carries **only an id and one timestamp**;
  both sides are read by id afterwards, for the twenty rows that survive paging.
- **Paging the two halves separately and stitching them is wrong.** A page boundary of the
  combined list falls in the middle of neither half, so one row is shown twice and another
  never at all. `WorklistEndpointTests` asserts two pages of a six-row union are six distinct
  ids.

17 new tests. `WorklistStatus` is derived and never stored — the transition rules stay on
`AdmissionStatus`, and the contract test asserts `/patient-worklist` publishes **only** a
`get`, so nobody adds a write that would make it a fourth place a status can change.

---

**Step 6 is complete, and it retired the last stub pointing at us.** Three endpoints:
`GET /api/bed-availability`, `POST /api/admissions/{id}/assign-bed` and
`GET /api/beds/{id}/occupancy`. **No migration** — `ux_bed_assignments_live_bed` landed with
`Patient_AddAdmission` at step 2, and nothing in this step needed a new column. 35 new tests.
Eight things settled while building it:

- **The hold rule moved out of `CapacityService` into `BedHold`.** It had one reader; it now has
  four, and copying it into each of them is exactly what `patient-spec.yaml` promises we will
  not do. It is written **twice inside that one file**, on purpose: `LiveOn` is an expression
  because EF turns it into SQL, `IsLive` is a plain method for rows already in memory, and EF
  cannot translate a method call inside a query. Two spellings side by side beat two spellings
  in two files.
- **An expired hold still holds its slot in the index, so the write path has to close it.** The
  index covers every row with status `reserved` or `occupied`, and a unique index cannot consult
  the clock — so a lapsed hold reads as free everywhere and is still un-assignable in practice.
  A nurse picks the bed the list just offered and gets a 409 they cannot act on. `assign-bed`
  now releases lapsed holds on that bed first, as `release_reason = hold_expired`, and puts the
  admission that was holding it back to `awaiting_bed`. **This is the one place expiry is
  written rather than applied at read time**, and it is the bug this step nearly shipped.
- **`awaiting_bed -> bed_reserved` is not an edge, and it did not become one.** The published
  table goes through `awaiting_approval`, and the endpoint moves two hops in one transaction:
  assigning by hand *is* the proposal and the approval in one act, so both moves happen and both
  are checked. Nothing observes the middle state. Adding the edge would have meant editing the
  workflow in three documents plus two drift tests to save one line.
- **H2 refuses an *upgrade* as well as an unapproved downgrade.** Ward types map onto three
  rungs — icu, hdu, everything else — and `day_case` / `outpatient` sit on the bottom rung with
  `inpatient` because there is no ward type below `general`. A general patient into an ICU bed is
  a 409 even for a duty manager: it is not generosity, it is the last ICU bed spent on somebody
  who does not need it.
- **The approval split is a 403 from the service, twice over.** ICU and HDU are the duty
  manager's (`cl_pat_012`); so is any downgrade (`cl_pat_013`), checked first because a downgrade
  into HDU is both and "this is a downgrade" is the more specific complaint. It cannot be an
  `[Authorize]` policy because it depends on which bed the body names — same shape as check-in.
- **One message code per hard rule, not one for "that bed will not work".** `cl_pat_014` taken,
  `015` out of service, `016` too acute, `017` gender policy, `018` isolation, `019` retired
  ward. A nurse who is told which rule refused them knows which other bed to try.
- **`bed_id` had to be nullable to be required.** `[Required]` on a plain `Guid` always passes:
  the binder turns an absent key into `Guid.Empty`, so the request got as far as a 404 for the
  all-zeroes bed instead of a 400. The same trap as every required enum in this component, found
  by a test that omits the key. **The published contract is unchanged.**
- **The occupancy answer is its own interface, to break a dependency cycle.** Assigning a bed
  needs Equipment's register, and Equipment's bed service asks us about occupancy — one service
  doing both is `BedService -> occupancy -> bed register -> BedService`, which the container
  throws on at the first request. `IBedOccupancyService` reads nothing but our own
  `bed_assignments`, so nothing points back.

Two things this step made possible and deliberately did not do: the `wardId` filter on
`GET /admissions` (now unblocked — a ward is reachable through a live assignment), and a sweep
that closes lapsed holds nobody has re-assigned over. Both are in `STUBS.md`.

**Steps 13–16 are self-contained.** Nothing else in the group depends on the care advisory
agent, and it depends on nothing outside this component beyond the `doctor` role claim,
which auth already issues. Build it in parallel with the bed-agent track or after it — it
does not gate anyone and nobody gates it.

---

## Things that will bite

**The concurrency guarantee is the index, not your code** — *and the index cannot read a
clock.* Two nurses assigning the same bed at the same moment both pass an application-level
"is it free" check. The partial unique index is what actually stops it:

```sql
CREATE UNIQUE INDEX ux_bed_assignments_live_bed ON bed_assignments (bed_id)
    WHERE status IN ('reserved', 'occupied');
```

A hold and an occupancy both claim the bed, so one index covers both races. There is no
separate `bed_reservations` table — see the step 2 note below.

Catch the constraint violation and return 409. Do not try to prevent it with a prior read.

**The trap inside that:** the index covers any `reserved` row, and it has no idea whether the
hold has run out. So an expired hold makes a bed report *free* on every read and stay
*un-assignable* in the database. Whatever writes an assignment has to close lapsed holds on that
bed first — the one place the 30-minute rule is written down rather than applied at read time.
Step 6 does it in `BedAssignmentService.ReleaseLapsedHoldsAsync`; step 11 must go through the
same path.

**A patient record is not a patient account.** A `Patient` row is a medical record created
by staff, and it exists whether or not that person ever installs the app — an unconscious
arrival has a record and no account, forever. A `PatientAccount` is a login. The link is
`Patient.UserAccountId`, nullable and unique, made deliberately by staff through
`link-account` after checking identity — **never inferred from a matching phone number**,
because two people share a phone far more often than a hospital would like.

**The state machine is the thing a marker will poke at.** Seven states, and §17.2 says you
may be asked to modify a business rule live — which is one edit to the `Moves` table in
`AdmissionStatusMachine`, plus the same edit to `patient-management-plan.md` §4.2 and the
`IllegalTransition` description in `patient-spec.yaml`, because two tests hold the three
against each other. Mirror the transition matrix in React and offer only legal moves — a transition with side effects has its own endpoint, and PATCHing
straight to `admitted` skips the approval and the approver stamp.

**Urgency translation at the M1 → M4 boundary.** Emergency's `critical/high/medium/low`
becomes your `routine/urgent/emergency`. A mismatch is a 400 and it only shows up at
integration checkpoint 3.

**Two human gates, and they are different people.** A ward nurse confirms a normal-ward bed
in Flutter; the Duty Manager confirms ICU, high-dependency or **any downgrade** in React.
`AgentWorkflow.RequiredApproverRole` persists which one, so the authorization rule is
visible in the audit trail instead of buried in C#.

**Is `Referral` a fourth admission source?** (Open Decision 10.) Your spec publishes three.
Adding it means changing a committed enum, so it is not a diagram-only change. Your call.

---

## Auth

Your roles: `ward_nurse` and `patient` (Flutter), `duty_manager` and `doctor` (React).

You have the largest `/me/*` surface in the project — `/me/admission`, `/me/history`,
`/me/appointments`, `/me/pre-register`, `/me/care-recommendations`. **Every one is scoped
by the `sub` claim, never by a parameter.** This is the component where an id-in-the-route bug means one patient reading
another patient's medical record, which is both a §16.1 security failure and the worst
possible demo.

Two things your Flutter screens must handle:

- **`principal.patient_id` is null** for someone who signed up but has never been treated
  here. That is the ordinary state for a new account, not an error.
- `typ: patient` accounts see a completely different navigation tree from staff. `GET
  /auth/me` on startup is what decides which.

`clinical_clearance` on the discharge checklist is gated on the `doctor` claim.
`Doctor` is a Staff Management role — you only check the claim, you do not own the list.

---

## Dependencies

**You will need to stub:**

| What | From | Contract |
| :--- | :--- | :--- |
| ~~`GET /beds` — the bed register~~ | M3 | **Real since 2026-09-10.** Read it through `IBedRegistryService`, which is the one file in this component that knows Equipment's bed table exists. Never write it |
| `POST /staff/lookup` | M2 | Rendering "Approved by …" |
| Dispatch notification | M1 | Triggers your pre-admission |

**Others are waiting on you for:** ~~`Ward` (all three)~~ **built**,
~~`GET /capacity/wards` (M1)~~ **built 2026-09-11**,
~~`GET /wards/{id}/occupancy` (M2)~~ **built 2026-09-11**,
~~`GET /beds/{id}/occupancy` (M3)~~ **built 2026-09-11**, `POST /admissions/pre-admit` (M1).

**So `POST /admissions/pre-admit` for Kaveesha is the only thing anybody is still waiting on
us for.**
