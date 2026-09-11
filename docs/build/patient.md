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
| 6 | **Manual bed assignment, no AI** | Pick a bed by hand, with the 30-minute hold. The concurrency guarantee lives in the partial unique index, not in code |
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

**Steps 13–16 are self-contained.** Nothing else in the group depends on the care advisory
agent, and it depends on nothing outside this component beyond the `doctor` role claim,
which auth already issues. Build it in parallel with the bed-agent track or after it — it
does not gate anyone and nobody gates it.

---

## Things that will bite

**The concurrency guarantee is the index, not your code.** Two nurses assigning the same
bed at the same moment both pass an application-level "is it free" check. The partial
unique index is what actually stops it:

```sql
CREATE UNIQUE INDEX ux_bed_assignments_live_bed ON bed_assignments (bed_id)
    WHERE status IN ('reserved', 'occupied');
```

A hold and an occupancy both claim the bed, so one index covers both races. There is no
separate `bed_reservations` table — see the step 2 note below.

Catch the constraint violation and return 409. Do not try to prevent it with a prior read.

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
| `GET /beds` — the bed register | M3 | **Your hardest dependency.** The bed agent has nothing to reason over without it |
| `POST /staff/lookup` | M2 | Rendering "Approved by …" |
| Dispatch notification | M1 | Triggers your pre-admission |

**Others are waiting on you for:** ~~`Ward` (all three)~~ **built**,
~~`GET /capacity/wards` (M1)~~ **built 2026-09-11**,
~~`GET /wards/{id}/occupancy` (M2)~~ **built 2026-09-11**,
`GET /beds/{id}/occupancy` (M3 — they cannot service any bed safely until this is real, and
it is now the only one of the four still outstanding), `POST /admissions/pre-admit` (M1).
