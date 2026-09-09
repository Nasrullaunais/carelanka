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
| 4 | **The 7-state admission status machine** | `awaiting_bed → awaiting_approval → bed_reserved → admitted → ready_for_discharge → discharged`, plus `cancelled`. Illegal transitions → 409. **This is the backbone — test it hardest** |
| 5 | `GET /capacity/wards` + `GET /wards/{id}/occupancy` | M1 and M2 are both blocked on these. Before the agent |
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

**Steps 1 and 2 are done, and step 3 is half done.** `Ward` landed in `Patient_AddWard` (PR #12). `Patient`,
`Admission`, `Appointment`, `BedAssignment`, `Discharge` and `DischargeChecklistItem`
landed in `Patient_AddAdmission`, with their configurations and the partial unique indexes.
Building them settled five disagreements between `entity_diagram.md` and
`patient-spec.yaml`, all resolved in the spec's favour and written up as Revision 2.9 of the
diagram. The one worth knowing before step 6: **`BedReservation` no longer exists.** The
30-minute hold is a `BedAssignment` row with `status = 'reserved'` and a `reserved_until`,
which is what the spec has always published.

**Step 3, patients half.** `POST /patients`, `GET /patients`, `GET /patients/{id}`,
`PUT /patients/{id}`, `POST /patients/lookup` and `POST /patients/{id}/link-account` are
built and tested. The admissions half — `createAdmission`, `listAdmissions`,
`getAdmission`, `completeAdmissionDetails` — is the next branch. Three things settled while
building the first half:

- **`temp_reference` is generated, not requested.** Register with no NIC and no phone and
  the server allocates `UNKNOWN-2026-0001`, numbered per year. Supplying a NIC later never
  clears it.
- **Three new policies.** `PatientRegistrar`, `PatientReader` and `PatientEditor` in
  `Common/Auth/Policies.cs`, because the role combinations this spec publishes did not
  exist. Additive, flagged for the group.
- **A query-string enum needs a type converter.** `?sortBy=full_name` does not bind to
  `FullName` without one; the JSON converter only covers request and response bodies. See
  `SnakeCaseEnumTypeConverter`.

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
may be asked to modify a business rule live. Mirror the transition matrix in React and
offer only legal moves — a transition with side effects has its own endpoint, and PATCHing
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

**Others are waiting on you for:** `Ward` (all three), `GET /capacity/wards` (M1),
`GET /wards/{id}/occupancy` (M2), `GET /beds/{id}/occupancy` (M3 — they cannot service any
bed safely until this is real), `POST /admissions/pre-admit` (M1).
