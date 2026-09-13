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
| 7 | Discharge checklist + confirmation | **Done.** `clinical_clearance` gated on the `doctor` role claim. **Billing came with it** — `billing_settled` cannot be an honest tick with nothing behind it |
| 8 | Codegen gate | |
| 9 | React: admissions dashboard, bed board, occupancy report | |
| 9b | **The `/me/*` backend** | **Done 2026-09-12.** Seven routes: `pre-register`, `profile`, `admission`, `history`, book / list / cancel appointments. Not one takes a patient id - all scoped by the `sub` claim. `pre-register` creates no admission; see `patient-management-plan.md` §7.6 |
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

**Step 7 is complete, and billing came with it.** Three discharge endpoints
(`GET /api/discharges/candidates`, `PATCH /api/discharges/{id}/checklist`,
`POST /api/discharges/{id}/confirm`) and six billing ones under `/api/admissions/{id}/bill` and
`/api/billing/outstanding`. Migration `Patient_AddBilling`: `bills`, `bill_line_items`, and one
new column on `bed_assignments`. Two React screens, `/discharge` and `/billing`. 16 new tests.

**Billing was out of scope and is not any more.** `patient-management-plan.md` §11 listed
"billing beyond a checklist tick" as deliberately not built, in the same document that made
`billing_settled` a mandatory item. A required tick with nothing behind it is a box somebody
presses to turn a screen green. It is claimed openly in `integration_of_functions.md` §11.10 so
the other three can see it rather than find out.

Things settled while building it:

- **A bill can only contain what the schema records, and this one says so out loud.** An
  admission fee from the care level, and a line per bed per day — and nothing clinical, because
  no table anywhere ties a treatment, a scan or a drug to an admission. Equipment's
  `PharmacyTransaction` has no admission id. So reception types clinical charges by hand, and
  the design doc, the contract and the screen all state that rather than generating plausible
  line items from nothing. The tempting stub here would have been the most dangerous one in the
  project — see the new section in `STUBS.md`.
- **`billing_settled` is not tickable by anybody.** Settling the bill writes it, in the same
  transaction, and `PATCH /checklist` refuses the key from every role with `cl_pat_025`. Two
  ways to write one fact is two ways for it to be wrong, and this is the one fact where the
  disagreement would be a patient told they still owe money they have paid.
- **Settle prepares the bill first if nobody has.** Without that, a visit nobody billed could
  never tick the box and therefore could never be discharged — a deadlock with no way out from
  the desk. Found by asking what happens on the least interesting path.
- **`bed_assignments.occupied_at` had to exist.** Nothing recorded when a hold became an
  occupancy. For a single stay `Admission.AdmittedAt` happens to be the same instant; for a
  patient moved to a second bed mid-stay there were two occupancies and one arrival time, so
  "how long was this patient in *this* bed" had no answer. One nullable column, with
  `CreatedAt` as the fallback for rows written before it.
- **EF saves a new child as an UPDATE if you set its key yourself.** Adding a `BillLineItem` to
  a tracked `bill.LineItems` and calling `SaveChanges` produced
  *"expected to affect 1 row, actually affected 0"*. Change tracking decides Added-versus-
  Modified for an entity it meets through a navigation by looking at the key, and a non-default
  Guid means "this row already exists". Every new child now goes through `_db.X.Add` as well as
  the collection. The same trap is in `DischargeService` for checklist items.
- **The discharge row is created on first touch, not at admission.** Every visit already on
  `main` got one the moment it was needed, so there was no backfill and no data migration — and
  a visit nobody discharges never grows five rows it does not use. The entity comment said
  "created at admission time"; it was never true, and now it does not claim to be.
- **`Discharge` stores rows, not `jsonb`.** §6.1 of the plan said `jsonb`; step 2 built
  `discharge_checklist_items` and the plan had never been corrected. Fixed now: a tick records
  who and when, and that wants a foreign key rather than a blob.
- **Three services had each written out the same bed-label join.** `BedLabels` is now one file
  with two methods, and `AdmissionService`, `WorklistService` and `DischargeService` all call
  it. It was two copies before this step and would have been three.
- **A React fallback got a badge wrong, and only the browser found it.** A checklist with no
  rows yet still has to render five boxes, and the placeholder marked everything except
  follow-up as required — so "Transport arranged" showed a Required badge next to a hint saying
  it was optional. Tests could not see it; walking the screen could. The fallback now reads the
  same mandatory list the server does.

**The policy rework that came with it** — `Policies.cs` is group-owned, so this is in the PR
body too:

- **`PatientReader` + `AdmissionReader` → one `PatientDetails`.** The split was never real: a
  `PatientDetail` carries the patient's admissions, so every `PatientReader` role could already
  read care level, urgency and status through `GET /patients/{id}`. Two names over one level of
  access. Now six of the seven staff roles, ambulance crew excluded. `integration_of_functions.md`
  §11.8 was promising equipment management a closed door that had a window next to it, and now
  says so.
- **`PatientRegistrar` = general staff, ward nurse, duty manager.** Ambulance crew removed — the
  crew are the response team, the paperwork is done at the desk. **This one reaches into
  Emergency**, because their spec is written around ambulance crew and duty manager and
  `GeneralStaff` does not appear in it. Raised as §11.9 for Kaveesha; `emergency-spec.yaml` is
  hers and has not been touched.
- **Two new policies**, `DischargeChecklist` (nurse, doctor, manager) and `BillingDesk` (general
  staff, administrator, duty manager). Confirming a discharge reuses `AdmissionEditor`.
  **Superseded 2026-09-12** — confirming is now its own `DischargeConfirmer` (general staff,
  nurse, duty manager) and the ICU/HDU narrowing is gone. See the addendum below.
- **Four existing tests changed because the rule changed**, not to go green: equipment
  management can now read the admissions board, the administrator can too, the worklist refusal
  test moved to ambulance crew, and `AdmissionDetail` now serves `discharge` and `bill` instead
  of omitting them.

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
  a 409: it is not generosity, it is the last ICU bed spent on somebody who does not need it.
  **Revised 2026-09-12 — the duty manager may now overrule this** (see the step 6 addendum
  below); a nurse or reception gets a 403 first and never reaches the 409.
- **The approval split is a 403 from the service, twice over.** ICU and HDU are the duty
  manager's (`cl_pat_012`); so is any downgrade (`cl_pat_013`), checked first because a downgrade
  into HDU is both and "this is a downgrade" is the more specific complaint. It cannot be an
  `[Authorize]` policy because it depends on which bed the body names — same shape as check-in.
  **Revised 2026-09-12 — ICU and HDU are no longer special.** The split is now the mismatch
  alone: `cl_pat_012` is the step *up*, `cl_pat_013` the step down, and a matching ward is
  anybody's. See the addendum below.
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

**Step 6 addendum (2026-09-12) — who may place a patient, and a rule about age.** Recorded for
the group as `integration_of_functions.md` §11.14.

- **New `Policies.BedAssigner`** — general staff, ward nurse, duty manager — on `assign-bed` and
  `correct-bed`, replacing `AdmissionEditor` there. Reception beds the walk-in it just
  registered. `AdmissionEditor` is untouched, so reception still cannot cancel a visit or
  confirm a discharge.
- **The role rule is now the mismatch and nothing else.** `NeedsDutyManager` no longer names
  `icu`/`hdu` at all: it is `IsDowngrade || IsMoreAcuteThanNeeded`. An ICU bed for an ICU patient
  is a match, so a nurse or reception may make it — the old rule made the hospital's most urgent
  admission wait for a signature on the obvious. Scarcity is an argument against giving an ICU
  bed to somebody who does *not* need one, which is the step-up case, and that is still gated.
- **The duty manager may place a patient in a ward more acute than assessed**, which H2 used to
  refuse for everybody. That is the step-up gate above, and the one place `cl_pat_012` now fires.
- **Discharge went the same way, and for the same reason.** New `Policies.DischargeConfirmer`
  (general staff, ward nurse, duty manager) on `POST /discharges/{id}/confirm`, and
  `DischargeService.EnsureMayConfirm` deleted outright — ICU and HDU discharges are no longer
  the duty manager's. **`cl_pat_024` is retired and its number must never be reused.** The gate
  was always the checklist: `clinical_clearance` is a doctor's alone and `billing_settled` is
  written only by settling the bill, so nobody goes home un-cleared or unpaid whoever confirms
  it. A duty manager signing after the doctor had already cleared the patient was delay, not
  safety. Reception is on the list because it settles the bill on that same screen.
- **New hard rule H6 and code `cl_pat_030`** — a `pediatric` ward takes only patients under 18.
  One-directional, and an unrecorded date of birth counts as an adult. No override, because like
  the gender policy it is a property of the ward.
- **`GET /bed-availability` now allows `pageSize` up to 500.** At the shared cap of 100 the
  seeded hospital's 135 beds were cut off mid-alphabet, so pediatric and surgical beds could
  never be chosen — silently, which is what made it dangerous. **Found by clicking the screen,
  not by a test**, and the same walkthrough caught two more: reception got a red "your role does
  not allow this" from the walk-in `/arrive` chain that only a ward nurse may call, and an ICU
  bed for an ICU patient was wrongly coloured as an override when it is simply the right bed.

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

- **A signup with no medical record behind it** is the ordinary state for a new account, not
  an error. **Do not read `principal.patient_id` to detect it** — common auth hard-codes that
  to null for everybody, so it cannot tell a linked account from an unlinked one
  (`integration_of_functions.md` §11.7). Call `GET /api/me/profile` on startup: 200 means
  there is a record, 404 (`cl_pat_033`) means show the details form.
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
us for** — and after the policy rework on 2026-09-11 its `Roles:` line and `Policies.cs`
disagree about `AmbulanceCrew`. Nothing is broken today because the endpoint does not exist, but
that has to be settled before it does. `integration_of_functions.md` §11.9.
