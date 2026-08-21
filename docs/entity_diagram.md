# Entity Class Diagram

Reflects all decisions settled during the schema-design grilling session (37 decisions,
single hospital / multiple wards, unified staff identity, generic agent-workflow and
audit-log schemas). PKs are `Guid` (PostgreSQL `uuid`, `default: gen_random_uuid()`)
throughout.

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

#### RefreshToken extends Entity
```
+ StaffMemberId: Guid (non-null) FK → StaffMember.Id
+ TokenHash: string (unique, non-null)
+ ExpiresAt: DateTimeOffset (non-null)
+ RevokedAt: DateTimeOffset (nullable)
```
**Table:** `refresh_tokens`
**Note:** Persisted so sessions can be revoked server-side (compromised device,
terminated staff) rather than relying on stateless JWT expiry alone. *(Decision 36)*
`RevokedAt` is the one post-insert mutation; the row is otherwise append-only, so it
stays on `Entity` rather than gaining an `UpdatedAt` that would duplicate `RevokedAt`.

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

#### EquipmentType extends SoftDeletableEntity
```
+ Name: string (unique, non-null)
```
**Table:** `equipment_types`
**Note:** Shared taxonomy used by both durable `EquipmentItem` rows and aggregate
`StockLevel` rows, giving the Equipment Monitoring Agent one vocabulary for
"required equipment" across both. *(Decision 33)*

#### EquipmentItem extends SoftDeletableEntity
```
+ EquipmentTypeId: Guid (non-null) FK → EquipmentType.Id
+ WardId: Guid (non-null) FK → Ward.Id
+ SerialNumber: string (unique, nullable)
+ Status: EquipmentItemStatus (non-null)
```
**Table:** `equipment_items`
**Note:** Durable, individually-tracked assets (ventilators, monitors). *(Decision 8)*

#### StockLevel extends AuditedEntity
```
+ EquipmentTypeId: Guid (non-null) FK → EquipmentType.Id
+ WardId: Guid (non-null) FK → Ward.Id
+ Quantity: int (non-null)
+ ReorderThreshold: int (non-null)
```
**Table:** `stock_levels`
**Constraint:** UNIQUE(EquipmentTypeId, WardId)
**Note:** Aggregate count for bulk/consumable items (bandages, syringes) — not
individually serialized. *(Decisions 8, 33)*

#### MaintenanceSchedule extends AuditedEntity
```
+ EquipmentItemId: Guid (non-null) FK → EquipmentItem.Id
+ DueDate: DateOnly (non-null)
+ CompletedDate: DateOnly (nullable)
+ Status: MaintenanceStatus (non-null)
+ Notes: string (nullable)
```
**Table:** `maintenance_schedules`
**Note:** Event log (one row per service/inspection), not a single recurring-schedule
row — "next due" is derived as the latest open row. Doubles as service history.
*(Decision 24)*

#### Warning extends AuditedEntity
```
+ EntityType: string (non-null) -- "EquipmentItem" | "StockLevel"
+ EntityId: Guid (non-null) -- polymorphic target id
+ Type: WarningType (non-null)
+ Severity: WarningSeverity (non-null)
+ Status: WarningStatus (non-null)
+ ResolvedAt: DateTimeOffset (nullable)
```
**Table:** `warnings`
**Constraint:** UNIQUE(EntityType, EntityId, Type) **WHERE Status = 'Open'** *(Rev 2)*
**Note:** Deterministic/system-detected (threshold check), not agent-created. The
Equipment Monitoring Agent reviews open `Warning` rows and produces a prioritised
`AgentWorkflow` recommendation pointing back at one via `(EntityType, EntityId)`.
*(Decisions 17, 34)*
*(Rev 2)* `DetectedAt` removed — it duplicated the inherited `CreatedAt` exactly. The
partial UNIQUE stops the threshold job inserting a duplicate open warning on every tick.

---

### Patient Management

#### Ward extends SoftDeletableEntity
```
+ Name: string (unique, non-null)
+ Type: WardType (non-null)                             -- (Rev 2.2: was AdmissionCategory)
+ GenderPolicy: WardGenderPolicy (non-null)             -- (Rev 2)
```
**Table:** `wards`
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
Occupancy is instead the presence of a live `BedAssignment` (`EndAt IS NULL`) and a hold is a
`BedReservation` with `Status = Held`:

```
"Is bed 12 free?"
    = it exists in Equipment's register        (M3's data, M4 reads)
    AND condition = 'usable'                   (M3's data, M4 reads)
    AND no live BedAssignment references it    (M4's data)
```

Two reads, zero shared writes. `Cleaning` had no home in either published spec and is
carried to Open Decisions as item 9 rather than dropped silently.

#### BedReservation extends AuditedEntity *(Rev 2 — new)*
```
+ BedId: Guid (non-null) FK → Bed.Id
+ AdmissionId: Guid (nullable) FK → Admission.Id
+ AgentWorkflowId: Guid (nullable) FK → AgentWorkflow.Id
+ ExpiresAt: DateTimeOffset (non-null)
+ Status: BedReservationStatus (non-null)
+ ReleasedReason: string (nullable)
```
**Table:** `bed_reservations`
**Constraint:** UNIQUE(BedId) **WHERE Status = 'Held'**
**Note:** *(Rev 2)* Implements "picks one and **holds it for 30 minutes**". This is the
concurrency-critical piece of the patient flow: without a real row and a partial unique
index, two workflows can hold the same bed and the second approval silently overwrites
the first. `AgentWorkflowId` is nullable because a hold is also created on the
reject-and-override path, where a human picks the bed and no agent proposal exists.
A background sweep moves `Held` rows past `ExpiresAt` to `Expired` and returns the bed to
`Available`.

#### Patient extends SoftDeletableEntity
```
+ FirstName: string (non-null)
+ LastName: string (non-null)
+ DateOfBirth: DateOnly (nullable)
+ NationalId: string (nullable, unique when present)
+ TempReference: string (nullable, unique when present)  -- (Rev 2.1)
+ Gender: Gender (nullable)                             -- (Rev 2: was string)
+ PhoneNumber: string (nullable)
+ EmergencyContactName: string (nullable)
+ EmergencyContactPhone: string (nullable)
+ UserAccountId: Guid (nullable, unique when present) FK → PatientAccount.Id  -- (Rev 2.5 — new)
```
**Table:** `patients`
**Constraints:**
- `UNIQUE (national_id) WHERE national_id IS NOT NULL`
- `UNIQUE (temp_reference) WHERE temp_reference IS NOT NULL` *(Rev 2.1)*
- `UNIQUE (user_account_id) WHERE user_account_id IS NOT NULL` *(Rev 2.5)*
- `CHECK (national_id IS NOT NULL OR phone_number IS NOT NULL OR temp_reference IS NOT NULL)` *(Rev 2.1)*

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

#### Appointment extends AuditedEntity *(Rev 2 — new)*
```
+ PatientId: Guid (non-null) FK → Patient.Id
+ ScheduledAt: DateTimeOffset (non-null)
+ Category: AdmissionCategory (non-null)
+ WardId: Guid (nullable) FK → Ward.Id
+ Status: AppointmentStatus (non-null)
+ BookedByStaffMemberId: Guid (nullable) FK → StaffMember.Id
+ Notes: string (nullable)
```
**Table:** `appointments`
**Note:** *(Rev 2)* Covers the patient flow's third arrival path — "they booked a visit
beforehand". Previously an `Admission` could only be emergency-linked or unexplained.
On check-in this becomes an `Admission` with `Source = Booked`.
`BookedByStaffMemberId` is nullable and reserved for the self-booking case; see
[Open Decisions](#open-decisions) for whether patients book directly.

#### Admission extends AuditedEntity
```
+ PatientId: Guid (non-null) FK → Patient.Id
+ EmergencyCallId: Guid (nullable) FK → EmergencyCall.Id
+ AppointmentId: Guid (nullable) FK → Appointment.Id    -- (Rev 2)
+ Source: AdmissionSource (non-null)                    -- (Rev 2)
+ Category: AdmissionCategory (non-null)
+ Urgency: AdmissionUrgency (non-null)                  -- (Rev 2.2: was AcuityLevel)
+ IsInfectious: bool = false (non-null)                 -- (Rev 2.2: was RequiresIsolation)
+ CategorySetByStaffMemberId: Guid (non-null) FK → StaffMember.Id  -- (Rev 2.2)
+ CategorySetAt: DateTimeOffset (non-null)              -- (Rev 2.2)
+ ExpectedArrivalAt: DateTimeOffset (nullable)          -- (Rev 2)
+ AdmittedAt: DateTimeOffset (nullable)                 -- (Rev 2: was non-null)
+ Status: AdmissionStatus (non-null)                    -- (Rev 2.1: now 7 states, stored)
+ MissingFields: text[] (non-null, default '{}')        -- (Rev 2.2: was MissingDetails)
+ DetailsComplete: bool (GENERATED, stored)             -- (Rev 2.1)
+ DetailsCompletedAt: DateTimeOffset (nullable)         -- (Rev 2.1)
+ CancelReason: CancelReason (nullable)                 -- (Rev 2.2)
+ DispatchId: Guid (nullable) FK → Dispatch.Id           -- (Rev 2.5 — new)
+ ReportedByUserId: Guid (nullable) FK → PatientAccount.Id -- (Rev 2.5 — new)
```
**Table:** `admissions`
**Note:** `EmergencyCallId` nullable — null for walk-in/referral admissions, set for
the emergency-originated path. *(Decision 25)*
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
returns. Deriving them from `BedReservation.Status` + `BedAssignment.EndAt` +
`Discharge.ReadinessStatus` was the alternative; it was rejected for one decisive reason:

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
`{national_id, date_of_birth}` does. Values come from `PatientDetailField`; queryable with
`WHERE 'national_id' = ANY(missing_fields)`, which is the outstanding-paperwork worklist.

`DetailsComplete` is a **stored generated column**, `cardinality(missing_fields) = 0` — the
one place in this schema where a computed value is right, because unlike `Status` it has no
transition rules to enforce and cannot drift by construction. `DetailsCompletedAt` records
when the gap closed, which answers "how long did we hold an unidentified patient".

Deliberately **separate from `Status`**: how far through their stay a patient is and how
complete their paperwork is are independent facts. A fully-admitted ICU patient can still be
missing a NIC, and collapsing the two would make one unrepresentable.

#### BedAssignment extends AuditedEntity
```
+ AdmissionId: Guid (non-null) FK → Admission.Id
+ BedId: Guid (non-null) FK → Bed.Id
+ StartAt: DateTimeOffset (non-null)
+ EndAt: DateTimeOffset (nullable)
+ IsDowngrade: bool = false (non-null)                  -- (Rev 2)
+ DowngradeReason: string (nullable)                    -- (Rev 2)
+ AssignedByStaffMemberId: Guid (nullable) FK → StaffMember.Id  -- (Rev 2)
+ AgentWorkflowId: Guid (nullable) FK → AgentWorkflow.Id -- (Rev 2)
```
**Table:** `bed_assignments`
**Constraints:** UNIQUE(BedId) WHERE EndAt IS NULL; UNIQUE(AdmissionId) WHERE EndAt IS NULL *(Rev 2)*
**Note:** Multiple rows per `Admission` (ward/bed transfers mid-stay); exactly one
active row (`EndAt IS NULL`) at a time. *(Decision 19)*
*(Rev 2)* That "exactly one" rule was prose only — two concurrent requests could both
succeed. It is now enforced by two partial unique indexes. `IsDowngrade` records the
flow's "if ICU is full it offers the next best thing, **flagged as a downgrade**"; it is a
real column rather than a note in the workflow payload because it *routes the approval*
(downgrades are Duty Manager only), so it must be queryable and auditable.

#### Discharge extends AuditedEntity
```
+ AdmissionId: Guid (unique, non-null) FK → Admission.Id
+ ReadinessStatus: DischargeReadinessStatus (non-null)
+ DischargedAt: DateTimeOffset (nullable)
+ DischargeSummary: string (nullable)
+ DischargedByStaffMemberId: Guid (nullable) FK → StaffMember.Id
```
**Table:** `discharges`
**Note:** 1:1 companion row created **at admission time** (`ReadinessStatus = NotReady`),
not only once discharge actually happens — this gives clinical staff somewhere to
update readiness during the stay, and gives the Patient Admission & Bed Agent a
persistent target to monitor. `DischargedAt`/`DischargeSummary` stay null until
confirmed. *(Decisions 10, 32)*
*(Rev 2)* `ReadinessStatus` is now **derived**, not directly edited: it becomes `Ready`
once every `DischargeChecklistItem` for this discharge is complete. The "ready to go"
list is a query over this field.

#### DischargeChecklistItem extends AuditedEntity *(Rev 2 — new)*
```
+ DischargeId: Guid (non-null) FK → Discharge.Id
+ ItemType: DischargeChecklistItemType (non-null)
+ CompletedAt: DateTimeOffset (nullable)
+ CompletedByStaffMemberId: Guid (nullable) FK → StaffMember.Id
+ Notes: string (nullable)
```
**Table:** `discharge_checklist_items`
**Constraint:** UNIQUE(DischargeId, ItemType)
**Note:** *(Rev 2)* The patient flow's step 7 is "staff tick a checklist (doctor's
clearance, medicine, bill settled) → all ticked → shows on a 'ready to go' list". A single
`ReadinessStatus` enum could not represent three independently tickable items or record
who ticked each one. Rows are seeded alongside the `Discharge` row at admission time.

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
**`Allocation`** *(Rev 2)*, `LeaveRequest`, `EquipmentItem`, `StockLevel`,
`MaintenanceSchedule`, `Patient`, `Admission`, **`BedAssignment`** *(Rev 2)*,
**`BedReservation`** *(Rev 2)*, `Discharge`, `Ward`, `Bed`, `AgentWorkflow`.
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

### EquipmentItemStatus
```
Operational, UnderMaintenance, OutOfService
```

### MaintenanceStatus
```
Scheduled, Completed, Overdue
```

### WarningType
```
LowStock, OverdueMaintenance
```

### WarningSeverity
```
Low, Medium, High, Critical
```

### WarningStatus
```
Open, Resolved
```

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

### WardGenderPolicy *(Rev 2 — new)*
```
Male, Female, Mixed
```

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
Management's (`BedAssignment`), `Reserved` is Patient Management's (`BedReservation`), and
only `Maintenance` was ever Equipment's. `Condition` now carries the Equipment fact alone;
the other two are read from their owners' rows. See the `Bed` note and
`integration_of_functions.md` §6.1. `Cleaning` is Open Decision 7.

### BedReservationStatus *(Rev 2 — new)*
```
Held, Confirmed, Expired, Released
```

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
| `BedReserved` | Approved and held (`BedReservation.Status = Held`); patient not yet in it. | null |
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

### PatientDetailField *(Rev 2.1 — new, review item 4)*
```
NationalId, DateOfBirth, Gender, PhoneNumber,
EmergencyContactName, EmergencyContactPhone, Address
```
The allowed members of `Admission.MissingFields`. A closed set rather than free text, so
"what is still outstanding" can be counted and filtered instead of parsed.

### DischargeReadinessStatus
```
NotReady, PendingReview, Ready
```

### DischargeChecklistItemType *(Rev 2 — new)*
```
DoctorClearance, MedicationDispensed, BillSettled
```

### AgentType
```
DispatchRouting, StaffAllocation, EquipmentMonitoring, PatientAdmissionBed
```

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
CreateDispatch, TransferEquipment, CreateMaintenanceSchedule
```

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
CREATE UNIQUE INDEX ux_ambulances_reg          ON ambulances (registration_number)   WHERE is_active;
CREATE UNIQUE INDEX ux_wards_name              ON wards (name)                       WHERE is_active;
CREATE UNIQUE INDEX ux_beds_ward_number        ON beds (ward_id, bed_number)         WHERE is_active;
CREATE UNIQUE INDEX ux_equipment_types_name    ON equipment_types (name)             WHERE is_active;
CREATE UNIQUE INDEX ux_equipment_items_serial  ON equipment_items (serial_number)
    WHERE is_active AND serial_number IS NOT NULL;
CREATE UNIQUE INDEX ux_patients_national_id    ON patients (national_id)             WHERE national_id IS NOT NULL;
CREATE UNIQUE INDEX ux_patients_temp_ref       ON patients (temp_reference)          WHERE temp_reference IS NOT NULL;
CREATE UNIQUE INDEX ux_beds_ward_distance      ON beds (ward_id, nurse_station_distance) WHERE is_active;
```

### Partial unique indexes — business invariants

```sql
-- one occupant per bed, one bed per admission
CREATE UNIQUE INDEX ux_bed_assign_bed   ON bed_assignments (bed_id)       WHERE end_at IS NULL;
CREATE UNIQUE INDEX ux_bed_assign_adm   ON bed_assignments (admission_id) WHERE end_at IS NULL;

-- one live hold per bed  (the 30-minute reservation race)
CREATE UNIQUE INDEX ux_bed_reservation  ON bed_reservations (bed_id)      WHERE status = 'held';

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

ALTER TABLE stock_levels ADD CONSTRAINT ck_stock_nonneg
    CHECK (quantity >= 0 AND reorder_threshold >= 0);

ALTER TABLE leave_requests ADD CONSTRAINT ck_leave_dates CHECK (start_date <= end_date);
ALTER TABLE leave_requests ADD CONSTRAINT ck_leave_swap_fields
    CHECK (type = 'shift_swap' OR (swap_with_staff_member_id IS NULL AND swap_shift_id IS NULL));

ALTER TABLE bed_assignments ADD CONSTRAINT ck_bed_assign_window
    CHECK (end_at IS NULL OR end_at > start_at);
ALTER TABLE bed_assignments ADD CONSTRAINT ck_bed_assign_downgrade
    CHECK (NOT is_downgrade OR downgrade_reason IS NOT NULL);

ALTER TABLE bed_reservations ADD CONSTRAINT ck_bed_res_expiry CHECK (expires_at > created_at);

-- admitted_at is set exactly when the patient has actually arrived in a bed  (Rev 2.1)
ALTER TABLE admissions ADD CONSTRAINT ck_admissions_arrival
    CHECK ((status IN ('admitted', 'ready_for_discharge', 'discharged') AND admitted_at IS NOT NULL)
        OR (status IN ('awaiting_bed', 'awaiting_approval', 'bed_reserved', 'cancelled')
            AND admitted_at IS NULL));

-- every patient is identifiable by something  (Rev 2.1, review item 4)
ALTER TABLE patients ADD CONSTRAINT ck_patients_identifiable
    CHECK (national_id IS NOT NULL OR phone_number IS NOT NULL OR temp_reference IS NOT NULL);

-- missing_fields may only name known fields  (Rev 2.1)
ALTER TABLE admissions ADD CONSTRAINT ck_admissions_missing_fields
    CHECK (missing_fields <@ ARRAY['national_id','date_of_birth','gender','phone_number',
                                    'emergency_contact_name','emergency_contact_phone','address']::text[]);

ALTER TABLE discharges ADD CONSTRAINT ck_discharge_ready
    CHECK (discharged_at IS NULL OR readiness_status = 'ready');

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
CREATE INDEX ix_discharges_ready       ON discharges (readiness_status)
    WHERE discharged_at IS NULL;

-- expiry sweep for the 30-minute holds
CREATE INDEX ix_bed_reservations_expiry ON bed_reservations (expires_at) WHERE status = 'held';

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
  (`'national_id' = ANY(...)`), which b-tree cannot serve. *(Rev 2.1)*
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
| Ward | StockLevel | 1:N | StockLevel.WardId |
| Ward | Dispatch | 1:N (nullable) | Dispatch.DestinationWardId |
| Skill | Shift | 1:N (nullable) | Shift.RequiredSkillId |
| EquipmentType | EquipmentItem | 1:N | EquipmentItem.EquipmentTypeId |
| EquipmentType | StockLevel | 1:N | StockLevel.EquipmentTypeId |
| EquipmentItem | MaintenanceSchedule | 1:N | MaintenanceSchedule.EquipmentItemId |
| Admission | BedAssignment | 1:N | BedAssignment.AdmissionId |
| Admission | BedReservation | 1:N (nullable) | BedReservation.AdmissionId |
| Admission | Discharge | 1:1 | Discharge.AdmissionId |
| Bed | BedAssignment | 1:N | BedAssignment.BedId |
| Bed | BedReservation | 1:N | BedReservation.BedId |
| Discharge | DischargeChecklistItem | 1:N | DischargeChecklistItem.DischargeId |
| Shift | Allocation | 1:N | Allocation.ShiftId |
| Allocation | Allocation | 1:1 (nullable, self) | Allocation.ReplacedByAllocationId |
| AgentWorkflow | AgentProposedChange | 1:N | AgentProposedChange.AgentWorkflowId |
| AgentWorkflow | AgentWorkflow | 1:N (nullable, self) | AgentWorkflow.ParentWorkflowId |
| AgentWorkflow | BedAssignment | 1:N (nullable) | BedAssignment.AgentWorkflowId |
| LeaveRequest | StaffMember | N:1 (nullable) ×2 | ReviewedBy…, SwapWith… |
| LeaveRequest | Shift | N:1 (nullable) | LeaveRequest.SwapShiftId |
| Warning | EquipmentItem \| StockLevel | N:1 (polymorphic) | (EntityType, EntityId) |
| AgentWorkflow | any domain entity | N:1 (polymorphic) | (EntityType, EntityId) |
| AuditLog | any audited entity | N:1 (polymorphic) | (EntityType, EntityId) |

---

## Soft-Delete & History Model

Soft-deletable entities (`SoftDeletableEntity`): `StaffMember`, `Patient`, `Ambulance`,
`EquipmentItem`, `EquipmentType`, `Ward`, `Bed`. These are FK targets of historical
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
| EquipmentType | equipment_types | ✓ | |
| EquipmentItem | equipment_items | ✓ | |
| StockLevel | stock_levels | | |
| MaintenanceSchedule | maintenance_schedules | | |
| Warning | warnings | | changed |
| Ward | wards | ✓ | changed |
| Bed | beds | ✓ | changed |
| BedReservation | bed_reservations | | **new** |
| Patient | patients | ✓ | changed |
| Appointment | appointments | | **new** |
| Admission | admissions | | changed |
| BedAssignment | bed_assignments | | changed |
| Discharge | discharges | | changed |
| DischargeChecklistItem | discharge_checklist_items | | **new** |
| AgentWorkflow | agent_workflows | | changed |
| AgentProposedChange | agent_proposed_changes | | **new** |
| Notification | notifications | | **new** |
| AuditLog | audit_logs | | changed |

**32 tables** (was 25).

---

## Component Ownership

| Component | Owner | Entities |
|-----------|-------|----------|
| Emergency / Ambulance | Member 1 | EmergencyCall, Ambulance, Dispatch, DispatchCrew, RouteLog |
| Staff Management | Member 2 | Shift, Allocation, LeaveRequest, Skill, StaffMemberSkill, WardStaffingRule |
| Health Equipment | Member 3 | EquipmentType, EquipmentItem, StockLevel, MaintenanceSchedule, Warning, **Bed** |
| Patient Management | Member 4 | Patient, Admission, BedAssignment, BedReservation, Discharge, DischargeChecklistItem, Appointment, Ward |
| Shared / Group | All | StaffMember, RefreshToken, DeviceToken, Notification, AgentWorkflow, AgentProposedChange, AuditLog |

**Note:** `Ward` sits under Patient Management but is referenced by all four components
(`Shift.WardId`, `EquipmentItem.WardId`, `StockLevel.WardId`, `Dispatch.DestinationWardId`).
Treat its schema as frozen once agreed — changes to it break three other members.

**Note (Rev 2.2 — review item 3):** `Bed` moved from Patient Management to Health
Equipment. Rev 2.1 listed it under Member 4, which contradicted three documents that had
already settled it the other way: `integration_of_functions.md` §3 and §6.1 (marked
**DECIDED**), `equipment-management-plan.md` §13.1, and `patient-management-plan.md`
("owned by Equipment Management (Member 3). We read it, we never write it.").
The split is: **Equipment owns the frame** — it exists, its number, its condition, repairs
and retirement. **Patient Management owns the occupant** — `BedAssignment` and
`BedReservation`. Neither writes the other's table.

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

**3. Enum storage strategy.** Native PostgreSQL enum vs `int` vs
`HasConversion<string>()`. Rev 2 adds nine enums and changes four existing ones — with
native PG enums each of those is an `ALTER TYPE` that EF Core migrations handle awkwardly.
*Recommendation:* `HasConversion<string>()` plus a CHECK constraint. Readable in `psql`,
trivial to extend, and the CHECK preserves integrity. This is ADR-worthy.

**4. `Skill` soft-deletability.** `Skill` is an admin-editable lookup table and an FK
target of `StaffMemberSkill` and now `Shift` — the exact profile Decision 21 covers, and
the same profile as `EquipmentType`, which *is* soft-deletable. It should almost certainly
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
`BedReservation`-style row. Member 3 decides, since it is their table.

**10. Is `Referral` a fourth admission source?** *(Rev 2.2)* Rev 2 proposed
`{Emergency, WalkIn, Booked, Referral}`; `patient-spec.yaml` publishes three and has no
`referral`. A patient referred from another clinic is plausibly distinct from a walk-in.
Member 4 decides — adding it means changing a committed enum, so it is not a diagram-only
change.

**11. Is Equipment one role or two?** *(Rev 2.2)* This diagram and `staff-spec.yaml` carry
a single `EquipmentManager`. `equipment-management-plan.md` §2 is written around two
distinct capabilities — **Inventory Administrator** (React: approves actions, manages
stock and the bed register) and **Equipment Technician** (Flutter: scans tags, completes
services, reports faults) — with different endpoint permissions for each. One role cannot
express that split. Members 2 and 3 to settle.
