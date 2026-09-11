# Entity Class Diagram

Reflects all decisions settled during the schema-design grilling session (37 decisions,
single hospital / multiple wards, unified staff identity, generic agent-workflow and
audit-log schemas). PKs are `Guid` (PostgreSQL `uuid`, `default: gen_random_uuid()`)
throughout.

**Revision 2.11** — patients got a short identifier of their own. One new column, in the
`Patient_AddPatientCode` migration. Changes marked *(Rev 2.11)*.

- **`Patient.PatientCode`** — eight characters, `P7K2X9QM`. The other three components have to
  name a patient on their own screens, and the only identifier this component published was a
  Guid: unreadable off a wristband and untypable into a form. The Guid is still the key
  everything stores; the code is what a person carries between screens, and searching turns it
  back into a Guid. Its unique index is the one on `patients` **not** scoped to `is_active`,
  for the reason set out under the table.

**Revision 2.10** — the seven-state admission status machine landed (step 4 of
`docs/build/patient.md`: `POST /admissions/{id}/arrive`, `POST /admissions/{id}/cancel`, and
the transition table both of them go through). One new column, in the
`Patient_AddCancelNote` migration. Changes marked *(Rev 2.10)*.

- **`Admission.CancelNote`** — the free-text half of a cancellation, next to the enum.
  `patient-spec.yaml` has always published a `note` on the cancel request body, and there was
  no column to put it in; accepting it and dropping it would have been a field that looks
  stored and is not. Now stored and published back as `cancel_note` on the `Admission`
  response, because "why is this bed free again?" a week later is rarely answered by
  `diverted_to_other_hospital` on its own.
- **No column for the transition table, and none for concurrency.** The seven states and the
  legal moves between them live in `AdmissionStatusMachine`, in code — they are rules, not
  data. Two people acting on one visit in the same instant are serialised with
  `SELECT ... FOR UPDATE` on the admission row, which is the same row lock
  `patient-spec.yaml` already describes for the bed approval. No row-version column: Postgres'
  `xmin` is not mappable without EF trying to add it as a real column, and a hand-rolled
  version column would need a trigger to maintain.

**Revision 2.9** — the rest of the Patient Management tables landed (`Patient`,
`Admission`, `Appointment`, `BedAssignment`, `Discharge`, `DischargeChecklistItem`, in the
`Patient_AddAdmission` migration), and building them settled five places where this
document and `patient-spec.yaml` disagreed. Changes marked *(Rev 2.9)*. In every case the
spec won, per the rule in `CLAUDE.md`: on an entity Member 4 owns, the committed spec beats
this document.

- **`BedReservation` is gone; the hold lives on `BedAssignment`.** The spec publishes one
  bed row with `status ∈ {reserved, occupied, released}` and a `reserved_until`, and has no
  reservation schema and no reservation path at all. Two tables would have put the same
  fact — *this bed is claimed* — in two places, which is the drift this document rejects
  `Bed.Status` for. The concurrency guarantee is unchanged and still an index, now two
  partial unique indexes on one table. See
  [`BedAssignment`](#bedassignment-extends-auditedentity-rev-29--changed).
- **`Patient` carries `FullName`, not `FirstName` + `LastName`**, and gains `Address`.
  The spec publishes `full_name` and `address` everywhere, and `PatientDetailField` already
  listed `Address` for a column this document never had. `NationalId` is `Nic`, the name the
  spec uses.
- **`Discharge` has no `ReadinessStatus`.** Readiness is a query over the checklist rows —
  `all_mandatory_ticked` — so the enum and the rows could never disagree.
  `DischargeChecklistItemType` has the spec's five values, not three, and each row carries
  `IsMandatory`.
- **`Admission` drops `EmergencyCallId` and `AppointmentId`.** The spec carries Emergency's
  reference as `dispatch_id`, a string of theirs and not a foreign key, and the appointment
  link lives on `Appointment.AdmissionId` — one link, one owner. `DischargedAt` added,
  which the spec returns and this document had nowhere.
- **Cross-component references are foreign keys with no navigation property.** Every
  `*StaffMemberId` and the two `PatientAccount` links are configured with
  `HasOne<StaffMember>()` and no navigation. That is `integration_of_functions.md` §5.1
  enforced by the model rather than by discipline — there is no `Include()` to reach for.
  It also removes a real bug: `StaffMember` is soft-deletable and
  `Admission.CategorySetByStaffMemberId` is required, so a navigation would have dropped
  every admission a retired clinician ever categorised out of every query.

**Revision 2.8** — the first Patient Management code landed (`Ward`: the entity, its
configuration, the `Patient_AddWard` migration and `GET`/`POST /wards`), and building it
settled two names this document and `patient-spec.yaml` disagreed on. Changes marked
*(Rev 2.8)*. **`Ward`'s schema is now frozen** — `Shift.WardId`, `EquipmentItem.WardId` and
`Dispatch.DestinationWardId` all point at it.

- **`WardGenderPolicy` is named `GenderPolicy`.** The committed spec publishes it as
  `GenderPolicy`, and on an entity Member 4 owns the spec wins. Same values, same wire
  values, one name in three places instead of two.
- **`Ward.Type` is named `Ward.WardType`.** The JSON field was already `ward_type`; calling
  the property `Type` meant the column, the property and the wire name were three different
  words for one thing.
- **`HighDependency` is spelled `Hdu` in C#.** Not a schema change — the wire value was
  always `hdu`. The C# member name *is* the wire value under this codebase's snake-case
  converter, so a "readable" `HighDependency` silently serialized to `high_dependency` and
  broke the match the Rev 2.2 note was written to protect.

**Revision 2.7** — the first Common code landed (auth: login, registration, refresh,
logout, `/auth/me`), and building it surfaced one place where this document could not be
implemented as written. Changes marked *(Rev 2.7)*.

- **`RefreshToken` could only hold staff sessions.** Its `StaffMemberId` was non-null,
  which predates `PatientAccount` (Rev 2.5) — so the patient login the specs require had
  nowhere to store a session. It now carries a `PrincipalType` plus one nullable FK per
  identity, with a CHECK making exactly one of them required. See
  [`RefreshToken`](#refreshtoken-extends-entity-rev-27--changed).
- **`PrincipalRole` and `PrincipalType` are written down.** Both were already in
  `common-spec.yaml` and neither was in this document, so the JWT's `role` and `typ`
  claims had no entry in the schema of record.
- **`ux_patient_accounts_phone` added to the index list.** The `PatientAccount` entity
  note already stated it; the Constraints section did not carry it.

**Revision 2.6** — a second Patient Management agent, added on the lecturer's direction
during the topic-finalization meeting. His feedback: a component called "Patient
Management" whose only AI behaviour is picking a bed does not read as patient-facing.
He asked for a second agent where the patient describes something in their own words and
the agent reads their stored record and responds — with a doctor's approval sitting
between the agent's draft and anything the patient sees, the same human-gate pattern
already used everywhere else in this document.

- **`CareRecommendation` (new)** — see
  [`CareRecommendation`](#carerecommendation-extends-auditedentity-rev-26--new). Owned by
  Patient Management, alongside the existing entities.
- **`AgentType.PatientCareAdvisory` (new)** — the fifth agent value. The existing
  `PatientAdmissionBed` is untouched; this project now has two agents in one component
  rather than a replacement.
- **`ProposedChangeType.CreateCareRecommendation` (new)** — the write this agent's
  workflow proposes, following the same `AgentProposedChange` shape every other agent
  already uses. No new column was needed on that shared table — see the entity note for
  why.
- **What this agent is not.** It does not diagnose, prescribe or set `admission_category`.
  It drafts a decision-support note; a `Doctor`-role staff member approves, edits or
  rejects it before the patient ever sees a word of it. That is the same wall §1 of
  `patient-management-plan.md` already draws around the first agent, held in the same
  place for the second.

**Revision 2** — aligned with the Staff Management "cascading swap" flow and the Patient
Management admission/discharge flow. Changes in this revision are marked *(Rev 2)*.
Items still requiring a group decision are collected in [Open Decisions](#open-decisions).

**Revision 2.1** — review feedback on PR #1, marked *(Rev 2.1)*:

- **`AdmissionStatus` now carries all seven states** the admission flow uses, stored rather
  than computed, and named identically to `patient-spec.yaml`. Reasoning and the legal
  transition table are under [`AdmissionStatus`](#admissionstatus-rev-21--changed-again-review-item-2).
- **`Patient.TempReference`** gives unidentified arrivals a handle (`UNKNOWN-2026-0142`),
  with a CHECK guaranteeing every patient row carries at least one identifier.
- **`Admission.MissingFields` / `DetailsComplete`** record *which* details are still
  outstanding, for the "relative brings the ID later" case.
- Enum literals in SQL normalised to snake_case throughout, matching the wire values
  already published in `patient-spec.yaml`.

**Revision 2.2** — schema reconciled against the three committed component specs
(`patient-spec.yaml`, `equipment-spec.yaml`, `staff-spec.yaml`), marked *(Rev 2.2)*.
Rev 2.1 closed review items 2 and 4; this revision closes **item 3**, which was not
addressed, and fixes name and value mismatches found by comparing every entity, field and
enum in this document against those specs.

The rule applied throughout: **where this diagram and a component's own committed spec
disagree, the spec wins — for whichever member owns that entity.** That cuts in every
direction, including against Patient Management (see `Gender`).

- **`Bed` moved to Health Equipment (Member 3)** — review item 3. Three other documents had
  already settled this; Rev 2.1 was the only one placing it under Member 4.
- **`Bed` field names and `BedCondition`** aligned to `equipment-spec.yaml`. Occupancy and
  holds are no longer denormalized onto `Bed`, which had reintroduced a cross-write between
  two components.
- **`Ward.Type` is now `WardType`**, not `AdmissionCategory` — maternity, pediatric and
  isolation wards were unrepresentable.
- **`Admission.Urgency` / `IsInfectious` / `MissingFields`** — Rev 2 had invented second
  names for fields the committed spec already published.
- **`AdmissionCategory.HighDependency` serializes to `hdu`**, not `high_dependency`.
- **`StaffRole` aligned to `staff-spec.yaml`**, adding `Doctor` — without it, a published
  authorization rule in `patient-spec.yaml` could not be implemented.
- **`Gender.Unknown` kept**, and `patient-spec.yaml` corrected to match it.
- Three new items in [Open Decisions](#open-decisions) (9–11) for things that need an
  owner's call rather than a unilateral edit.

**Revision 2.5** — the patient login finally has a table, and the two columns
`patient-spec.yaml` publishes but the diagram never had:

- **`PatientAccount` (new)** — the app login for a patient, separate from the `Patient`
  medical record. Rev 2.3 decided patients keep an app login and Rev 2.4 wired a caller id
  to it, but no entity was ever added, so three committed specs referenced a table that did
  not exist. See [`PatientAccount`](#patientaccount-extends-softdeletableentity-rev-25--new)
  for why an account and a record must not be the same row.
- **`EmergencyCall.CallerUserId` repointed** from `Patient.Id` to `PatientAccount.Id`.
  Rev 2.4 had a bystander's login pointing at a medical record.
- **`Patient.UserAccountId` (new)** — the one optional link between the two, nullable and
  unique. Already listed in `patient-management-plan.md` §4; missing here.
- **`Admission.DispatchId` / `ReportedByUserId` (new)** — both already published by
  `patient-spec.yaml`'s `Admission` schema and required by the pre-admission flow.
  Member 4's own omission, found while checking Rev 2.4.
- **The `urgency` translation** between Emergency's `CallPriority` and Patient's
  `AdmissionUrgency` is written down, in `integration_of_functions.md` §22 and both specs.
  Nothing changes in this file — the two enums stay separate, as they should.

**Revision 2.4** — the Emergency component's entities gain the fields its newly-written
design needs (Member 1's own entities, added alongside `emergency-management-plan.md`
and `emergency-spec.yaml`):

- **`EmergencyCall.CallerUserId` / `PatientIsCaller`** — closes
  `integration_of_functions.md` Open Item 11.4. See
  [`EmergencyCall`](#emergencycall-extends-auditedentity).
- **`EmergencyCall.AddressLabel` / `Transported`** — a reverse-geocoded street name for
  the crew, and the fact that not every call ends in a hospital trip.
- **`Ambulance.OutOfServiceReason`** — "broken down" and "scheduled service" are
  different operational facts, and the fleet report separates them.
- **`Dispatch.SupersededByDispatchId`** — a self-reference set when a run is diverted.
  A diverted `Dispatch` keeps its own row and points at its replacement, so "who was
  sent first and why did that change" stays answerable. Overwriting the row instead
  would destroy the audit trail the diversion approval gate depends on
  (`emergency-management-plan.md` §5.2).

**Revision 2.3** — Open Decision 1 (patient identity) resolved: **patients keep an app
login.** Rev 2.1 recorded the Component Plan as having no Patient role; v2 of that plan
does list one, and both `patient-management-plan.md` and `patient-spec.yaml` are already
built on it. The deciding requirement is assignment §4.1, "meaningful and different
purposes for the React and Flutter applications". Knock-on work is listed under the
decision.

## Architecture Overview

```
Entity (abstract - Id, CreatedAt)
    ├── AuditedEntity (abstract - + UpdatedAt)
    │     └── SoftDeletableEntity (abstract - + IsActive, DeletedAt)
    │           └── Concrete Entities (soft-deletable, referenced by history)
    │     └── Concrete Entities (mutable, not soft-deletable)
    └── Concrete Entities (append-only / immutable)
```

---

## Base Classes

### Entity (abstract)
```
+ Id: Guid (PK, default: gen_random_uuid())
+ CreatedAt: DateTimeOffset (non-null)
```

### AuditedEntity extends Entity (abstract)
```
+ UpdatedAt: DateTimeOffset (non-null)
```
Used by any entity whose rows are mutated after insert (status transitions, field edits).

### SoftDeletableEntity extends AuditedEntity (abstract)
```
+ IsActive: bool = true (non-null)
+ DeletedAt: DateTimeOffset (nullable)
```
Used by entities that are FK targets of historical/child records — hard-deleting them
would either cascade-destroy history or be blocked by FK constraints. *(Decision 21)*

---

## Concrete Entities

### Identity & Auth

#### StaffMember extends SoftDeletableEntity
```
+ Email: string (unique, non-null)
+ PasswordHash: string (non-null)
+ FirstName: string (non-null)
+ LastName: string (non-null)
+ PhoneNumber: string (nullable)
+ Department: string (nullable)
+ Role: StaffRole (non-null)
```
**Table:** `staff_members`
**Note:** Unified identity model — every operator role (field and admin) is a
`StaffMember` row. `Patient` is intentionally excluded and has no auth identity.
*(Decisions 2, 26)*

#### Skill extends AuditedEntity
```
+ Name: string (unique, non-null)
```
**Table:** `skills`
**Note:** Admin-editable lookup table, not a hardcoded enum. *(Decision 6, 27)*

#### StaffMemberSkill extends Entity
```
+ StaffMemberId: Guid (non-null) FK → StaffMember.Id
+ SkillId: Guid (non-null) FK → Skill.Id
+ ValidFrom: DateOnly (nullable)                       -- (Rev 2)
+ ExpiresAt: DateOnly (nullable)                       -- (Rev 2)
```
**Table:** `staff_member_skills`
**Constraint:** UNIQUE(StaffMemberId, SkillId)
**Relationships:** N StaffMember ↔ N Skill (via StaffMemberSkill) — structured so the
Staff Allocation Agent can match staff skills to ward needs programmatically. *(Decision 27)*
**Note:** *(Rev 2)* `ValidFrom`/`ExpiresAt` support the staff flow's requirement that the
validator confirm a nurse **actively** holds a skill — clinical certifications lapse.
Both nullable means "held indefinitely"; a skill is active when
`ValidFrom IS NULL OR ValidFrom <= today` and `ExpiresAt IS NULL OR ExpiresAt >= today`.

#### RefreshToken extends Entity *(Rev 2.7 — changed)*
```
+ PrincipalType: PrincipalType (non-null)              -- (Rev 2.7) which table below is set
+ StaffMemberId: Guid (nullable) FK → StaffMember.Id           -- (Rev 2.7: was non-null)
+ PatientAccountId: Guid (nullable) FK → PatientAccount.Id     -- (Rev 2.7 — new)
+ TokenHash: string (unique, non-null)
+ ExpiresAt: DateTimeOffset (non-null)
+ RevokedAt: DateTimeOffset (nullable)
+ RevokedReason: string (nullable)                     -- (Rev 2.7 — new)
```
**Table:** `refresh_tokens`
**Owner:** Common.
**Constraints:**
- `UNIQUE (token_hash)`
- `CHECK (principal_type IN ('staff', 'patient'))`
- `CHECK ((principal_type = 'staff' AND staff_member_id IS NOT NULL AND patient_account_id IS NULL)
   OR (principal_type = 'patient' AND patient_account_id IS NOT NULL AND staff_member_id IS NULL))`

**Note:** Persisted so sessions can be revoked server-side (compromised device,
terminated staff) rather than relying on stateless JWT expiry alone. *(Decision 36)*
`RevokedAt` is the one post-insert mutation; the row is otherwise append-only, so it
stays on `Entity` rather than gaining an `UpdatedAt` that would duplicate `RevokedAt`.

*(Rev 2.7)* **This table could only hold staff sessions, and there are two identities.**
The non-null `StaffMemberId` predates `PatientAccount` (Rev 2.5). `common-spec.yaml`
already publishes `POST /auth/refresh` as working "for both identities — `RefreshToken`
rows carry the principal", so the committed spec is what the code was built to.

Two nullable foreign keys with a CHECK, rather than one bare `PrincipalId: Guid`, because
a plain Guid pointing at "one of two tables" is a foreign key the database cannot enforce
— nothing would stop a row naming an account that was hard-deleted, or an id from neither
table. The CHECK is what makes the pair behave as one required field: exactly one is set,
and it is the one `principal_type` names.

`RevokedReason` (`rotated`, `logout`, `reuse_detected`) is not decoration. Refresh tokens
are single-use and rotating, so rows are revoked constantly in normal operation; without
a reason column there is no way to tell an ordinary rotation from the one revocation that
means a token was stolen.

#### DeviceToken extends AuditedEntity *(Rev 2 — new)*
```
+ StaffMemberId: Guid (non-null) FK → StaffMember.Id
+ Token: string (unique, non-null)                     -- FCM/APNs registration token
+ Platform: DevicePlatform (non-null)
+ LastSeenAt: DateTimeOffset (non-null)
+ RevokedAt: DateTimeOffset (nullable)
```
**Table:** `device_tokens`
**Note:** *(Rev 2)* Required by both flows — the staff flow pushes "report to the ICU" to
the reassigned nurse's Flutter app, and the patient flow pushes the bed assignment.
One staff member may have several devices. Scoped to `StaffMember` only; see
[Open Decisions](#open-decisions) for the patient-device question.

#### PatientAccount extends SoftDeletableEntity *(Rev 2.5 — new)*
```
+ PhoneNumber: string (unique, non-null)               -- the login identifier
+ PasswordHash: string (non-null)
+ FullName: string (non-null)
+ LastLoginAt: DateTimeOffset (nullable)
```
**Table:** `patient_accounts`
**Owner:** Patient Management (Member 4).
**Constraints:**
- `UNIQUE (phone_number) WHERE is_active`

*(Rev 2.5)* **This table was missing, and three committed specs were already pointing at
it.** Rev 2.3 resolved Open Decision 1 in favour of patients keeping an app login, but no
entity was ever added for that login — so `patient-spec.yaml`'s
`POST /patients/{id}/link-account` (`user_account_id`), its
`Admission.reported_by_user_id`, and Rev 2.4's `EmergencyCall.CallerUserId` were each
referencing a table that did not exist. Rev 2.4 filled the gap by pointing
`CallerUserId` at `Patient.Id`, which is the one thing it must not be — see below.

**An account is not a record, and conflating them breaks the emergency path.** A
`Patient` row is a *medical record*, created by staff, and it exists whether or not that
person ever installs the app — most do not, and an unconscious arrival certainly has not.
A `PatientAccount` is a *login*, created by a person signing up, and it exists whether or
not they have ever been treated here. `patient-spec.yaml` already says this in as many
words: *"A patient RECORD and a patient ACCOUNT are different things."*

The case that forces the split is the ordinary one for this system: **a bystander rings in
for a stranger on the road.** That caller has an account and is not the patient
(`PatientIsCaller = false`); the patient has a record and no account. If `CallerUserId`
were an FK to `Patient.Id`, storing the caller would mean fabricating a medical record for
a healthy passer-by, and it would then be indistinguishable from the record of the person
actually bleeding. Two tables, one FK each, and that whole class of mix-up cannot occur.

**Link, don't merge.** `Patient.UserAccountId` is the single optional join between them,
nullable on the record side and unique — one account, one record, and neither requires the
other. Staff make that link deliberately through `link-account` after checking identity;
it is never inferred from a matching phone number, because two people share a phone far
more often than a hospital would like.

---

### Emergency / Ambulance

#### EmergencyCall extends AuditedEntity
```
+ PatientId: Guid (nullable) FK → Patient.Id
+ CallerUserId: Guid (nullable) FK → PatientAccount.Id      -- (Rev 2.5: was Patient.Id)
+ PatientIsCaller: bool (non-null)                          -- (Rev 2.4 — new)
+ CallerName: string (nullable)
+ CallerPhone: string (nullable)
+ Latitude: decimal(9,6) (non-null)
+ Longitude: decimal(9,6) (non-null)
+ AddressLabel: string (nullable)                           -- (Rev 2.4 — new)
+ Details: string (nullable)
+ Priority: CallPriority (non-null)
+ Status: CallStatus (non-null)
+ Outcome: string (nullable)
+ Transported: bool (nullable)                              -- (Rev 2.4 — new)
```
**Table:** `emergency_calls`
**Note:** `PatientId` nullable — identity is often unknown at the scene, settable
later once intake matches the call to a registered `Patient`. *(Decision 25)*

*(Rev 2.4)* **`CallerUserId` / `PatientIsCaller` close Open Item 11.4 in
`integration_of_functions.md`.** The caller is often not the patient — someone rings
in for a stranger on the road — and that is asked once, on the call screen, rather
than guessed later. `CallerUserId` is the logged-in app user who placed the call
(nullable — an anonymous caller, or one phoning the hotline, has none);
`PatientIsCaller` is their answer.

*(Rev 2.5 correction)* Rev 2.4 wrote this FK as `→ Patient.Id`. It is
`→ PatientAccount.Id` — a caller is a **login**, not a medical record, and the whole
point of `PatientIsCaller = false` is that the two are different people. See
[`PatientAccount`](#patientaccount-extends-softdeletableentity-rev-25--new).
`PatientId` is set when `PatientIsCaller = true`; when `false`, Patient Management
creates a new record from `CallerName`/provisional details and records
`CallerUserId` as that patient's emergency contact and `reported_by_user_id`,
matching the field `patient-spec.yaml`'s `Admission.reported_by_user_id` already
publishes. Both fields are carried unchanged onto the dispatch notification
(`emergency-spec.yaml`'s `DispatchNotification`, `integration_of_functions.md` §4.2,
§22) so Patient Management never has to re-derive them.

#### Ambulance extends SoftDeletableEntity
```
+ RegistrationNumber: string (unique, non-null)
+ CurrentLatitude: decimal(9,6) (nullable)
+ CurrentLongitude: decimal(9,6) (nullable)
+ Status: AmbulanceStatus (non-null)
+ OutOfServiceReason: string (nullable)                     -- (Rev 2.4 — new)
```
**Table:** `ambulances`
**Note:** Current-position fields only — no time-series location history.
Onboard equipment is explicitly not tracked (equipment stays ward-scoped only).
*(Decisions 9, 14)*

#### Dispatch extends AuditedEntity
```
+ EmergencyCallId: Guid (non-null) FK → EmergencyCall.Id
+ AmbulanceId: Guid (non-null) FK → Ambulance.Id
+ DestinationWardId: Guid (nullable) FK → Ward.Id       -- (Rev 2: was non-null)
+ Status: DispatchStatus (non-null)
+ SupersededByDispatchId: Guid (nullable) FK → Dispatch.Id  -- (Rev 2.4 — new)
+ DispatchedAt: DateTimeOffset (non-null)
+ CompletedAt: DateTimeOffset (nullable)
```
**Table:** `dispatches`
**Note:** Row is created either instantly (nearest-ambulance fast path) or after
Duty Manager approval (reassignment case) — both paths are recorded uniformly via
`AgentWorkflow` beforehand. *(Decisions 18, 20)*
*(Rev 2)* `DestinationWardId` is now nullable: the fast path creates the `Dispatch`
immediately, but the destination ward is only settled once the Patient Admission & Bed
Agent runs, which is a later step in the orchestration sequence. Non-null would have
forced a decision that has not been made yet.

#### DispatchCrew extends Entity
```
+ DispatchId: Guid (non-null) FK → Dispatch.Id
+ StaffMemberId: Guid (non-null) FK → StaffMember.Id
```
**Table:** `dispatch_crew`
**Constraint:** UNIQUE(DispatchId, StaffMemberId)
**Note:** Crew assigned directly on `Dispatch`, not through `Allocation` — ambulance
duty is real-time, not part of the ward-based shift roster. *(Decision 30)*

#### RouteLog extends AuditedEntity
```
+ DispatchId: Guid (unique, non-null) FK → Dispatch.Id
+ OriginLatitude: decimal(9,6) (non-null)
+ OriginLongitude: decimal(9,6) (non-null)
+ DestinationLatitude: decimal(9,6) (non-null)
+ DestinationLongitude: decimal(9,6) (non-null)
+ PlannedDistanceKm: decimal(7,2) (non-null)
+ PlannedDurationMinutes: int (non-null)
+ DepartedAt: DateTimeOffset (nullable)
+ ArrivedAt: DateTimeOffset (nullable)
+ MapsApiReference: string (nullable)
```
**Table:** `route_logs`
**Note:** Single summary row per `Dispatch` (1:1) — not a GPS waypoint trail.
*(Decision 23)*

---

### Staff Management

#### WardStaffingRule extends AuditedEntity *(Rev 2 — new)*
```
+ WardId: Guid (non-null) FK → Ward.Id
+ RequiredRole: StaffRole (non-null)
+ RequiredSkillId: Guid (nullable) FK → Skill.Id
+ MinimumHeadcount: int (non-null)
```
**Table:** `ward_staffing_rules`
**Constraint:** UNIQUE(WardId, RequiredRole, RequiredSkillId)
**Note:** *(Rev 2)* The staff flow's deterministic validator must confirm that removing a
nurse "will not drop the General Ward below its own legal minimum staffing level".
Before Rev 2 the schema had no such floor — only `Shift.HeadcountNeeded`, which is a
staffing *target*, not an enforced minimum, and is per-slot rather than per-ward.
This table is the ward-level policy source of truth; `Shift.MinimumHeadcount` is
denormalized from it when a shift is created, so the validator can check a single slot
without recomputing policy.

#### Shift extends AuditedEntity
```
+ WardId: Guid (non-null) FK → Ward.Id
+ Date: DateOnly (non-null)
+ StartTime: TimeOnly (non-null)
+ EndTime: TimeOnly (non-null)
+ RequiredRole: StaffRole (non-null)
+ RequiredSkillId: Guid (nullable) FK → Skill.Id        -- (Rev 2)
+ HeadcountNeeded: int (non-null)
+ MinimumHeadcount: int (non-null)                      -- (Rev 2)
```
**Table:** `shifts`
**Note:** `Shift` is the slot/template; `Allocation` assigns staff to it. *(Decision 15)*
*(Rev 2)* `RequiredSkillId` is the matching input the Staff Allocation Agent needs — before
Rev 2 nothing in the schema recorded that an ICU shift requires the ICU skill, so
skill-matching had no data behind it. `MinimumHeadcount` is the safe floor the validator
enforces, distinct from `HeadcountNeeded` (the target).
**Night shifts:** when `EndTime < StartTime` the shift ends on `Date + 1 day`. Every
overlap and coverage query must apply this rule — see [Open Decisions](#open-decisions).

#### Allocation extends AuditedEntity                    -- (Rev 2: was Entity)
```
+ ShiftId: Guid (non-null) FK → Shift.Id
+ StaffMemberId: Guid (non-null) FK → StaffMember.Id
+ Status: AllocationStatus (non-null)                   -- (Rev 2)
+ EndedAt: DateTimeOffset (nullable)                    -- (Rev 2)
+ EndedReason: string (nullable)                        -- (Rev 2)
+ ReplacedByAllocationId: Guid (nullable) FK → Allocation.Id   -- (Rev 2)
```
**Table:** `allocations`
**Constraint:** UNIQUE(ShiftId, StaffMemberId) **WHERE Status = 'Confirmed'** *(Rev 2)*
**Relationships:** N StaffMember ↔ N Shift (via Allocation)
**Note:** Row only exists once the Staff Allocation Agent's roster proposal is
approved — see `AgentWorkflow`. *(Decisions 15, 18)*
*(Rev 2)* The cascading-swap flow requires "the old `Allocation` is **ended**, a new
`Allocation` is created". A bare join row could only be *deleted*, which destroys the
history the same flow's audit step depends on, and the unconditional UNIQUE would then
block ever re-allocating that nurse to that shift. `Status` + `EndedAt` +
`ReplacedByAllocationId` make the swap traceable end to end, and the UNIQUE is scoped to
`Confirmed` so superseded rows can coexist.

#### LeaveRequest extends AuditedEntity
```
+ StaffMemberId: Guid (non-null) FK → StaffMember.Id
+ Type: LeaveRequestType (non-null)
+ StartDate: DateOnly (non-null)
+ EndDate: DateOnly (non-null)
+ Reason: string (nullable)
+ Status: LeaveRequestStatus (non-null)
+ ReviewedByStaffMemberId: Guid (nullable) FK → StaffMember.Id
+ SwapWithStaffMemberId: Guid (nullable) FK → StaffMember.Id
+ SwapShiftId: Guid (nullable) FK → Shift.Id
```
**Table:** `leave_requests`
**Note:** Plain direct-approval table, not agent-driven — none of the 4 AI agents
covers leave/swap requests. `Type` discriminator folds shift-swap requests into the
same table rather than a parallel entity. `SwapWith*`/`SwapShift*` only apply when
`Type = ShiftSwap`. *(Decisions 16, 29)*
*(Rev 2)* `LeaveRequestType` gained `Sick` and `Emergency`, and the vague `Leave` value was
renamed `Annual`. The cascading-swap flow is triggered by an urgent `Sick` request for a
shift starting in two hours, which the previous two-value enum could not express.

---

### Health Equipment

> **Reconciled with `equipment-spec.yaml` and the shipped code.** *(Rev 3 — Member 3,
> 2026-09-11. Closes Open Decision 12.)*
>
> The five entities here used to describe a database nobody was going to build. What
> changed, and why:
>
> | Was | Now | Why |
> | :--- | :--- | :--- |
> | `EquipmentType` | **`EquipmentCategory`** | Renamed to the name the spec publishes. Same table, same job. |
> | `StockLevel` | **`PharmacyItem`** | Absorbed. See the answer below. |
> | — | **`PharmacyCategory`**, **`PharmacyTransaction`**, **`ActionRequest`** | Published by the spec, modelled nowhere until now. |
>
> **The two questions this section was flagged on, answered:**
>
> **(1) `StockLevel` does not survive.** `PharmacyItem` replaced it outright. The old model
> split consumables from durable assets and counted them per ward. The built model keeps one
> catalog row per medicine or supply with its quantity on it, and every change to that
> quantity is a `PharmacyTransaction`. Keeping `StockLevel` as well would mean two tables
> answering "how many do we have", which is exactly the drift the transaction log exists to
> prevent.
>
> **(2) Pharmacy stock is central, not per-ward.** One quantity per item for the whole
> hospital, so there is no `WardId` on `PharmacyItem`. This is a deliberate simplification
> recorded in `equipment-management-plan.md` §15, and §16 open question 1 is the group's
> decision to revisit it. Equipment, unlike pharmacy, *is* located: `EquipmentItem.WardId`
> says where a machine sits.
>
> `Bed` is owned by this component but defined under Patient Management below, next to the
> `BedAssignment` it is read with. That split is settled in `integration_of_functions.md`
> §6.1: we own the frame, they own the occupant.
>
> **Not yet built:** `ActionRequest` is modelled here from the spec but has no table or
> endpoints yet. Everything else in this section is live on `main`.

#### EquipmentCategory extends SoftDeletableEntity *(Rev 3 — renamed from `EquipmentType`)*
```
+ Name: string (non-null, max 150)
```
**Table:** `equipment_categories`
**Constraint:** UNIQUE(Name) **WHERE is_active**
**Note:** A table rather than an enum, so a sixth category is a row and not a migration.
Seeded with the five from `equipment-management-plan.md` §1.1. The unique index is scoped
to live rows for the reason every other one in this schema is: retiring a category must not
make its name unusable forever, and the global query filter would hide the clashing row from
the service-layer duplicate check. *(Decision 33, revised)*

#### EquipmentItem extends SoftDeletableEntity *(Rev 3 — fields aligned to the spec)*
```
+ Name: string (non-null, max 150)
+ CategoryId: Guid (non-null) FK → EquipmentCategory.Id
+ Model: string (non-null, max 100)
+ Manufacturer: string (non-null, max 150)
+ PurchaseDate: DateOnly (non-null)
+ Status: EquipmentStatus (non-null)
+ WardId: Guid (nullable) -- Patient Management's ward; null means the central store
+ AssignedToAdmissionId: Guid (nullable) -- set while Status = assigned
+ AssetTag: string (non-null, max 50)
+ SerialNumber: string (nullable, max 100)
+ NextMaintenanceDue: DateOnly (nullable)
```
**Table:** `equipment_items`
**Constraints:** UNIQUE(AssetTag) **WHERE is_active** · UNIQUE(SerialNumber) **WHERE
is_active AND serial_number IS NOT NULL** · CHECK(status IN the enum)
**Indexes:** `(CategoryId)` · `(WardId)` · `(Status)` · `(NextMaintenanceDue) WHERE status
<> 'retired'`
**Note:** Durable, individually tracked assets. `WardId` and `AssignedToAdmissionId` are
bare references into Patient Management's tables — no foreign key, because we never write
them. Asset tags are meant to be unique across beds and equipment together; Postgres cannot
index across two tables, so that half is enforced in application code and is a **known gap**
tracked as issue #17. *(Decision 8)*

#### Bed extends SoftDeletableEntity — **owned here, defined under Patient Management**
See the `Bed` entry in the Patient Management section. Listed here so this section is a
complete inventory of what Member 3 owns.

#### PharmacyCategory extends SoftDeletableEntity *(Rev 3 — new)*
```
+ Name: string (non-null, max 150)
+ RequiresPrescription: bool (non-null)
```
**Table:** `pharmacy_categories`
**Constraint:** UNIQUE(Name) **WHERE is_active**
**Note:** The five from `equipment-management-plan.md` §1.2, seeded by
`docs/seed/002_pharmacy_categories.sql`. `RequiresPrescription` records the rule; deciding
what a patient should actually be given is clinical staff's call and never this component's.

#### PharmacyItem extends SoftDeletableEntity *(Rev 3 — new; replaces `StockLevel`)*
```
+ Name: string (non-null, max 200)
+ CategoryId: Guid (non-null) FK → PharmacyCategory.Id
+ Manufacturer: string (nullable, max 150)
+ BatchNumber: string (nullable, max 50)
+ ExpiryDate: DateOnly (nullable)
+ Unit: string (non-null, max 20) -- tablet, bottle, box
+ QuantityOnHand: int (non-null)
+ ReorderThreshold: int (non-null)
+ UnitPrice: decimal(12,2) (nullable)
```
**Table:** `pharmacy_items`
**Constraints:** UNIQUE(Name) **WHERE is_active** · CHECK(quantity_on_hand >= 0) ·
CHECK(reorder_threshold >= 0)
**Indexes:** `(CategoryId)` · `(ExpiryDate) WHERE expiry_date IS NOT NULL`
**Note:** One catalog row per medicine or supply, central to the hospital. **No
`IsAvailable` column**: availability is `quantity_on_hand > 0`, computed when the row is
read, for the same reason occupancy is not a column on `Bed` — one source of truth, nothing
to drift. `QuantityOnHand` is never written directly by a caller; every change is a
`PharmacyTransaction` applied as one conditional `UPDATE ... WHERE quantity_on_hand >= :qty`,
so it cannot go negative and two people dispensing at once cannot both succeed past zero.
The check constraint is the last line of defence under that. The expiry index is filtered
because the expiry sweep never asks about bandages.

#### PharmacyTransaction extends Entity *(Rev 3 — new)*
```
+ PharmacyItemId: Guid (non-null) FK → PharmacyItem.Id
+ Type: PharmacyTransactionType (non-null)
+ Quantity: int (non-null) -- always positive; Type gives the sign
+ PerformedByStaffId: Guid (non-null) -- Staff Management's row, id only
+ Note: string (nullable, max 300)
```
**Table:** `pharmacy_transactions`
**Constraints:** CHECK(quantity > 0) · CHECK(type IN the enum)
**Index:** `(PharmacyItemId, CreatedAt DESC)` — serves the history endpoint and the usage
rate the low-stock warning needs
**Note:** `Entity`, not `AuditedEntity`, and **no soft delete**: this is the audit trail, so
there is no `UpdatedAt` because nothing updates and no `IsActive` because nothing is
withdrawn. It also carries **no navigation back to `PharmacyItem`** in the EF model.
`PharmacyItem` has a global query filter, and a required navigation into a filtered entity
makes an `Include` silently drop history rows once an item is retired — the trail has to
outlive the thing it describes. An `adjusted` row requires a `Note`; a stocktake correction
nobody explained cannot be audited later.

#### MaintenanceSchedule extends AuditedEntity *(Rev 3 — now polymorphic)*
```
+ AssetType: AssetType (non-null) -- equipment_item | bed
+ AssetId: Guid (non-null) -- polymorphic target id
+ ScheduleType: MaintenanceType (non-null)
+ ScheduledDate: DateOnly (non-null)
+ Status: MaintenanceStatus (non-null)
+ PerformedByStaffId: Guid (nullable)
+ CompletedAt: DateTimeOffset (nullable)
+ Notes: string (nullable)
+ CreatedBy: RaisedBy (non-null) -- agent | user
```
**Table:** `maintenance_schedules`
**Indexes:** `(AssetType, AssetId)` · `(ScheduledDate) WHERE status IN ('scheduled','overdue')`
**Note:** *(Rev 3)* No longer an `EquipmentItemId` foreign key. Beds and equipment share one
scheduling flow rather than two, so the reference is polymorphic and unconstrained — which is
the cost of that choice, stated rather than hidden. **`Status` never holds `overdue` in the
database**: it is derived on read from `scheduled_date < today AND status = 'scheduled'`,
so there is nothing cached to drift. One row per service event, so it doubles as service
history. `CreatedBy` records whether the sweep or a person scheduled it. *(Decision 24,
revised)*

#### Warning extends AuditedEntity *(Rev 3 — fields aligned to the spec)*
```
+ Type: WarningType (non-null)
+ Severity: WarningSeverity (non-null)
+ RelatedEntityType: RelatedEntityType (non-null) -- pharmacy_item | equipment_item | bed
+ RelatedEntityId: Guid (non-null) -- polymorphic target id
+ WardId: Guid (nullable)
+ RecommendedAction: string (non-null)
+ Status: WarningStatus (non-null)
+ RaisedBy: RaisedBy (non-null) -- agent | user
+ WorkflowId: Guid (nullable) FK → AgentWorkflow.Id
+ AcknowledgedByStaffId: Guid (nullable)
+ AcknowledgedAt: DateTimeOffset (nullable)
+ ResolvedAt: DateTimeOffset (nullable)
```
**Table:** `warnings`
**Index:** `(Status)` — every dashboard filters on open warnings
**Note:** *(Rev 3)* The polymorphic target is a typed enum now, not a free string, and it
gained `pharmacy_item`. `RecommendedAction` is a short human sentence, never the model's raw
reasoning. `RaisedBy` distinguishes the threshold sweep and a person reporting a fault from
anything the agent infers; a reported fault starts at `high` severity because a person
saying the machine is broken outranks what a sweep guesses.
*(Rev 3)* The Rev 2 partial UNIQUE on `(EntityType, EntityId, Type) WHERE Status = 'Open'`
is **not** in the shipped schema. It was written for a sweep that inserts on every tick, and
that sweep does not exist yet. It has to come back with it, or the first run will duplicate
every open warning. Tracked in `docs/build/equipment.md` step 7.

#### ActionRequest extends AuditedEntity *(Rev 3 — new; **not built yet**)*
```
+ WarningId: Guid (nullable) FK → Warning.Id
+ ActionType: ActionType (non-null)
+ Details: jsonb (non-null) -- shape depends on ActionType
+ EstimatedCost: decimal (nullable)
+ Urgency: Urgency (non-null)
+ ProposedBy: RaisedBy (non-null)
+ WorkflowId: Guid (nullable) FK → AgentWorkflow.Id
+ RequiresApproval: bool (non-null)
+ AutoApproved: bool (non-null)
+ Status: ActionRequestStatus (non-null)
+ ApprovedByStaffId: Guid (nullable)
+ ApprovedAt: DateTimeOffset (nullable)
+ RejectionReason: string (nullable)
+ ExecutedAt: DateTimeOffset (nullable)
```
**Table:** `action_requests`
**Index:** `(Status)` — the approvals queue
**Note:** The human-approval gate, and the screen `equipment-management-plan.md` calls the
demo. The agent proposes rows here and never writes anything else; a person with the right
role decides whether the action happens. **`RequiresApproval` is computed once at creation
by a deterministic threshold rule, never by the model** — that is what stops a persuasive
model approving its own spending. `Details` is `jsonb` because its shape follows
`ActionType`, and each shape is documented in `equipment-spec.yaml`. Approving above the
threshold flips `Status` and executes the action **in one transaction**, so a half-approved
request cannot exist. Before any action touching a `Bed`, the server asks Patient Management
whether it is occupied, in the same request, before committing — maintenance never evicts a
patient.

---

### Patient Management

#### Ward extends SoftDeletableEntity
```
+ Name: string (unique, non-null)
+ WardType: WardType (non-null)                         -- (Rev 2.2: was AdmissionCategory; Rev 2.8: was Type)
+ GenderPolicy: GenderPolicy (non-null)                 -- (Rev 2; Rev 2.8: enum was WardGenderPolicy)
```
**Table:** `wards` — **built.** `Patient_AddWard`, `api/Data/Entities/Patient/Ward.cs`.
**Note:** `Type` lets the Patient Admission & Bed Agent filter candidate beds by matching
ward type to the patient's category. *(Decision 31)*

*(Rev 2.2)* **`Type` is its own enum, not `AdmissionCategory`.** Reusing `AdmissionCategory`
made maternity, pediatric and isolation wards unrepresentable — a hospital has those wards,
and `patient-management-plan.md` hard rules H3 (gender policy) and H4 (isolation) are
written against them. `patient-spec.yaml` already publishes a separate `WardType`; this now
matches it. The two enums overlap on `icu` and `hdu`, which is what the downgrade ladder
walks; they are not the same list and should not share a type.
*(Rev 2)* `GenderPolicy` implements the patient flow's "throws out every bed in the wrong
gender ward" filter — before Rev 2 the schema had nothing on `Ward` to filter against.

#### Bed extends SoftDeletableEntity — **owned by Health Equipment (Member 3)** *(Rev 2.2)*
```
+ WardId: Guid (non-null) FK → Ward.Id
+ BedNumber: string (non-null)                          -- (Rev 2.2: was Label)
+ Condition: BedCondition (non-null)                    -- (Rev 2.2: was Status: BedStatus)
+ HasIsolation: bool = false (non-null)                 -- (Rev 2.2: was IsIsolationCapable)
+ NurseStationDistance: int (non-null)                  -- (Rev 2.2: was ProximityRank)
+ AssetTag: string (nullable)                           -- (Rev 2.2)
```
**Table:** `beds`
**Constraint:** UNIQUE(WardId, BedNumber) WHERE IsActive
**Note:** *(Rev 2)* `HasIsolation` is the bed side of the "no isolation for
infectious patients" filter (the patient side is `Admission.IsInfectious`).
`NurseStationDistance` is the bed side of "sicker patient goes closer to the nurses'
station" (the patient side is `Admission.Urgency`); 1 = closest, unique within a ward so
the agent's ranking is deterministic. Both filters were unimplementable before Rev 2.

*(Rev 2.2 — review item 3)* **Field names and `Condition` aligned to the owner's spec.**
`equipment-spec.yaml` and `patient-spec.yaml` already publish this table with identical
field names and an identical `BedCondition` enum; Rev 2.1 was the only document using
`Label` / `IsIsolationCapable` / `ProximityRank` / `BedStatus`. Renamed to match, and
`AssetTag` added because Equipment prints it as the QR code their technician scans
(`equipment-management-plan.md` §3.1).

**Occupancy is deliberately not a column here.** Rev 2.1's `BedStatus` carried `Occupied`
and `Reserved`, denormalized from `BedAssignment`. That reintroduces exactly the cross-write
`integration_of_functions.md` §6.1 was written to prevent: Equipment owns this row, but only
Patient Management knows who is in the bed, so Patient would be writing Equipment's table.
Occupancy and holds are both `BedAssignment` rows: `Status = Occupied` is someone in the
bed, `Status = Reserved` is the 30-minute hold *(Rev 2.9)*.

```
"Is bed 12 free?"
    = it exists in Equipment's register        (M3's data, M4 reads)
    AND condition = 'usable'                   (M3's data, M4 reads)
    AND no BedAssignment references it with
        status IN ('reserved', 'occupied')      (M4's data)
```

Two reads, zero shared writes. `Cleaning` had no home in either published spec and is
carried to Open Decisions as item 9 rather than dropped silently.

#### Patient extends SoftDeletableEntity
```
+ PatientCode: string(8) (non-null, unique)              -- (Rev 2.11 - new)
+ FullName: string (non-null)                           -- (Rev 2.9: was FirstName + LastName)
+ DateOfBirth: DateOnly (nullable)
+ Nic: string (nullable, unique when present)           -- (Rev 2.9: was NationalId)
+ TempReference: string (nullable, unique when present)  -- (Rev 2.1)
+ Gender: Gender (non-null)                             -- (Rev 2: was string; Rev 2.9: now required)
+ Phone: string (nullable)                              -- (Rev 2.9: was PhoneNumber)
+ Address: string (nullable)                            -- (Rev 2.9 — new)
+ EmergencyContactName: string (nullable)
+ EmergencyContactPhone: string (nullable)
+ UserAccountId: Guid (nullable, unique when present) FK → PatientAccount.Id  -- (Rev 2.5 — new)
```
**Table:** `patients` — **built.** `Patient_AddAdmission`, `api/Data/Entities/Patient/Patient.cs`.
**Constraints:**
- `UNIQUE (patient_code)` — **not** scoped to `is_active`; see below *(Rev 2.11)*
- `UNIQUE (nic) WHERE nic IS NOT NULL AND is_active`
- `UNIQUE (temp_reference) WHERE temp_reference IS NOT NULL AND is_active` *(Rev 2.1)*
- `UNIQUE (user_account_id) WHERE user_account_id IS NOT NULL AND is_active` *(Rev 2.5)*
- `CHECK (nic IS NOT NULL OR phone IS NOT NULL OR temp_reference IS NOT NULL)` *(Rev 2.1)*

*(Rev 2.9)* **One `FullName`, and the three unique indexes are scoped `AND is_active`.**
The spec publishes `full_name` in every request and response, and splitting it in the
database only to join it back on every read buys nothing — Sri Lankan names do not reliably
divide into two parts anyway. `Address` was already in `PatientDetailField` as something a
relative can bring later, with no column to put it in. Scoping the unique indexes to active
rows is the same rule `Ward` follows: deactivate a duplicate record and its NIC has to
become usable again, or the merge you just did can never be redone.

**Note:** `NationalId` nullable — unconscious/unidentified emergency admissions may
lack one at intake — but unique whenever present, to dedupe registered patients.
*(Decision 35)*
*(Rev 2)* `Gender` promoted from free text to an enum: the gender-ward filter is a
deterministic rule run by C# validation code, and a deterministic filter cannot run
reliably on free text.

*(Rev 2.1 — review item 4)* **`TempReference` makes an unidentified patient identifiable.**
Before this, a patient with no NIC had nothing distinguishing them at all: three unconscious
arrivals in one evening were three rows differing only by `Id`, and staff had no handle to
say *which* one they meant. `TempReference` is server-generated at intake when no NIC and no
phone number is available — format `UNKNOWN-{yyyy}-{sequence}`, e.g. `UNKNOWN-2026-0142` —
and matches the value `patient-spec.yaml` already returns.

The CHECK is the real guarantee: **every patient row carries at least one identifier.**
The reference is never cleared once a NIC arrives later, so the paper trail, wristband and
verbal handover from the unidentified period still resolve to the right person.

*(Rev 2.11 — new)* **`PatientCode` is the handle a human uses.** Eight characters, `P` then
seven, e.g. `P7K2X9QM`. Server-generated once at registration and never changed afterwards.

The problem it solves: Equipment's assign screen asks the nurse which patient a drip stand is
going to, and the only answer this component had was a Guid. Nobody reads
`3f9c1a2e-8b44-4f31-9a7d-2c05e6b7d813` off a wristband, and nobody types it correctly into
another screen. So a patient now has two identifiers, doing two different jobs:

| | `Id` | `PatientCode` |
| :--- | :--- | :--- |
| Shape | Guid | eight characters |
| Used by | every stored reference, every FK, every URL | people, out loud and on paper |
| Generated | `Guid.NewGuid()` | random, from a 31-character alphabet |

**Nothing stores the code as a reference.** A foreign key is still `Id`. The code is what a
human carries between screens; the screen turns it back into an `Id` by searching for it, and
`GET /patients?search=` matches it alongside name, NIC, phone and temp reference.

**The alphabet has no `0`/`O` and no `1`/`I`/`L`** — 31 characters, so seven of them are about
27 billion codes. Random rather than sequential: a running number publishes how many patients
the hospital has ever registered, and two desks registering at the same moment would have to
agree on who gets the next one.

**Its unique index is the one on this table not scoped `WHERE is_active`,** against the
repo-wide rule. That rule exists because a *human* re-enters an identifier after a merge or a
deactivation — deactivate ward `ICU-1` and somebody has to be able to create `ICU-1` again.
Nobody ever types a patient code in to create one; the server picks it. Reusing a deactivated
record's code would make one wristband resolve to two different people, so the database is told
to refuse it outright rather than relying on the generator to remember.

#### Appointment extends AuditedEntity *(Rev 2 — new)*
```
+ PatientId: Guid (non-null) FK → Patient.Id
+ ScheduledAt: DateTimeOffset (non-null)
+ Status: AppointmentStatus (non-null)
+ Reason: string (nullable)                             -- (Rev 2.9: was Notes)
+ BookedByStaffMemberId: Guid (nullable) FK → StaffMember.Id
+ AdmissionId: Guid (nullable) FK → Admission.Id        -- (Rev 2.9 — new, set at check-in)
```
**Table:** `appointments` — **built.** `Patient_AddAdmission`.

*(Rev 2.9)* **`Category` and `WardId` dropped; `AdmissionId` added.** The care level is
chosen by a clinician at the desk on check-in — `CheckInRequest` carries
`admission_category` and `category_set_by_staff_id` — so storing a guess at booking time
gave the same fact two homes and no rule about which one wins. `Reason` is free text shown
to staff and never read by the bed agent: free text stays data, never instructions.
**Note:** *(Rev 2)* Covers the patient flow's third arrival path — "they booked a visit
beforehand". Previously an `Admission` could only be emergency-linked or unexplained.
On check-in this becomes an `Admission` with `Source = Booked`.
`BookedByStaffMemberId` is nullable and reserved for the self-booking case; see
[Open Decisions](#open-decisions) for whether patients book directly.

#### Admission extends AuditedEntity
```
+ PatientId: Guid (non-null) FK → Patient.Id
+ Source: AdmissionSource (non-null)                    -- (Rev 2)
+ Category: AdmissionCategory (non-null)
+ Urgency: AdmissionUrgency (non-null)                  -- (Rev 2.2: was AcuityLevel)
+ Status: AdmissionStatus (non-null)                    -- (Rev 2.1: now 7 states, stored)
+ IsInfectious: bool = false (non-null)                 -- (Rev 2.2: was RequiresIsolation)
+ CategorySetByStaffMemberId: Guid (non-null) FK → StaffMember.Id  -- (Rev 2.2)
+ CategorySetAt: DateTimeOffset (non-null)              -- (Rev 2.2)
+ DispatchId: string (nullable)                          -- (Rev 2.5; Rev 2.9: Emergency's own reference, not a FK)
+ ReportedByUserId: Guid (nullable) FK → PatientAccount.Id -- (Rev 2.5 — new)
+ ExpectedArrivalAt: DateTimeOffset (nullable)          -- (Rev 2)
+ AdmittedAt: DateTimeOffset (nullable)                 -- (Rev 2: was non-null)
+ DischargedAt: DateTimeOffset (nullable)               -- (Rev 2.9 — new)
+ MissingFields: text[] (non-null, default '{}')        -- (Rev 2.2: was MissingDetails)
+ DetailsComplete: bool (GENERATED, stored)             -- (Rev 2.1)
+ DetailsCompletedAt: DateTimeOffset (nullable)         -- (Rev 2.1)
+ CancelReason: CancelReason (nullable)                 -- (Rev 2.2)
+ CancelNote: string(500) (nullable)                    -- (Rev 2.10 — new)
```
**Table:** `admissions` — **built.** `Patient_AddAdmission`, then
`Patient_AddOpenAdmissionIndex` and `Patient_AddCancelNote`.

*(Rev 2.10)* **`CancelNote` added; `Status`'s transitions are code, not schema.** The spec's
cancel body has always carried an optional `note` and there was no column for it. The seven
states and the moves between them stay in `AdmissionStatusMachine` — a transition table is a
rule, and putting rules in columns is how two copies of a workflow get started.

*(Rev 2.9)* **`EmergencyCallId` and `AppointmentId` dropped, `DispatchId` is a string.**
The spec's `Admission` returns exactly one Emergency reference, `dispatch_id`, and describes
it as "Emergency Service's reference" — their identifier, carried as text. A real foreign
key would mean Patient Management could not create an admission until Member 1's `dispatches`
table existed, which is precisely the dependency `STUBS.md` exists to avoid. The appointment
link moved to `Appointment.AdmissionId`: two nullable FKs pointing at each other is one link
with two places to disagree. `DischargedAt` is returned by the spec and had no column.
*(Rev 2)* The patient flow requires that for an emergency "the record is created
**BEFORE** they arrive, so a bed is ready when they get here". That state was
unrepresentable: `AdmittedAt` was non-null and `AdmissionStatus` was only
`{Active, Discharged}`.
`Urgency` and `IsInfectious` are the patient-side inputs to the bed agent's filter and
ranking rules; both are set by clinical staff, never by the agent — the same wall that
keeps `Category` a human decision.

*(Rev 2.2)* **Renamed to the names `patient-spec.yaml` already publishes.** Rev 2 invented
`AcuityLevel` and `RequiresIsolation` for fields the committed spec already had as
`urgency` and `is_infectious`. These were never two pairs of fields, only two names for
one pair, and keeping both would have meant either a translation layer or two columns
recording the same clinical fact:

| Rev 2.1 name | `patient-spec.yaml` | Drives |
| :--- | :--- | :--- |
| `AcuityLevel` (Critical/High/Medium/Low) | `urgency` (routine/urgent/emergency) | Soft rule S2 — higher urgency prefers a lower `NurseStationDistance` |
| `RequiresIsolation` | `is_infectious` | Hard rule H4 — an infectious patient must get `HasIsolation = true` |
| `MissingDetails` | `missing_fields` | The outstanding-paperwork worklist |

`is_infectious` is the better of the two names because it records the *clinical fact*;
"requires isolation" is the *consequence* the rule derives from it, and storing a
consequence invites it disagreeing with its own cause.

Also added, from the committed spec: `CategorySetByStaffMemberId` and `CategorySetAt`
(recorded proof a human — not the agent — chose the care level, which is the audit the
approval gate leans on), and `CancelReason`, whose closed enum was already published.

*(Rev 2.1 — review item 2)* **`Status` is stored and authoritative, not computed.**
The four Rev 2 values did not cover `awaiting_bed`, `awaiting_approval`, `bed_reserved` or
`ready_for_discharge`, so it now carries the same seven values `patient-spec.yaml` already
returns. Deriving them from `BedAssignment.Status` and the discharge checklist was the
alternative; it was rejected for one decisive reason:

> **A derived status cannot be guarded.** `patient-spec.yaml` already documents an
> `IllegalTransition` 409 with an explicit transition table — `admitted -> ready_for_discharge`
> is legal, `awaiting_bed -> admitted` is not. You can only reject an illegal move if there
> is a stored *previous* value to compare against. A computed status has no previous value:
> it would silently skip states whenever an underlying row changed, and the 409 could never
> fire.

The three-table join on every read was the secondary argument, but it is the weaker one —
correctness settles this, not cost.

**The drift risk is real and is handled by writing both in one transaction.** The
reservation, assignment and discharge rows remain the *mechanism*; `Status` is the *fact*.
Same treatment `Bed.Status` already gets. A reconciliation query belongs in the integration
test suite so drift fails a build rather than surfacing in a demo:

```sql
-- must return zero rows
SELECT a.id, a.status FROM admissions a
LEFT JOIN bed_assignments ba ON ba.admission_id = a.id AND ba.end_at IS NULL
WHERE (a.status = 'admitted'      AND ba.id IS NULL)
   OR (a.status = 'awaiting_bed'  AND ba.id IS NOT NULL);
```

*(Rev 2.1 — review item 4)* **`MissingFields` records what is still outstanding**, for the
"emergency arrival, relative brings the ID later" case. A `text[]` of field names rather
than a bare boolean, because "incomplete" alone does not tell a ward clerk *what to chase* —
`{nic, date_of_birth}` does. Values come from `PatientDetailField`; queryable with
`WHERE 'nic' = ANY(missing_fields)`, which is the outstanding-paperwork worklist.

`DetailsComplete` is a **stored generated column**, `cardinality(missing_fields) = 0` — the
one place in this schema where a computed value is right, because unlike `Status` it has no
transition rules to enforce and cannot drift by construction. `DetailsCompletedAt` records
when the gap closed, which answers "how long did we hold an unidentified patient".

Deliberately **separate from `Status`**: how far through their stay a patient is and how
complete their paperwork is are independent facts. A fully-admitted ICU patient can still be
missing a NIC, and collapsing the two would make one unrepresentable.

#### BedAssignment extends AuditedEntity *(Rev 2.9 — changed)*
```
+ AdmissionId: Guid (non-null) FK → Admission.Id
+ BedId: Guid (non-null)                                -- no FK: beds is Equipment's table
+ Status: AssignmentStatus (non-null)                   -- (Rev 2.9 — new)
+ ReservedUntil: DateTimeOffset (nullable)              -- (Rev 2.9 — the 30-minute hold)
+ AssignedBy: AssignedBy (non-null)                     -- (Rev 2.9: agent or human)
+ WorkflowId: Guid (nullable) → AgentWorkflow.Id        -- (Rev 2: no FK until that table exists)
+ IsDowngrade: bool = false (non-null)                  -- (Rev 2)
+ ApprovedByStaffMemberId: Guid (nullable) FK → StaffMember.Id  -- (Rev 2)
+ ApprovedAt: DateTimeOffset (nullable)                 -- (Rev 2.9 — new)
+ OverrideReason: string (nullable)                     -- (Rev 2.9: was DowngradeReason)
+ ReleasedAt: DateTimeOffset (nullable)                 -- (Rev 2.9: was EndAt)
+ ReleaseReason: ReleaseReason (nullable)               -- (Rev 2.9 — new)
```
**Table:** `bed_assignments` — **built.** `Patient_AddAdmission`.
**Constraints:**
- `UNIQUE (bed_id) WHERE status IN ('reserved', 'occupied')`
- `UNIQUE (admission_id) WHERE status IN ('reserved', 'occupied')`

**Note:** Multiple rows per `Admission` (ward/bed transfers mid-stay); exactly one live row
at a time. *(Decision 19)* That "exactly one" rule was prose only — two concurrent requests
could both succeed. It is enforced by the two partial unique indexes above, not by any check
in a service: two nurses assigning bed 12 at the same instant both pass an application-level
"is it free?" test, and the second `INSERT` is what actually fails.

`IsDowngrade` records the flow's "if ICU is full it offers the next best thing, **flagged as
a downgrade**". It is a real column rather than a note in the workflow payload because it
*routes the approval* — downgrades are Duty Manager only — so it has to be queryable and
auditable.

*(Rev 2.9)* **This row is the hold as well as the occupancy.** `BedReservation` was a
separate table until the spec was implemented; it publishes one bed row whose `status` walks
`reserved → occupied → released`, and no reservation schema at all. One row per claim means
"this bed is taken" has exactly one home, which is the same argument this document makes for
keeping occupancy off `Bed`. A background sweep moves `reserved` rows past `ReservedUntil` to
`released` with `ReleaseReason = HoldExpired`.

**`BedId` is a plain column, not a foreign key.** `beds` belongs to Health Equipment
(Member 3) and does not exist yet. The constraint goes in when their table lands; until then
`STUBS.md` row 109 stands.

#### Discharge extends AuditedEntity *(Rev 2.9 — changed)*
```
+ AdmissionId: Guid (unique, non-null) FK → Admission.Id
+ FlaggedBy: AssignedBy (non-null)                      -- (Rev 2.9: agent or human)
+ FlaggedAt: DateTimeOffset (non-null)                  -- (Rev 2.9 — new)
+ ConfirmedByStaffMemberId: Guid (nullable) FK → StaffMember.Id  -- (Rev 2.9: was DischargedByStaffMemberId)
+ ConfirmedAt: DateTimeOffset (nullable)                -- (Rev 2.9: was DischargedAt)
+ SummaryNote: string (nullable)                        -- (Rev 2.9: was DischargeSummary)
```
**Table:** `discharges` — **built.** `Patient_AddAdmission`.
**Note:** 1:1 companion row created **at admission time**, with its checklist rows unticked,
not only once discharge actually happens — this gives clinical staff somewhere to
update readiness during the stay, and gives the Patient Admission & Bed Agent a
persistent target to monitor. `DischargedAt`/`DischargeSummary` stay null until
confirmed. *(Decisions 10, 32)*
*(Rev 2.9)* **`ReadinessStatus` is gone entirely.** Rev 2 already made it derived — `Ready`
once every checklist item was complete — and a derived value still stored is a value that can
drift. The spec publishes `all_mandatory_ticked` instead, computed from the rows on read, so
the flag and the boxes cannot disagree. How far through their stay the patient is stays on
`Admission.Status`, where the transition guard can see it. `DischargedAt` moved to
`Admission`, which is where the spec returns it.

#### DischargeChecklistItem extends AuditedEntity *(Rev 2 — new; Rev 2.9 — changed)*
```
+ DischargeId: Guid (non-null) FK → Discharge.Id
+ ItemType: DischargeChecklistItemType (non-null)
+ IsMandatory: bool = true (non-null)                   -- (Rev 2.9 — new)
+ TickedAt: DateTimeOffset (nullable)                   -- (Rev 2.9: was CompletedAt)
+ TickedByStaffMemberId: Guid (nullable) FK → StaffMember.Id  -- (Rev 2.9: was CompletedByStaffMemberId)
+ Notes: string (nullable)
```
**Table:** `discharge_checklist_items` — **built.** `Patient_AddAdmission`.
**Constraint:** UNIQUE(DischargeId, ItemType)

*(Rev 2.9)* **Five item types, and each row says whether it blocks.** The spec's
`ChecklistUpdateRequest` publishes `clinical_clearance`, `medication_issued`,
`billing_settled`, `follow_up_recorded` and `transport_arranged`, and its `ChecklistItem`
carries a `mandatory` flag — a follow-up appointment that has not been booked should not
hold a well patient in a bed, but an unpaid bill might. `ticked` is not a column:
`TickedAt IS NOT NULL` is the answer, so a boolean and a timestamp can never contradict
each other.
**Note:** *(Rev 2)* The patient flow's step 7 is "staff tick a checklist (doctor's
clearance, medicine, bill settled) → all ticked → shows on a 'ready to go' list". A single
A single readiness enum could not represent independently tickable items or record
who ticked each one. Rows are seeded alongside the `Discharge` row at admission time.

#### CareRecommendation extends AuditedEntity *(Rev 2.6 — new)*
```
+ PatientId: Guid (non-null) FK → Patient.Id
+ AdmissionId: Guid (nullable) FK → Admission.Id       -- set when raised during a stay
+ ReportedText: string (non-null)                      -- the patient's own words
+ ReportedAt: DateTimeOffset (non-null)
+ RedFlag: bool = false (non-null)                     -- matched the emergency-keyword screen
+ UrgencyFlag: CareUrgency (nullable)                   -- the agent's draft triage flag
+ AgentMessage: string (nullable)                       -- the agent's draft, doctor-facing only
+ Status: CareRecommendationStatus (non-null)
+ ReviewedByStaffMemberId: Guid (nullable) FK → StaffMember.Id  -- Doctor role, enforced in code
+ ReviewedAt: DateTimeOffset (nullable)
+ DoctorMessage: string (nullable)                     -- what the patient actually sees
+ RejectionReason: string (nullable)                   -- staff-facing only, never sent to the patient
```
**Table:** `care_recommendations`
**Note:** *(Rev 2.6)* The second Patient Management agent's domain row, added on the
lecturer's direction — see the Rev 2.6 note above. `AgentMessage` and `DoctorMessage` are
deliberately two columns, not one edited in place: the model's draft must survive
independently of what a doctor approved, for the same audit reason `AgentWorkflow`
already keeps a `FinalOutcome` separate from its `Plan`. **The patient never reads
`AgentMessage`** — only `DoctorMessage`, and only once `Status = Approved`. A `Doctor`
role check gates every write to the review fields; `ReviewedByStaffMemberId` is how a
`clinical_clearance`-style approval trail exists for this agent too.
**Why no new column on `AgentProposedChange`.** That table's typed FKs
(`ProposedStaffMemberId`, `ProposedShiftId`, `ProposedBedId`, `ProposedWardId`) exist
because those four values need referential integrity and query-by-content. This agent's
proposed write is a **new** `CareRecommendation` row — a create, exactly like `ReserveBed`
already is — so it needs no target FK, only a `Payload` (`patient_id`, `admission_id`,
`urgency_flag`, `agent_message`) and `AppliedEntityId` once the row exists. No group-owned
shared table changes shape for this.

---

### Cross-Cutting: Agent Workflow & Audit

#### AgentWorkflow extends AuditedEntity
```
+ AgentType: AgentType (non-null)
+ EntityType: string (non-null) -- polymorphic target type name
+ EntityId: Guid (non-null) -- polymorphic target id
+ CorrelationId: Guid (non-null)                        -- (Rev 2)
+ ParentWorkflowId: Guid (nullable) FK → AgentWorkflow.Id -- (Rev 2)
+ Objective: string (non-null)
+ Plan: jsonb (non-null, default '[]')
+ CompletedSteps: jsonb (non-null, default '[]')
+ ToolResults: jsonb (non-null, default '[]')
+ ValidationResults: jsonb (nullable)
+ Errors: jsonb (nullable)
+ Status: AgentWorkflowStatus (non-null)
+ RequiredApproverRole: StaffRole (nullable)            -- (Rev 2)
+ StartedAt: DateTimeOffset (nullable)                  -- (Rev 2)
+ CompletedAt: DateTimeOffset (nullable)                -- (Rev 2)
+ AttemptCount: int = 0 (non-null)                      -- (Rev 2)
+ ReviewedByStaffMemberId: Guid (nullable) FK → StaffMember.Id  -- (Rev 2: was ApprovedBy*)
+ ReviewedAt: DateTimeOffset (nullable)                 -- (Rev 2: was ApprovedAt)
+ ReviewNotes: string (nullable)                        -- (Rev 2)
+ FinalOutcome: string (nullable)
```
**Table:** `agent_workflows`
**Note:** Single generic schema shared by all 4 agents, linked to its target domain
row via `(EntityType, EntityId)` — e.g. `EntityType="EmergencyCall"` for Dispatch &
Routing, `EntityType="Warning"` for Equipment Monitoring. Every agent action is
recorded here, including auto-executed fast paths (`Status = AutoApproved`) — the
domain write only happens once `Status` reaches `Approved` or `AutoApproved`, never
before. Persisted fields match the assignment's mandated workflow-state list; no hidden
reasoning, passwords, or tokens are stored here. *(Decisions 4, 18, 20)*

*(Rev 2)* Five changes, each traceable to a requirement:
- **`CorrelationId` / `ParentWorkflowId`** — the component plan has one emergency call
  fanning out to all four agents with the Duty Manager reviewing "the full plan". Four
  unrelated rows pointing at four different `EntityType`s could not be queried as a chain
  or approved as a unit.
- **`RequiredApproverRole`** — the patient flow routes approval by content ("normal ward →
  ward nurse; ICU or downgrade → Duty Manager only"). Persisting the required role keeps
  the authorization rule visible to the audit trail instead of buried in C#.
- **`StartedAt`/`CompletedAt`/`AttemptCount`** — spec §9.1 Observability names *timings*
  and *retries* explicitly; `CreatedAt`/`UpdatedAt` do not cover either.
- **`ReviewedBy*` rename** — `ApprovedBy*` could not record who *rejected* a workflow.
- **Status enum** — see `AgentWorkflowStatus` below.

**Reasoning trace vs proposed writes:** `Plan`, `CompletedSteps`, `ToolResults`,
`ValidationResults` and `Errors` stay `jsonb` — genuinely variable-shape across four
agents, and inventing a relational schema for them buys nothing. The concrete domain
mutations a workflow proposes are **not** stored here; they go in `AgentProposedChange`,
where they get FK integrity, per-change validation verdicts, and the uniqueness
constraints that make concurrent workflows safe.

#### AgentProposedChange extends AuditedEntity *(Rev 2 — new)*
```
+ AgentWorkflowId: Guid (non-null) FK → AgentWorkflow.Id
+ Sequence: int (non-null)
+ ChangeType: ProposedChangeType (non-null)
+ TargetEntityType: string (nullable)   -- row being ended/replaced; null for creates
+ TargetEntityId: Guid (nullable)
+ ProposedStaffMemberId: Guid (nullable) FK → StaffMember.Id
+ ProposedShiftId: Guid (nullable) FK → Shift.Id
+ ProposedBedId: Guid (nullable) FK → Bed.Id
+ ProposedWardId: Guid (nullable) FK → Ward.Id
+ Payload: jsonb (non-null, default '{}')  -- remaining field values for the write
+ ValidationStatus: ProposedChangeValidationStatus (non-null)
+ ValidationMessage: string (nullable)
+ AppliedAt: DateTimeOffset (nullable)
+ AppliedEntityId: Guid (nullable)      -- the row actually created/updated
```
**Table:** `agent_proposed_changes`
**Constraint:** UNIQUE(AgentWorkflowId, Sequence)
**Note:** *(Rev 2)* One row per domain write a workflow wants to make. The cascading swap
produces two (`EndAllocation`, `CreateAllocation`); a bed proposal produces one
(`ReserveBed`). The typed nullable FKs — rather than IDs buried in `Payload` — are what
give the deterministic validator referential integrity (a proposal cannot reference a
soft-deleted nurse) and what let the approval query filter by content without
`jsonb_path_query`. `ValidationStatus` records the step-3 verdict per change, so a partly
invalid plan can be returned for revision instead of rejected wholesale.

#### Notification extends AuditedEntity *(Rev 2 — new)*
```
+ RecipientStaffMemberId: Guid (non-null) FK → StaffMember.Id
+ Channel: NotificationChannel (non-null)
+ Title: string (non-null)
+ Body: string (non-null)
+ EntityType: string (nullable)     -- deep-link target
+ EntityId: Guid (nullable)
+ Status: NotificationStatus (non-null)
+ SentAt: DateTimeOffset (nullable)
+ ReadAt: DateTimeOffset (nullable)
+ FailureReason: string (nullable)
```
**Table:** `notifications`
**Note:** *(Rev 2)* No notification entity existed before, yet both flows depend on one:
the staff flow "pushes a live alert to the administrative system" (React) and sends the
reassigned nurse "an immediate push notification" (Flutter); the patient flow notifies on
bed assignment. `Channel` distinguishes in-app alerts from device push. Delivery uses
`DeviceToken`. `(EntityType, EntityId)` lets the client deep-link to the workflow awaiting
approval.

#### AuditLog extends Entity
```
+ EntityType: string (non-null)
+ EntityId: Guid (non-null)
+ Operation: AuditOperation (non-null)
+ PerformedByStaffMemberId: Guid (nullable) FK → StaffMember.Id
```
**Table:** `audit_logs`
**Note:** Lightweight action log (no before/after value diffs), captured
automatically via an EF Core `SaveChanges` interceptor. Scoped to the main
aggregate-root entities plus the two allocation/assignment tables that agent workflows
mutate — `EmergencyCall`, `Dispatch`, `Ambulance`, `StaffMember`, `Shift`,
**`Allocation`** *(Rev 2)*, `LeaveRequest`, `EquipmentItem`, `PharmacyItem` *(Rev 3)*,
`MaintenanceSchedule`, `Patient`, `Admission`, **`BedAssignment`** *(Rev 2)*,
`Discharge`, `Ward`, `Bed`, `AgentWorkflow`.
`PerformedByStaffMemberId` nullable for system-initiated changes (e.g. deterministic
`Warning` generation). *(Decisions 7, 11, 12, 13)*
*(Rev 2)* `Allocation` and `BedAssignment` were previously excluded as "join tables", but
both flows require exactly those writes to be audited — the staff flow's step 4 says
"the `AuditLog` records the exact time and approving user" about an `Allocation` change.
`PerformedAt` removed: it duplicated the inherited `CreatedAt` exactly.
**Soft deletes** are recorded as `Operation = Delete` (not `Update`) when the interceptor
sees `IsActive` transition `true → false`.

---

## Enums

### StaffRole *(Rev 2.2 — aligned to `staff-spec.yaml`)*
```
WardNurse, Doctor, AmbulanceCrew, GeneralStaff, DutyManager,
HospitalAdministrator, EquipmentManager
```
Serialized as `ward_nurse`, `doctor`, `ambulance_crew`, `general_staff`, `duty_manager`,
`hospital_administrator`, `equipment_manager` — as published in `staff-spec.yaml`.

*(Rev 2.2)* Three changes, all resolving disagreements `staff-spec.yaml` itself flagged as
open:

- **`Doctor` added.** `patient-spec.yaml` gates the `clinical_clearance` discharge
  checklist item on a `Doctor` claim, and `staff-spec.yaml` already carries the role. The
  diagram was the only document without it, which made a published authorization rule
  unimplementable.
- **`DutyDispatchManager` → `DutyManager`**, matching both committed specs. This is the
  role the bed-downgrade approval gate names.
- **`EquipmentInventoryManager` → `EquipmentManager`**, matching `staff-spec.yaml`.

Note that `equipment-management-plan.md` §2 works in terms of two *capabilities* —
Inventory Administrator and Equipment Technician — rather than this single role. Member 3
and Member 2 should confirm whether that is one role or two; carried as Open Decision 11.

### PrincipalRole *(Rev 2.7 — new)*
```
WardNurse, Doctor, AmbulanceCrew, GeneralStaff, DutyManager,
HospitalAdministrator, EquipmentManager, Patient
```
Serialized `ward_nurse` … `equipment_manager`, plus `patient`. **Never stored** — it
exists only in the JWT `role` claim and in `CurrentPrincipal` on the wire.

*(Rev 2.7)* A separate name rather than adding `Patient` to `StaffRole`, because
`StaffRole` is a column on `staff_members` and a patient has no row there. One enum for
two different things would make an impossible value representable in the database.

### PrincipalType *(Rev 2.7 — new)*
```
Staff, Patient
```
Serialized `staff`, `patient`. Stored on `refresh_tokens.principal_type` and carried in
the JWT as the `typ` claim.

*(Rev 2.7)* **This is the claim that is easy to leave out and expensive to add back.**
`sub` alone is ambiguous: a `StaffMember.Id` and a `PatientAccount.Id` are both GUIDs from
different tables. An endpoint that trusts `sub` without checking `typ` looks a patient id
up in `staff_members`, finds nothing, and either 500s or — worse — silently treats the
request as unauthenticated staff.

### CallPriority
```
Critical, High, Medium, Low
```

### CallStatus
```
Received, Dispatched, EnRoute, Completed, Cancelled
```

### AmbulanceStatus
```
Available, Dispatched, EnRoute, AtScene, Transporting, OutOfService
```

### DispatchStatus
```
Assigned, EnRoute, Completed, Cancelled, Reassigned
```

### AllocationStatus *(Rev 2 — new)*
```
Proposed, Confirmed, Cancelled, Released
```
`Proposed` = pending workflow approval. `Confirmed` = live roster entry (the only status
the UNIQUE index covers). `Released` = ended by a swap, with `ReplacedByAllocationId` set.
`Cancelled` = shift or proposal withdrawn before it ever went live.

### LeaveRequestType *(Rev 2 — changed)*
```
Annual, Sick, Emergency, ShiftSwap
```
Was `{Leave, ShiftSwap}`. `Leave` renamed `Annual`; `Sick` and `Emergency` added.

### LeaveRequestStatus
```
Pending, Approved, Rejected
```

### EquipmentStatus *(Rev 3 — renamed from `EquipmentItemStatus`, values replaced)*
```
Available, Assigned, Maintenance, Retired
```
Serialized as `available`, `assigned`, `maintenance`, `retired`.

*(Rev 3)* The old `Operational, UnderMaintenance, OutOfService` described a machine's
condition. These describe where it is in its life, which is what the register actually
tracks: `Assigned` says a patient has it, and `Retired` is terminal — a replacement is a new
row, never a revived one. Every status change goes through one guard, so an illegal move is
a 409 and never a quiet success. Reporting a fault is the single documented exemption from
that table: it moves an item to `Maintenance` from any state but `Retired`, including while
a patient has it, because refusing that would leave a known-faulty machine reading as usable.

### BedCondition *(Rev 3 — new)*
```
Usable, OutOfService
```
Serialized as `usable`, `out_of_service`. A bed's condition, which is ours. Whether anyone is
in it is Patient Management's and is never a column here.

### MaintenanceType *(Rev 3 — new)*
```
RoutineService, Calibration, Repair
```
Serialized as `routine_service`, `calibration`, `repair`.

### MaintenanceStatus *(Rev 3 — values replaced)*
```
Scheduled, InProgress, Completed, Overdue, Cancelled
```
Serialized as `scheduled`, `in_progress`, `completed`, `overdue`, `cancelled`.

*(Rev 3)* **`Overdue` is never stored.** It is derived on read from `scheduled_date < today
AND status = 'scheduled'`, so nothing has to be swept nightly and nothing can drift.

### AssetType *(Rev 3 — new)*
```
EquipmentItem, Bed
```
Serialized as `equipment_item`, `bed`. What a `MaintenanceSchedule` row points at.

### PharmacyTransactionType *(Rev 3 — new)*
```
Received, Dispensed, Adjusted, ExpiredRemoved
```
Serialized as `received`, `dispensed`, `adjusted`, `expired_removed`.

Quantity on a transaction is always positive; **this is what gives it a sign**, so a row can
never be read two ways. `Dispensed` and `ExpiredRemoved` take stock and are guarded;
`Received` and `Adjusted` add it.

**Open:** `equipment-management-plan.md` §5.1 describes `Adjusted` as `±quantity`, but the
published request carries a positive quantity with no sign and the documented 409 names only
the two taking types. A stocktake that finds *fewer* boxes therefore cannot be recorded
today. Settling it needs a sign on the request or a fifth value here.

### WarningType *(Rev 3 — values replaced)*
```
LowStock, MedicineExpiring, MaintenanceOverdue, EquipmentFaulty
```
Serialized as `low_stock`, `medicine_expiring`, `maintenance_overdue`, `equipment_faulty`.

### WarningSeverity
```
Low, Medium, High, Critical
```
Serialized as `low`, `medium`, `high`, `critical`.

### WarningStatus *(Rev 3 — values replaced)*
```
Open, Acknowledged, ActionTaken, Dismissed
```
Serialized as `open`, `acknowledged`, `action_taken`, `dismissed`.

*(Rev 3)* `Resolved` is gone. A warning that led somewhere is `action_taken`; one a person
judged not worth acting on is `dismissed`. Collapsing both into "resolved" loses which
happened, and the agent-performance report is exactly the question of which.

### RelatedEntityType *(Rev 3 — new)*
```
PharmacyItem, EquipmentItem, Bed
```
Serialized as `pharmacy_item`, `equipment_item`, `bed`. What a `Warning` points at.

### RaisedBy *(Rev 3 — new)*
```
Agent, User
```
Serialized as `agent`, `user`. Who raised a warning or proposed an action. The distinction is
load-bearing: the agent-performance report and the approval threshold both read it.

### ActionType *(Rev 3 — new)*
```
ReorderPharmacyStock, ScheduleMaintenance, ReallocateEquipment, RetireEquipment,
DisposeExpiredStock
```
Serialized as `reorder_pharmacy_stock`, `schedule_maintenance`, `reallocate_equipment`,
`retire_equipment`, `dispose_expired_stock`. Decides the shape of `ActionRequest.Details`.

### ActionRequestStatus *(Rev 3 — new)*
```
PendingApproval, Approved, Rejected, Completed
```
Serialized as `pending_approval`, `approved`, `rejected`, `completed`.

### Urgency *(Rev 3 — new)*
```
Routine, Urgent, Critical
```
Serialized as `routine`, `urgent`, `critical`. Feeds the deterministic approval threshold:
`Urgent` or `Critical` always requires a human.

### AdmissionCategory *(Rev 2 — changed; Rev 2.2 — wire values pinned)*
```
ICU, HighDependency, Inpatient, DayCase, Outpatient
```
Serialized as `icu`, **`hdu`**, `inpatient`, `day_case`, `outpatient`.

*(Rev 2.2)* `HighDependency` serializes to **`hdu`**, not `high_dependency`.
`patient-spec.yaml` publishes `hdu` in both this enum and `WardType`, and its downgrade
ladder is documented as `icu -> hdu -> inpatient`. Rev 2.1 said enum literals were
"normalised to snake_case to match your wire values", which for this member produced
`high_dependency` and silently broke the match. The C# member keeps the readable name and
`HasConversion<string>()` maps it to `hdu`.

Ordered most to least acute so the bed agent's downgrade logic ("offers the next best
thing") is a simple ordinal step.

### WardType *(Rev 2.2 — new)*
```
ICU, HighDependency, General, Maternity, Pediatric, Isolation
```
Serialized as `icu`, `hdu`, `general`, `maternity`, `pediatric`, `isolation` — as
published in `patient-spec.yaml`. `Ward.Type` used to reuse `AdmissionCategory`, which
could not express a maternity, pediatric or isolation ward. Overlaps `AdmissionCategory`
on `icu`/`hdu` only; the two lists are not interchangeable.

### AdmissionUrgency *(Rev 2.2 — replaces AcuityLevel)*
```
Routine, Urgent, Emergency
```
Serialized as `routine`, `urgent`, `emergency` — the enum `patient-spec.yaml` already
publishes. Patient-side input to "sicker patient goes closer to the nurses' station";
pairs with `Bed.NurseStationDistance`, and breaks the tie when two admissions want the
last bed.

Rev 2 introduced this as `AcuityLevel {Critical, High, Medium, Low}`, which duplicated
both the committed `urgency` field and the shape of `CallPriority`. One field, one name.

Named `AdmissionUrgency`, not `Urgency`, because `equipment-spec.yaml` already has an
`Urgency` of its own — how urgent a maintenance or restock job is. Two different facts
that happened to pick the same word; in one shared project they need different names.
The JSON field stays `urgency`, so nothing about the API changes.

### Gender *(Rev 2 — new)*
```
Male, Female, Other, Unknown
```
Serialized as `male`, `female`, `other`, `unknown`.

*(Rev 2.2)* **`Unknown` is correct and `patient-spec.yaml` was the document at fault** —
it published only three values while the same spec allows an unidentified arrival with no
NIC, no phone and a generated `TempReference`. A patient nobody can identify has an unknown
gender, and the gender-ward filter (hard rule H3) has to do something deterministic with
that. `patient-spec.yaml` has been updated to match this enum, not the other way round.

### GenderPolicy *(Rev 2 — new; Rev 2.8 — renamed)*
```
Male, Female, Mixed
```
Serialized as `male`, `female`, `mixed`. *(Rev 2.8)* Was `WardGenderPolicy` here and
`GenderPolicy` in `patient-spec.yaml`; the spec wins on an entity Member 4 owns.

### AdmissionSource *(Rev 2 — new; Rev 2.2 — aligned)*
```
Emergency, WalkIn, PreRegistered
```
Serialized as `emergency`, `walk_in`, `pre_registered` — as published in
`patient-spec.yaml`.

*(Rev 2.2)* `Booked` renamed `PreRegistered`: same concept, and the committed spec's name
wins on an entity Member 4 owns. It is still the `Appointment` check-in path Rev 2 added,
which was a real gap worth keeping.

`Referral` is **not** dropped on the merits — it is a genuine fourth arrival path that
`patient-spec.yaml` has no value for. Adding it means adding it to the published enum too,
so it is carried to Open Decisions as item 10 rather than decided here by one member.

### AppointmentStatus *(Rev 2 — new)*
```
Scheduled, CheckedIn, Completed, Cancelled, NoShow
```

### BedCondition *(Rev 2.2 — replaces BedStatus)*
```
Usable, OutOfService
```
Serialized as `usable`, `out_of_service` — identical in `equipment-spec.yaml` and
`patient-spec.yaml`, which both already publish it.

Rev 2's `BedStatus {Available, Reserved, Occupied, Cleaning, Maintenance}` mixed three
different owners' facts into one column on a table Equipment owns: `Occupied` is Patient
Management's (`BedAssignment`), `Reserved` is Patient Management's (`BedAssignment` again,
under `Status = Reserved` — *Rev 2.9*), and
only `Maintenance` was ever Equipment's. `Condition` now carries the Equipment fact alone;
the other two are read from their owners' rows. See the `Bed` note and
`integration_of_functions.md` §6.1. `Cleaning` is Open Decision 7.

### AssignmentStatus *(Rev 2.9 — replaces BedReservationStatus)*
```
Reserved, Occupied, Released
```
Serialized as `reserved`, `occupied`, `released` — as published in `patient-spec.yaml`.
One `BedAssignment` row walks the three in order. `Reserved` is the 30-minute hold, and the
partial unique indexes treat `reserved` and `occupied` alike: both claim the bed.

Rev 2's `BedReservationStatus {Held, Confirmed, Expired, Released}` belonged to a separate
`bed_reservations` table that the committed spec does not have. `Confirmed` became
`Occupied`, and `Expired` became `Released` with `ReleaseReason = HoldExpired` — the reason
a hold ended is a different fact from the state it ended in, and keeping them apart means
"why is this bed free again?" has one answer instead of two half-answers.

### AssignedBy *(Rev 2.9 — new)*
```
Agent, User
```
Serialized as `agent`, `user`. Whether the bed agent proposed this or a human picked it.
Used on `BedAssignment.AssignedBy` and `Discharge.FlaggedBy`, and it is what the approval
gate and the agent-performance report both read.

### ReleaseReason *(Rev 2.9 — new)*
```
Discharged, HoldExpired, Cancelled, Transferred, Rejected
```
Serialized as `discharged`, `hold_expired`, `cancelled`, `transferred`, `rejected` — as
published in `patient-spec.yaml`.

### AdmissionStatus *(Rev 2.1 — changed again, review item 2)*
```
AwaitingBed, AwaitingApproval, BedReserved, Admitted,
ReadyForDischarge, Discharged, Cancelled
```
Serialized and stored as `awaiting_bed`, `awaiting_approval`, `bed_reserved`, `admitted`,
`ready_for_discharge`, `discharged`, `cancelled` — identical to the enum
`patient-spec.yaml` already publishes, so the API needs no translation layer.

Rev 2's four values (`Expected, Active, Discharged, Cancelled`) could not express the four
middle states of the flow. `Expected` is now `AwaitingBed`; `Active` is now `Admitted`.

| Status | Meaning | `AdmittedAt` |
| :-- | :-- | :--: |
| `AwaitingBed` | Record exists, no bed found yet. The emergency pre-arrival state. | null |
| `AwaitingApproval` | The bed agent proposed a bed; a human has not approved it. | null |
| `BedReserved` | Approved and held (`BedAssignment.Status = Reserved`); patient not yet in it. | null |
| `Admitted` | In the bed. Live `BedAssignment` with `EndAt IS NULL`. | set |
| `ReadyForDischarge` | Every `DischargeChecklistItem` complete; awaiting confirmation. | set |
| `Discharged` | Confirmed and gone; the bed is released. | set |
| `Cancelled` | Never happened — diverted, false alarm, no-show, died en route. | null |

Legal transitions — anything else is a 409, matching `patient-spec.yaml`'s
`IllegalTransition` response:

```
AwaitingBed       ──► AwaitingApproval, Cancelled
AwaitingApproval  ──► BedReserved, AwaitingBed, Cancelled
BedReserved       ──► Admitted, AwaitingBed, Cancelled
Admitted          ──► ReadyForDischarge
ReadyForDischarge ──► Discharged, Admitted        (a patient can deteriorate)
```

`AwaitingApproval → AwaitingBed` and `BedReserved → AwaitingBed` are the rejection and
hold-expiry paths: the proposal was refused or the 30 minutes ran out, and the search
restarts.

### CancelReason *(Rev 2.2 — new)*
```
DivertedToOtherHospital, FalseAlarm, DiedEnRoute, PatientRefused, NoShow
```
Serialized as `diverted_to_other_hospital`, `false_alarm`, `died_en_route`,
`patient_refused`, `no_show` — as published in `patient-spec.yaml`. `Admission.Cancelled`
already existed as a state with nothing recording *why*; the closed enum was in the spec
but had no home in this document.

### CareUrgency *(Rev 2.6 — new)*
```
Low, Medium, High
```
The Patient Care Advisory Agent's draft triage flag. `High` is forced by the deterministic
keyword screen for red-flag symptoms (`patient-management-plan.md` §8.13), never left to
model judgement alone.

### CareRecommendationStatus *(Rev 2.6 — new)*
```
PendingReview, Approved, Rejected
```
A `CareRecommendation` is invisible to the patient until `Approved`. `Rejected` still
records the doctor's reason, but the patient only ever sees a generic note that their
doctor reviewed it — never `RejectionReason` itself.

### PatientDetailField *(Rev 2.1 — new; Rev 2.9 — aligned to the spec)*
```
Nic, FullName, DateOfBirth, Phone, Address,
EmergencyContactName, EmergencyContactPhone
```
The allowed members of `Admission.MissingFields`. A closed set rather than free text, so
"what is still outstanding" can be counted and filtered instead of parsed.

*(Rev 2.9)* Renamed to `Nic` and `Phone`, and `Gender` dropped, so the list is exactly the
keys `CompleteDetailsRequest` accepts — these are the fields a relative can bring in later.
`Gender` is required at intake (`Unknown` is a legitimate answer for an unidentified
arrival), so it can never be outstanding. `FullName` added for the provisional-name case,
where Emergency sends what the caller shouted down the phone.

### DischargeReadinessStatus — **removed** *(Rev 2.9)*
Readiness is `all_mandatory_ticked`, computed from the `DischargeChecklistItem` rows on
read. A stored copy of a value derived from other rows is a value that can drift, and this
one had no transition rules to justify storing it — unlike `AdmissionStatus`, which does.

### DischargeChecklistItemType *(Rev 2 — new; Rev 2.9 — aligned to the spec)*
```
ClinicalClearance, MedicationIssued, BillingSettled, FollowUpRecorded, TransportArranged
```
Serialized as `clinical_clearance`, `medication_issued`, `billing_settled`,
`follow_up_recorded`, `transport_arranged` — the five keys `ChecklistUpdateRequest`
publishes. Rev 2's three were a shorter list under different names for the same boxes;
the committed spec wins. `ClinicalClearance` is the Doctor-only one, enforced in the
service against the role claim, not in the schema.

### AgentType
```
DispatchRouting, StaffAllocation, EquipmentMonitoring, PatientAdmissionBed,
PatientCareAdvisory
```
*(Rev 2.6)* `PatientCareAdvisory` is the second Patient Management agent — see the Rev 2.6
note near the top of this document. `PatientAdmissionBed` is unchanged.

### AgentWorkflowStatus *(Rev 2 — changed)*
```
Pending, PendingApproval, AutoApproved, Approved,
RevisionRequested, Rejected, Executed, Failed
```
Was `{Pending, AutoApproved, Approved, Rejected}`. The staff flow persists the proposal as
`Pending`, then "the workflow state advances to `PendingApproval`" once deterministic
validation passes — the old enum could not distinguish those. `RevisionRequested` is
required by spec §9.1 ("approves, rejects **or requests revision**") and by the React
approve/reject/revise controls in spec §7. `Executed` separates "a human said yes" from
"the domain write actually landed". `Failed` is required by spec §9.1's "safe, clearly
recorded failure".

Valid transitions:
```
Pending ──► PendingApproval ──► Approved ──────► Executed
   │              │         └──► RevisionRequested ──► Pending
   │              └────────► Rejected
   ├──► AutoApproved ──────────────────────────► Executed
   └──► Failed        (any state may fail; Errors is populated)
```

### ProposedChangeType *(Rev 2 — new)*
```
EndAllocation, CreateAllocation, ReserveBed, AssignBed, ReleaseBed,
CreateDispatch, TransferEquipment, CreateMaintenanceSchedule,
CreateCareRecommendation
```
*(Rev 2.6)* `CreateCareRecommendation` is the write the Patient Care Advisory Agent
proposes — a new `CareRecommendation` row, same shape as `ReserveBed`.

### ProposedChangeValidationStatus *(Rev 2 — new)*
```
Pending, Passed, Failed
```

### NotificationChannel *(Rev 2 — new)*
```
InApp, Push, Sms
```

### NotificationStatus *(Rev 2 — new)*
```
Queued, Sent, Failed
```

### DevicePlatform *(Rev 2 — new)*
```
Android, Ios, Web
```

### AuditOperation
```
Create, Update, Delete
```

---

## Constraints & Indexes

Everything in this section is a database-level guarantee. None of it existed before
Rev 2, and several stated invariants were prose only — enforceable by convention alone,
which two concurrent requests will defeat.

### Partial unique indexes — soft-delete aware

Plain `UNIQUE` on a soft-deletable table is a bug: deactivate ward `ICU-1` and you can
never create another `ICU-1`. Worse under EF Core, where a global query filter on
`IsActive` hides the conflicting row, so the service-layer duplicate check passes and
`SaveChanges` throws a `DbUpdateException` the app cannot explain to the user.

```sql
CREATE UNIQUE INDEX ux_staff_members_email     ON staff_members (email)              WHERE is_active;
CREATE UNIQUE INDEX ux_patient_accounts_phone ON patient_accounts (phone_number)     WHERE is_active;   -- (Rev 2.7)
CREATE UNIQUE INDEX ux_ambulances_reg          ON ambulances (registration_number)   WHERE is_active;
CREATE UNIQUE INDEX ux_wards_name              ON wards (name)                       WHERE is_active;
CREATE UNIQUE INDEX ux_beds_ward_number        ON beds (ward_id, bed_number)         WHERE is_active;
CREATE UNIQUE INDEX ux_equipment_categories_name ON equipment_categories (name)      WHERE is_active;   -- (Rev 3)
CREATE UNIQUE INDEX ux_equipment_items_asset_tag ON equipment_items (asset_tag)       WHERE is_active;   -- (Rev 3)
CREATE UNIQUE INDEX ux_equipment_items_serial  ON equipment_items (serial_number)
    WHERE is_active AND serial_number IS NOT NULL;
CREATE UNIQUE INDEX ux_pharmacy_categories_name ON pharmacy_categories (name)         WHERE is_active;   -- (Rev 3)
CREATE UNIQUE INDEX ux_pharmacy_items_name     ON pharmacy_items (name)               WHERE is_active;   -- (Rev 3)
CREATE UNIQUE INDEX ux_patients_nic            ON patients (nic)            WHERE nic IS NOT NULL AND is_active;
CREATE UNIQUE INDEX ux_patients_patient_code   ON patients (patient_code);   -- (Rev 2.11) no is_active scope, and that is deliberate: nobody re-enters a generated code
CREATE UNIQUE INDEX ux_patients_temp_reference  ON patients (temp_reference) WHERE temp_reference IS NOT NULL AND is_active;
CREATE UNIQUE INDEX ux_patients_user_account_id ON patients (user_account_id) WHERE user_account_id IS NOT NULL AND is_active;
CREATE UNIQUE INDEX ux_beds_ward_distance      ON beds (ward_id, nurse_station_distance) WHERE is_active;
```

### Partial unique indexes — business invariants

```sql
-- one live claim per bed, one per admission. A hold and an occupancy both claim the bed,
-- so one pair of indexes covers the 30-minute reservation race too.  (Rev 2.9)
CREATE UNIQUE INDEX ux_bed_assignments_live_bed ON bed_assignments (bed_id)
    WHERE status IN ('reserved', 'occupied');
CREATE UNIQUE INDEX ux_bed_assignments_live_admission ON bed_assignments (admission_id)
    WHERE status IN ('reserved', 'occupied');

-- one OPEN admission per patient  (Rev 2.1: was status = 'Active', which no longer exists.
-- "Open" now spans every state before the patient has left or the visit was called off,
-- so a second admission cannot be opened while one is mid-flight.)
CREATE UNIQUE INDEX ux_admissions_open ON admissions (patient_id)
    WHERE status NOT IN ('discharged', 'cancelled');

-- an ambulance cannot be on two runs
CREATE UNIQUE INDEX ux_dispatch_ambulance ON dispatches (ambulance_id)
    WHERE status IN ('assigned', 'en_route');

-- no duplicate open warning per target  (otherwise every threshold tick inserts one)
CREATE UNIQUE INDEX ux_warnings_open ON warnings (entity_type, entity_id, type)
    WHERE status = 'open';

-- one confirmed allocation per (shift, staff); superseded rows may coexist
CREATE UNIQUE INDEX ux_allocations_confirmed ON allocations (shift_id, staff_member_id)
    WHERE status = 'confirmed';

CREATE UNIQUE INDEX ux_refresh_tokens_hash ON refresh_tokens (token_hash);
CREATE UNIQUE INDEX ux_device_tokens_token ON device_tokens (token);
```

### CHECK constraints

```sql
ALTER TABLE shifts ADD CONSTRAINT ck_shifts_headcount
    CHECK (headcount_needed > 0 AND minimum_headcount > 0
           AND minimum_headcount <= headcount_needed);
ALTER TABLE ward_staffing_rules ADD CONSTRAINT ck_wsr_min CHECK (minimum_headcount > 0);

-- (Rev 3) stock_levels is gone; pharmacy_items carries the quantity now. The check is the
-- last line of defence under the conditional UPDATE that is the real guard.
ALTER TABLE pharmacy_items ADD CONSTRAINT ck_pharmacy_items_quantity
    CHECK (quantity_on_hand >= 0);
ALTER TABLE pharmacy_items ADD CONSTRAINT ck_pharmacy_items_reorder_threshold
    CHECK (reorder_threshold >= 0);
ALTER TABLE pharmacy_transactions ADD CONSTRAINT ck_pharmacy_transactions_quantity
    CHECK (quantity > 0);

ALTER TABLE leave_requests ADD CONSTRAINT ck_leave_dates CHECK (start_date <= end_date);
ALTER TABLE leave_requests ADD CONSTRAINT ck_leave_swap_fields
    CHECK (type = 'shift_swap' OR (swap_with_staff_member_id IS NULL AND swap_shift_id IS NULL));

ALTER TABLE bed_assignments ADD CONSTRAINT ck_bed_assign_window
    CHECK (end_at IS NULL OR end_at > start_at);
ALTER TABLE bed_assignments ADD CONSTRAINT ck_bed_assign_downgrade
    CHECK (NOT is_downgrade OR downgrade_reason IS NOT NULL);

-- admitted_at is set exactly when the patient has actually arrived in a bed  (Rev 2.1)
ALTER TABLE admissions ADD CONSTRAINT ck_admissions_arrival
    CHECK ((status IN ('admitted', 'ready_for_discharge', 'discharged') AND admitted_at IS NOT NULL)
        OR (status IN ('awaiting_bed', 'awaiting_approval', 'bed_reserved', 'cancelled')
            AND admitted_at IS NULL));

-- every patient is identifiable by something  (Rev 2.1, review item 4)
ALTER TABLE patients ADD CONSTRAINT ck_patients_identifiable
    CHECK (nic IS NOT NULL OR phone IS NOT NULL OR temp_reference IS NOT NULL);

-- missing_fields may only name known fields  (Rev 2.1)
ALTER TABLE admissions ADD CONSTRAINT ck_admissions_missing_fields
    CHECK (missing_fields <@ ARRAY['nic','full_name','date_of_birth','phone','address',
                                    'emergency_contact_name','emergency_contact_phone']::text[]);

-- (Rev 2.9) readiness_status is gone; a discharge is confirmed only once every mandatory
-- checklist row is ticked, which is a rule in the service, not a column comparison.

ALTER TABLE route_logs ADD CONSTRAINT ck_route_nonneg
    CHECK (planned_distance_km >= 0 AND planned_duration_minutes >= 0);

ALTER TABLE emergency_calls ADD CONSTRAINT ck_call_coords
    CHECK (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180);
ALTER TABLE ambulances ADD CONSTRAINT ck_amb_coords
    CHECK (current_latitude IS NULL OR
          (current_latitude BETWEEN -90 AND 90 AND current_longitude BETWEEN -180 AND 180));
```

### Non-unique indexes

EF Core indexes every FK by convention, so those are omitted here. The three polymorphic
`(EntityType, EntityId)` pairs have **no FK and therefore no automatic index**, despite
being the columns every audit and workflow lookup filters on.

```sql
CREATE INDEX ix_audit_logs_target      ON audit_logs (entity_type, entity_id, created_at DESC);
CREATE INDEX ix_agent_workflows_target ON agent_workflows (entity_type, entity_id);
CREATE INDEX ix_agent_workflows_corr   ON agent_workflows (correlation_id);
CREATE INDEX ix_warnings_target        ON warnings (entity_type, entity_id);

-- approval queues and dashboards
CREATE INDEX ix_agent_workflows_queue  ON agent_workflows (required_approver_role, created_at DESC)
    WHERE status = 'pending_approval';
CREATE INDEX ix_warnings_open          ON warnings (severity, created_at DESC) WHERE status = 'open';
CREATE INDEX ix_emergency_calls_open   ON emergency_calls (status, created_at DESC);
CREATE INDEX ix_notifications_unread   ON notifications (recipient_staff_member_id, created_at DESC)
    WHERE read_at IS NULL;

-- agent query paths
CREATE INDEX ix_shifts_ward_date       ON shifts (ward_id, date);
CREATE INDEX ix_allocations_staff      ON allocations (staff_member_id) WHERE status = 'confirmed';
CREATE INDEX ix_beds_ward_condition    ON beds (ward_id, condition) WHERE is_active;
CREATE INDEX ix_discharge_items_unticked ON discharge_checklist_items (discharge_id)
    WHERE ticked_at IS NULL;

-- expiry sweep for the 30-minute holds  (Rev 2.9)
CREATE INDEX ix_bed_assignments_reserved_until ON bed_assignments (reserved_until)
    WHERE status = 'reserved';

-- admission status is queried on every read; the open states drive every worklist  (Rev 2.1)
CREATE INDEX ix_admissions_status ON admissions (status, expected_arrival_at)
    WHERE status NOT IN ('discharged', 'cancelled');

-- outstanding-paperwork worklist: "which admissions are still missing a NIC?"  (Rev 2.1)
CREATE INDEX ix_admissions_missing_fields ON admissions USING gin (missing_fields)
    WHERE cardinality(missing_fields) > 0;
```

### Types and mapping notes

- **`DateTimeOffset` → `timestamptz`.** Npgsql **throws** unless `Offset == TimeSpan.Zero`.
  Every write path must use `DateTimeOffset.UtcNow`, never `.Now`. Enforce it in review.
- **Coordinates** are `numeric(9,6)`; without explicit precision EF Core emits bare
  `numeric`. Distance is computed application-side (Haversine) — no PostGIS dependency.
- **`jsonb` columns** need SQL defaults (`'[]'::jsonb`, `'{}'::jsonb`); the non-null
  agent-workflow trace fields are empty at creation.
- **`gen_random_uuid()`** is built in from PostgreSQL 13; on older versions enable
  `pgcrypto`. On PostgreSQL 18+, prefer `uuidv7()` for index locality.
- **FK delete behaviour:** `ON DELETE RESTRICT` to every soft-deletable target;
  `ON DELETE CASCADE` for owned children — `staff_member_skills`, `dispatch_crew`,
  `agent_proposed_changes`, `discharge_checklist_items`, `device_tokens`,
  `refresh_tokens`, `notifications`.
- **`text[]` → `string[]`.** Npgsql maps this natively; no value converter needed.
  `missing_fields` uses a **GIN** index because the queries are containment
  (`'nic' = ANY(...)`), which b-tree cannot serve. *(Rev 2.1)*
- **Generated column** *(Rev 2.1)* — `details_complete` is computed by the database, so it
  can never disagree with `missing_fields`:
  ```sql
  ALTER TABLE admissions ADD COLUMN details_complete boolean
      GENERATED ALWAYS AS (cardinality(missing_fields) = 0) STORED;
  ```
  In EF Core: `.HasComputedColumnSql("cardinality(missing_fields) = 0", stored: true)`.
  Mark it `ValueGeneratedOnAddOrUpdate()` so EF never tries to write it.
- **Enum literals in this document are written snake_case** (`awaiting_bed`, `pending_approval`),
  matching the wire values already published in `patient-spec.yaml` — one vocabulary from
  database to API, no translation layer. C# members stay PascalCase; a single
  `HasConversion` with a snake_case naming policy bridges them. *(Rev 2.1)*
- **Enum storage** (native PG enum vs int vs string) is still an open decision — see below.
  If the group picks `int`, every literal above becomes an ordinal and the CHECK constraints
  need rewriting, which is one more argument for the string option.

---

## Relationship Summary

| Entity A | Entity B | Cardinality | Via |
|----------|----------|-------------|-----|
| StaffMember | Skill | N:M | StaffMemberSkill |
| StaffMember | Shift | N:M | Allocation |
| StaffMember | Dispatch | N:M | DispatchCrew |
| StaffMember | RefreshToken | 1:N | RefreshToken.StaffMemberId |
| StaffMember | DeviceToken | 1:N | DeviceToken.StaffMemberId |
| StaffMember | Notification | 1:N | Notification.RecipientStaffMemberId |
| StaffMember | LeaveRequest | 1:N | LeaveRequest.StaffMemberId |
| Patient | Admission | 1:N | Admission.PatientId |
| Patient | Appointment | 1:N | Appointment.PatientId |
| Patient | EmergencyCall | 1:N (nullable) | EmergencyCall.PatientId |
| EmergencyCall | Admission | 1:N (nullable) | Admission.EmergencyCallId |
| EmergencyCall | Dispatch | 1:N | Dispatch.EmergencyCallId |
| Appointment | Admission | 1:N (nullable) | Admission.AppointmentId |
| Ambulance | Dispatch | 1:N | Dispatch.AmbulanceId |
| Dispatch | RouteLog | 1:1 | RouteLog.DispatchId |
| Ward | Bed | 1:N | Bed.WardId |
| Ward | Shift | 1:N | Shift.WardId |
| Ward | WardStaffingRule | 1:N | WardStaffingRule.WardId |
| Ward | EquipmentItem | 1:N | EquipmentItem.WardId |
| Ward | Dispatch | 1:N (nullable) | Dispatch.DestinationWardId |
| Skill | Shift | 1:N (nullable) | Shift.RequiredSkillId |
| EquipmentCategory | EquipmentItem | 1:N | EquipmentItem.CategoryId *(Rev 3)* |
| PharmacyCategory | PharmacyItem | 1:N | PharmacyItem.CategoryId *(Rev 3)* |
| PharmacyItem | PharmacyTransaction | 1:N | PharmacyTransaction.PharmacyItemId *(Rev 3)* |
| Warning | ActionRequest | 1:N (nullable) | ActionRequest.WarningId *(Rev 3)* |
| AgentWorkflow | ActionRequest | 1:N (nullable) | ActionRequest.WorkflowId *(Rev 3)* |
| AgentWorkflow | Warning | 1:N (nullable) | Warning.WorkflowId *(Rev 3)* |
| Admission | BedAssignment | 1:N | BedAssignment.AdmissionId |
| Admission | Discharge | 1:1 | Discharge.AdmissionId |
| Bed | BedAssignment | 1:N | BedAssignment.BedId |
| Discharge | DischargeChecklistItem | 1:N | DischargeChecklistItem.DischargeId |
| Shift | Allocation | 1:N | Allocation.ShiftId |
| Allocation | Allocation | 1:1 (nullable, self) | Allocation.ReplacedByAllocationId |
| AgentWorkflow | AgentProposedChange | 1:N | AgentProposedChange.AgentWorkflowId |
| AgentWorkflow | AgentWorkflow | 1:N (nullable, self) | AgentWorkflow.ParentWorkflowId |
| AgentWorkflow | BedAssignment | 1:N (nullable) | BedAssignment.WorkflowId |
| Appointment | Admission | 1:1 (nullable) | Appointment.AdmissionId *(Rev 2.9)* |
| LeaveRequest | StaffMember | N:1 (nullable) ×2 | ReviewedBy…, SwapWith… |
| LeaveRequest | Shift | N:1 (nullable) | LeaveRequest.SwapShiftId |
| MaintenanceSchedule | EquipmentItem \| Bed | N:1 (polymorphic) | (AssetType, AssetId) *(Rev 3)* |
| Warning | PharmacyItem \| EquipmentItem \| Bed | N:1 (polymorphic) | (RelatedEntityType, RelatedEntityId) *(Rev 3)* |
| AgentWorkflow | any domain entity | N:1 (polymorphic) | (EntityType, EntityId) |
| AuditLog | any audited entity | N:1 (polymorphic) | (EntityType, EntityId) |

---

## Soft-Delete & History Model

Soft-deletable entities (`SoftDeletableEntity`): `StaffMember`, `Patient`, `Ambulance`,
`EquipmentItem`, `EquipmentCategory`, `PharmacyCategory`, `PharmacyItem`, `Ward`, `Bed`.
*(Rev 3)* `PharmacyTransaction` is deliberately **not** on this list — it is the audit trail,
so nothing withdraws it. These are FK targets of historical
rows (`Allocation`, `Admission`, `Dispatch`, `MaintenanceSchedule`, `BedAssignment`,
etc.) — hard-deleting them would cascade-destroy that history or be blocked by FK
constraints (`ON DELETE RESTRICT`). *(Decision 21)*

Every unique constraint on these tables is scoped `WHERE is_active` — see
[Constraints & Indexes](#constraints--indexes).

All other entities are append-only or status-driven (transitions recorded via an
enum `Status` field, e.g. `CallStatus`, `DispatchStatus`, `AdmissionStatus`) rather
than deleted. *(Rev 2)* `Allocation` moved from delete-driven to status-driven, which
brings it in line with this rule for the first time.

`CreatedAt` is present on every table. `UpdatedAt` is present only on entities whose
rows are mutated after insert; pure join/append-only tables (`DispatchCrew`,
`StaffMemberSkill`, `RefreshToken`, `AuditLog`) only inherit `Entity`. *(Decision 22)*

---

## Database Schema Mapping

| Entity | Table | Soft-Deletable | Rev 2 |
|--------|-------|:---:|:---:|
| StaffMember | staff_members | ✓ | |
| Skill | skills | | |
| StaffMemberSkill | staff_member_skills | | changed |
| RefreshToken | refresh_tokens | | |
| DeviceToken | device_tokens | | **new** |
| EmergencyCall | emergency_calls | | |
| Ambulance | ambulances | ✓ | |
| Dispatch | dispatches | | changed |
| DispatchCrew | dispatch_crew | | |
| RouteLog | route_logs | | |
| WardStaffingRule | ward_staffing_rules | | **new** |
| Shift | shifts | | changed |
| Allocation | allocations | | changed |
| LeaveRequest | leave_requests | | changed |
| EquipmentCategory | equipment_categories | ✓ | **renamed (3)** |
| EquipmentItem | equipment_items | ✓ | changed (3) |
| PharmacyCategory | pharmacy_categories | ✓ | **new (3)** |
| PharmacyItem | pharmacy_items | ✓ | **new (3)** |
| PharmacyTransaction | pharmacy_transactions | | **new (3)** |
| MaintenanceSchedule | maintenance_schedules | | changed (3) |
| Warning | warnings | | changed (3) |
| ActionRequest | action_requests | | **new (3), not built** |
| Ward | wards | ✓ | changed |
| Bed | beds | ✓ | changed |
| Patient | patients | ✓ | changed |
| PatientAccount | patient_accounts | ✓ | **new (2.5)** |
| Appointment | appointments | | **new** |
| Admission | admissions | | changed |
| BedAssignment | bed_assignments | | changed |
| Discharge | discharges | | changed |
| DischargeChecklistItem | discharge_checklist_items | | **new** |
| AgentWorkflow | agent_workflows | | changed |
| AgentProposedChange | agent_proposed_changes | | **new** |
| Notification | notifications | | **new** |
| AuditLog | audit_logs | | changed |

**34 tables** (was 25). *(Rev 2.5 added `PatientAccount`. Rev 2.6 added `CareRecommendation`.)*

---

## Component Ownership

| Component | Owner | Entities |
|-----------|-------|----------|
| Emergency / Ambulance | Member 1 | EmergencyCall, Ambulance, Dispatch, DispatchCrew, RouteLog |
| Staff Management | Member 2 | Shift, Allocation, LeaveRequest, Skill, StaffMemberSkill, WardStaffingRule |
| Health Equipment | Member 3 | EquipmentCategory, EquipmentItem, **Bed**, PharmacyCategory, PharmacyItem, PharmacyTransaction, MaintenanceSchedule, Warning, ActionRequest |
| Patient Management | Member 4 | Patient, **PatientAccount**, Admission, BedAssignment, Discharge, DischargeChecklistItem, Appointment, Ward |
| Common | Group (common) | StaffMember, **PatientAccount**, RefreshToken, DeviceToken, Notification, AgentWorkflow, AgentProposedChange, AuditLog |

**Common means built once, not four times.** *(2026-09-07)* Anything that is not a
specific member's is common: auth and the JWT, the `DbContext` and base classes, the
exception handler, the audit interceptor, the agent workflow tables and the Coordinator
Agent. Contract: `specs/common-spec.yaml`. Reasoning: `docs/ADR.md` ADR 3.

**Note:** `Ward` sits under Patient Management but is referenced by all four components
(`Shift.WardId`, `EquipmentItem.WardId`, `Dispatch.DestinationWardId`). *(Rev 3)* Pharmacy
stock is central, so `PharmacyItem` has no `WardId`.
Treat its schema as frozen once agreed — changes to it break three other members.

**Note (Rev 2.2 — review item 3):** `Bed` moved from Patient Management to Health
Equipment. Rev 2.1 listed it under Member 4, which contradicted three documents that had
already settled it the other way: `integration_of_functions.md` §3 and §6.1 (marked
**DECIDED**), `equipment-management-plan.md` §13.1, and `patient-management-plan.md`
("owned by Equipment Management (Member 3). We read it, we never write it.").
The split is: **Equipment owns the frame** — it exists, its number, its condition, repairs
and retirement. **Patient Management owns the occupant** — `BedAssignment` and
`BedAssignment`, which carries both the hold and the occupancy *(Rev 2.9)*. Neither writes
the other's table.

---

## Open Decisions

These need a group call before implementation. Each one changes work already scoped.

**1. Patient identity — RESOLVED (Rev 2.3): patients do get an app login.**

*The premise was out of date.* Rev 2.1 recorded this as "the Component Plan's Flutter
roles are crew / nurse / staff only — no patient". Component Plan v2 does list a Patient
role: *"**Patient** | Flutter | Member 4 | Book a visit, view own admission status,
ward/bed and discharge details. Read-only, own record only"*, and counts it as one of the
four Flutter roles. `patient-management-plan.md` §10 lists the Patient screens, and
`patient-spec.yaml` already publishes three Patient-role endpoints
(`POST /me/pre-register`, `GET /me/admission`, `GET /me/history`). The documents agree;
this entry did not.

*Why it stays.* The course's own worked example — the *Assignment 1 sample project*
handout (AutoCare AI), issued with the brief — splits the two clients exactly this way:

> "The React application will be used mainly by **staff**"
> "The Flutter application will support **customers** and technicians"

"Customer" is role 1 of 5 in that sample — the end user of the service, our `Patient`
equivalent — and the Flutter feature list it gives includes "registration, login and
logout", "date and time selection" and "history and status tracking". Those are
`/me/pre-register`, the booking date picker and `/me/admission` under different names. A
staff-only mobile app is not the shape the example demonstrates.

Assignment §4.1 points the same way: **"meaningful and different purposes for the React
and Flutter applications."** With patients in Flutter that difference is self-evident —
React is the hospital's internal system, Flutter is the app the public uses. Staff-only on
both sides leaves it to be argued from posture alone, desk work versus walking around.

*On the cost.* A second auth path and patient-scoped authorization on every `/me/*`
endpoint is real work, and Rev 2.1 was right to raise it. Two things temper it. The sample
notes that **"user management should be implemented as a shared mandatory feature"** and
that "authentication and role-based authorization are already compulsory requirements" —
so this sits inside a baseline the project owes anyway, rather than being net-new scope.
And Rev 2.1's other point, that the three-roles minimum is already met without patients,
is correct on its own terms; it just is not the requirement that decides this.

There is also a demo cost to removing them. Our emergency path leans on the contrast
between a **logged-in caller** whose identity, history and contact details we already hold,
and an unidentified arrival registered as `UNKNOWN-2026-0142`. Without patient accounts
the first half of that contrast disappears.

**Knock-on changes this creates, still to do:**

- `Notification` and `DeviceToken` are written staff-only. Both need a nullable `PatientId`
  and a polymorphic recipient. *Owner: group / leader.*
- `Appointment.BookedByStaffMemberId` stays nullable — it is null for a self-booking.
  *Already modelled correctly (Rev 2).*
- `patient-spec.yaml` now publishes the booking endpoints: `POST /me/appointments`,
  `GET /me/appointments`, `POST /me/appointments/{id}/cancel`, and the staff side
  `GET /appointments`, `POST /appointments`, `POST /appointments/{id}/check-in`.
  Check-in creates an ordinary `Admission` with `source = pre_registered`, so the bed agent
  path is unchanged. Doctor calendars, time slots and availability search remain out of
  scope. *Done: Member 4.*

Note for Member 4's own design: patient notifications are local (the app checks its own
status), not push, so `DeviceToken` is not on the Patient Management critical path either
way.

**2. Single hospital vs. the "non-nearest hospital" approval trigger.** This document
settles on one hospital with multiple wards, but the Component Plan says the Duty Manager
approves when the plan "sends the patient to a hospital other than the nearest one" — an
unreachable branch. The reassignment trigger still works, so the approval demo survives.
Fix the Component Plan wording, or introduce a `Hospital` entity.

**3. Enum storage strategy — RESOLVED (2026-09-07): `HasConversion<string>()` plus a
CHECK constraint,** stored `snake_case` to match the wire values the specs publish. Full
reasoning and consequences are **ADR 5** in `docs/ADR.md`. Original entry kept below.

Native PostgreSQL enum vs `int` vs `HasConversion<string>()`. Rev 2 adds nine enums and changes four existing ones — with
native PG enums each of those is an `ALTER TYPE` that EF Core migrations handle awkwardly.
*Recommendation:* `HasConversion<string>()` plus a CHECK constraint. Readable in `psql`,
trivial to extend, and the CHECK preserves integrity. This is ADR-worthy.

**4. `Skill` soft-deletability.** `Skill` is an admin-editable lookup table and an FK
target of `StaffMemberSkill` and now `Shift` — the exact profile Decision 21 covers, and
the same profile as `EquipmentCategory` *(Rev 3)*, which *is* soft-deletable. It should almost certainly
extend `SoftDeletableEntity` too. Left unchanged pending sign-off.

**5. `StaffMember.Department: string` alongside a `Ward` entity.** Free-text department
makes "which staff cover this ward" unqueryable — which is what the Staff Allocation Agent
needs. Either FK it to `Ward` or drop it.

**6. Night shifts.** `Shift` splits `Date` + `StartTime` + `EndTime`, so a 22:00–06:00
shift has `EndTime < StartTime`. Rev 2 documents the roll-over rule on the entity, but
overlap detection stays fiddly. Consider `StartAt`/`EndAt` as `timestamptz` instead.

**7. Triple status bookkeeping.** `CallStatus`, `DispatchStatus` and `AmbulanceStatus` all
carry `EnRoute` — three rows to keep in sync on every transition. Pick one as
authoritative and derive the rest, or write down the sync rule.

**8. Decision log.** This document cites 37 numbered decisions but no log exists in the
repo, and decisions 1, 3, 5, 28 and 37 are never cited. For a submission graded on
traceable documentation, commit the log or inline the rationale.

**9. Where does `Cleaning` live?** *(Rev 2.2)* Rev 2's `BedStatus` had a `Cleaning` value;
`BedCondition` does not, and neither published spec has anywhere to record it. A bed being
turned over between patients is a real state and somebody owns it. Options: a third
`BedCondition` value (Equipment's, but Patient triggers it at discharge), or a short-lived
`BedAssignment` row held by nobody *(Rev 2.9)*. Member 3 decides, since it is their table.

**10. Is `Referral` a fourth admission source?** *(Rev 2.2)* Rev 2 proposed
`{Emergency, WalkIn, Booked, Referral}`; `patient-spec.yaml` publishes three and has no
`referral`. A patient referred from another clinic is plausibly distinct from a walk-in.
Member 4 decides — adding it means changing a committed enum, so it is not a diagram-only
change.

**12. ~~Health Equipment entities are stale in this diagram.~~** **CLOSED** *(Rev 3,
2026-09-11, Member 3.)* `EquipmentType` is now `EquipmentCategory`, `StockLevel` is absorbed
into `PharmacyItem`, and `PharmacyCategory`, `PharmacyItem`, `PharmacyTransaction` and
`ActionRequest` are modelled. The enums, the unique indexes, the check constraints, the
relationship table, the soft-delete list and the table-name mapping were all brought in line
at the same time. Eight of the nine entities are live on `main`; `ActionRequest` is modelled
from the spec and not built yet, which the section says on its face.

> One thing this closure did **not** settle, and it is a real gap rather than tidiness. The
> Rev 2 partial unique index on open warnings — `(EntityType, EntityId, Type) WHERE Status =
> 'Open'` — is not in the shipped schema. It exists to stop the threshold sweep inserting a
> duplicate open warning on every tick, and the sweep is not built yet. It has to come back
> with it.

**11. Is Equipment one role or two?** *(Rev 2.2)* This diagram and `staff-spec.yaml` carry
a single `EquipmentManager`. `equipment-management-plan.md` §2 is written around two
distinct capabilities — **Inventory Administrator** (React: approves actions, manages
stock and the bed register) and **Equipment Technician** (Flutter: scans tags, completes
services, reports faults) — with different endpoint permissions for each. One role cannot
express that split. Members 2 and 3 to settle.
