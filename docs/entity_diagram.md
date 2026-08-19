# Entity Class Diagram

Reflects all decisions settled during the schema-design grilling session (37 decisions,
single hospital / multiple wards, unified staff identity, generic agent-workflow and
audit-log schemas). PKs are `Guid` (PostgreSQL `uuid`, `default: gen_random_uuid()`)
throughout.

**Revision 2** — aligned with the Staff Management "cascading swap" flow and the Patient
Management admission/discharge flow. Changes in this revision are marked *(Rev 2)*.
Items still requiring a group decision are collected in [Open Decisions](#open-decisions).

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

---

### Emergency / Ambulance

#### EmergencyCall extends AuditedEntity
```
+ PatientId: Guid (nullable) FK → Patient.Id
+ CallerName: string (nullable)
+ CallerPhone: string (nullable)
+ Latitude: decimal(9,6) (non-null)
+ Longitude: decimal(9,6) (non-null)
+ Details: string (nullable)
+ Priority: CallPriority (non-null)
+ Status: CallStatus (non-null)
+ Outcome: string (nullable)
```
**Table:** `emergency_calls`
**Note:** `PatientId` nullable — identity is often unknown at the scene, settable
later once intake matches the call to a registered `Patient`. *(Decision 25)*

#### Ambulance extends SoftDeletableEntity
```
+ RegistrationNumber: string (unique, non-null)
+ CurrentLatitude: decimal(9,6) (nullable)
+ CurrentLongitude: decimal(9,6) (nullable)
+ Status: AmbulanceStatus (non-null)
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
+ Type: AdmissionCategory (non-null)
+ GenderPolicy: WardGenderPolicy (non-null)             -- (Rev 2)
```
**Table:** `wards`
**Note:** `Type` mirrors `AdmissionCategory` so the Patient Admission & Bed Agent can
filter candidate beds by matching ward type to the patient's category. *(Decision 31)*
*(Rev 2)* `GenderPolicy` implements the patient flow's "throws out every bed in the wrong
gender ward" filter — before Rev 2 the schema had nothing on `Ward` to filter against.

#### Bed extends SoftDeletableEntity
```
+ WardId: Guid (non-null) FK → Ward.Id
+ Label: string (non-null)
+ Status: BedStatus (non-null)
+ IsIsolationCapable: bool = false (non-null)           -- (Rev 2)
+ ProximityRank: int (non-null)                         -- (Rev 2)
```
**Table:** `beds`
**Constraint:** UNIQUE(WardId, Label) WHERE IsActive
**Note:** *(Rev 2)* `IsIsolationCapable` is the bed side of the "no isolation for
infectious patients" filter (the patient side is `Admission.RequiresIsolation`).
`ProximityRank` is the bed side of "sicker patient goes closer to the nurses' station"
(the patient side is `Admission.AcuityLevel`); lower rank = closer, unique within a ward
so the agent's ranking is deterministic. Both filters were unimplementable before Rev 2.
`Status = Occupied` is denormalized from the open `BedAssignment` row and must be written
in the same transaction; `Cleaning`/`Maintenance`/`Reserved` are not derivable and are
authoritative here.

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
+ Gender: Gender (nullable)                             -- (Rev 2: was string)
+ PhoneNumber: string (nullable)
+ EmergencyContactName: string (nullable)
+ EmergencyContactPhone: string (nullable)
```
**Table:** `patients`
**Constraint:** `CREATE UNIQUE INDEX ON patients (national_id) WHERE national_id IS NOT NULL`
**Note:** `NationalId` nullable — unconscious/unidentified emergency admissions may
lack one at intake — but unique whenever present, to dedupe registered patients.
*(Decision 35)*
*(Rev 2)* `Gender` promoted from free text to an enum: the gender-ward filter is a
deterministic rule run by C# validation code, and a deterministic filter cannot run
reliably on free text.

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
+ AcuityLevel: AcuityLevel (non-null)                   -- (Rev 2)
+ RequiresIsolation: bool = false (non-null)            -- (Rev 2)
+ ExpectedArrivalAt: DateTimeOffset (nullable)          -- (Rev 2)
+ AdmittedAt: DateTimeOffset (nullable)                 -- (Rev 2: was non-null)
+ Status: AdmissionStatus (non-null)
```
**Table:** `admissions`
**Note:** `EmergencyCallId` nullable — null for walk-in/referral admissions, set for
the emergency-originated path. *(Decision 25)*
*(Rev 2)* The patient flow requires that for an emergency "the record is created
**BEFORE** they arrive, so a bed is ready when they get here". That state was
unrepresentable: `AdmittedAt` was non-null and `AdmissionStatus` was only
`{Active, Discharged}`. Now a pre-arrival row is `Status = Expected` with
`ExpectedArrivalAt` set and `AdmittedAt` null, flipping to `Active` on arrival.
`AcuityLevel` and `RequiresIsolation` are the patient-side inputs to the bed agent's
filter and ranking rules; both are set by clinical staff, never by the agent — the same
wall that keeps `Category` a human decision.

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

### StaffRole
```
AmbulanceCrew, WardNurse, GeneralStaff, DutyDispatchManager,
HospitalAdministrator, EquipmentInventoryManager
```

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

### AdmissionCategory *(Rev 2 — changed)*
```
ICU, HighDependency, Inpatient, DayCase, Outpatient
```
`HighDependency` added — the patient flow's care levels are "ICU / high-dependency /
inpatient / day-case / outpatient". Ordered most to least acute so the bed agent's
downgrade logic ("offers the next best thing") is a simple ordinal step.

### AcuityLevel *(Rev 2 — new)*
```
Critical, High, Medium, Low
```
Patient-side input to "sicker patient goes closer to the nurses' station"; pairs with
`Bed.ProximityRank`.

### Gender *(Rev 2 — new)*
```
Male, Female, Other, Unknown
```

### WardGenderPolicy *(Rev 2 — new)*
```
Male, Female, Mixed
```

### AdmissionSource *(Rev 2 — new)*
```
Emergency, WalkIn, Booked, Referral
```

### AppointmentStatus *(Rev 2 — new)*
```
Scheduled, CheckedIn, Completed, Cancelled, NoShow
```

### BedStatus *(Rev 2 — changed)*
```
Available, Reserved, Occupied, Cleaning, Maintenance
```
`Reserved` added for the 30-minute hold; the authoritative hold record is `BedReservation`.

### BedReservationStatus *(Rev 2 — new)*
```
Held, Confirmed, Expired, Released
```

### AdmissionStatus *(Rev 2 — changed)*
```
Expected, Active, Discharged, Cancelled
```
`Expected` = record created before arrival (the emergency fast path). `Cancelled` covers a
booked or expected admission that never happened.

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
CREATE UNIQUE INDEX ux_beds_ward_label         ON beds (ward_id, label)              WHERE is_active;
CREATE UNIQUE INDEX ux_equipment_types_name    ON equipment_types (name)             WHERE is_active;
CREATE UNIQUE INDEX ux_equipment_items_serial  ON equipment_items (serial_number)
    WHERE is_active AND serial_number IS NOT NULL;
CREATE UNIQUE INDEX ux_patients_national_id    ON patients (national_id)             WHERE national_id IS NOT NULL;
CREATE UNIQUE INDEX ux_beds_ward_proximity     ON beds (ward_id, proximity_rank)     WHERE is_active;
```

### Partial unique indexes — business invariants

```sql
-- one occupant per bed, one bed per admission
CREATE UNIQUE INDEX ux_bed_assign_bed   ON bed_assignments (bed_id)       WHERE end_at IS NULL;
CREATE UNIQUE INDEX ux_bed_assign_adm   ON bed_assignments (admission_id) WHERE end_at IS NULL;

-- one live hold per bed  (the 30-minute reservation race)
CREATE UNIQUE INDEX ux_bed_reservation  ON bed_reservations (bed_id)      WHERE status = 'Held';

-- one active admission per patient
CREATE UNIQUE INDEX ux_admissions_active ON admissions (patient_id)       WHERE status = 'Active';

-- an ambulance cannot be on two runs
CREATE UNIQUE INDEX ux_dispatch_ambulance ON dispatches (ambulance_id)
    WHERE status IN ('Assigned', 'EnRoute');

-- no duplicate open warning per target  (otherwise every threshold tick inserts one)
CREATE UNIQUE INDEX ux_warnings_open ON warnings (entity_type, entity_id, type)
    WHERE status = 'Open';

-- one confirmed allocation per (shift, staff); superseded rows may coexist
CREATE UNIQUE INDEX ux_allocations_confirmed ON allocations (shift_id, staff_member_id)
    WHERE status = 'Confirmed';

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
    CHECK (type = 'ShiftSwap' OR (swap_with_staff_member_id IS NULL AND swap_shift_id IS NULL));

ALTER TABLE bed_assignments ADD CONSTRAINT ck_bed_assign_window
    CHECK (end_at IS NULL OR end_at > start_at);
ALTER TABLE bed_assignments ADD CONSTRAINT ck_bed_assign_downgrade
    CHECK (NOT is_downgrade OR downgrade_reason IS NOT NULL);

ALTER TABLE bed_reservations ADD CONSTRAINT ck_bed_res_expiry CHECK (expires_at > created_at);

-- a pre-arrival record has no admitted_at; anything else must have one
ALTER TABLE admissions ADD CONSTRAINT ck_admissions_arrival
    CHECK ((status = 'Expected' AND admitted_at IS NULL)
        OR (status <> 'Expected' AND admitted_at IS NOT NULL));

ALTER TABLE discharges ADD CONSTRAINT ck_discharge_ready
    CHECK (discharged_at IS NULL OR readiness_status = 'Ready');

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
    WHERE status = 'PendingApproval';
CREATE INDEX ix_warnings_open          ON warnings (severity, created_at DESC) WHERE status = 'Open';
CREATE INDEX ix_emergency_calls_open   ON emergency_calls (status, created_at DESC);
CREATE INDEX ix_notifications_unread   ON notifications (recipient_staff_member_id, created_at DESC)
    WHERE read_at IS NULL;

-- agent query paths
CREATE INDEX ix_shifts_ward_date       ON shifts (ward_id, date);
CREATE INDEX ix_allocations_staff      ON allocations (staff_member_id) WHERE status = 'Confirmed';
CREATE INDEX ix_beds_ward_status       ON beds (ward_id, status) WHERE is_active;
CREATE INDEX ix_discharges_ready       ON discharges (readiness_status)
    WHERE discharged_at IS NULL;

-- expiry sweep for the 30-minute holds
CREATE INDEX ix_bed_reservations_expiry ON bed_reservations (expires_at) WHERE status = 'Held';
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
- **Enum storage** is an open decision — see below.

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
| Health Equipment | Member 3 | EquipmentType, EquipmentItem, StockLevel, MaintenanceSchedule, Warning |
| Patient Management | Member 4 | Patient, Admission, BedAssignment, BedReservation, Discharge, DischargeChecklistItem, Appointment, Ward, Bed |
| Shared / Group | All | StaffMember, RefreshToken, DeviceToken, Notification, AgentWorkflow, AgentProposedChange, AuditLog |

**Note:** `Ward` sits under Patient Management but is referenced by all four components
(`Shift.WardId`, `EquipmentItem.WardId`, `StockLevel.WardId`, `Dispatch.DestinationWardId`).
Treat its schema as frozen once agreed — changes to it break three other members.

---

## Open Decisions

These need a group call before implementation. Each one changes work already scoped.

**1. Patient identity — blocking.** `StaffMember` is the only auth identity
(`Patient` "has no auth identity"), but the patient flow has patients booking visits
"in the app" and receiving "Ward 5B, Bed 12" on their phone. The Component Plan's Flutter
roles are crew / nurse / staff only — no patient. Three documents disagree.
*Recommendation:* keep patients out of the app. Deliver the bed notification by SMS from
the backend and let a receptionist or nurse create `Appointment` rows. Adding a patient
identity means a second auth path, patient-scoped authorization on every endpoint, and PII
exposure decisions — real work that earns nothing on the rubric, since the
three-roles-per-client requirement is already met without it. `Notification` and
`DeviceToken` are currently written staff-only on that assumption; if the group decides
otherwise, both need a nullable `PatientId` and a polymorphic recipient.

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
