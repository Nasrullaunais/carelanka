# Staff Management — Component Design

**CareLanka Hospital Management System · SE3090 Assignment 1**
**Owner:** Member 2 (Nasrullah) · **Status:** Implemented (Core & Reports) / Design Plan (Agent & UI) · **Version:** 1.0
**Contract:** `specs/staff-spec.yaml` (44 operations) · **Build Guide:** `docs/build/staff.md`
**Boundaries:** `specs/integration_of_functions.md` §17–§21

This document specifies the software architecture, domain model, deterministic constraints, AI agent boundaries, and operational workflows for the **Staff Management** component of CareLanka. It reflects the completed backend implementation and establishes the exact design specifications for the pending Roster Proposal agent and frontend user interfaces.

---

## 1. Staff Management Overview and Responsibilities

Staff Management is responsible for the human workforce of CareLanka Hospital. It manages staff member profiles, clinical certifications, shift schedules, ward staffing constraints, workforce allocations, leave workflows, attendance logging, and ward coverage auditing.

### 1.1 Core Responsibilities

1. **Staff Directory & Identity:** Maintain authoritative staff profiles, departments, clinical roles, and active employment lifecycles.
2. **Inter-Service Staff Lookup:** Provide a high-throughput, fault-tolerant batch lookup service (`POST /api/staff/lookup`) for Emergency, Equipment, and Patient Management to resolve audit trails ("Approved by...") without duplicating staff data across boundaries.
3. **Skills & Certifications:** Track clinical skill certifications with date-bounded currency (`valid_from` to `expires_at`).
4. **Shift Scheduling:** Plan shifts with required clinical roles, skill qualifications, and target headcount requirements, including overnight rollover calculations.
5. **Ward Staffing Rules:** Enforce ward-level minimum headcount policies across roles and required qualifications.
6. **Workforce Allocation:** Assign qualified staff to shifts while enforcing hard deterministic constraints (qualification validity, shift overlaps, double-booking prevention, and leave conflicts).
7. **Leave & Shift Swaps:** Manage staff leave requests and peer-to-peer shift swaps, automatically releasing conflicting allocations upon approval.
8. **Personal Roster & Time Attendance:** Allow staff members to view their individual schedules (`/me/shifts`) and log attendance through shift clock-in and clock-out operations.
9. **Ward Coverage Aggregation:** Compute real-time coverage status (`optimal`, `adequate`, `understaffed`, `critical`) across all hospital wards without leaking sensitive staff identities to external callers.
10. **Management Reporting:** Generate comprehensive reports on ward coverage compliance, leave utilization, and AI agent performance metrics.
11. **AI Roster Proposal Agent:** Draft automated reallocation proposals to resolve staffing shortages using allow-listed tools, cascading cross-ward swaps, deterministic validation, and human-in-the-loop approval.

### 1.2 Boundary Separation: What Staff Management Does NOT Do

| Out of Scope for Staff Management | Responsible Component | Boundary Contract |
| :--- | :--- | :--- |
| Patient admissions, bed allocations, and clinical care categories | Patient Management (Member 4) | Staff reads ward identity and occupancy; Patient stores `StaffMemberId` references. |
| Ward entity definition and physical bed capacity | Patient Management (Member 4) | Staff references `WardId` as a foreign key; ward metadata belongs to Member 4 (`integration_of_functions.md` §17). |
| Emergency call triage and ambulance dispatch routing | Emergency (Member 1) | Emergency tracks `AmbulanceCrewAssignment`; Staff provides staff lookup (`integration_of_functions.md` §19). |
| Medical equipment allocation and pharmacy inventory | Equipment Management (Member 3) | Equipment tracks item assignments; Staff resolves staff IDs via lookup (`integration_of_functions.md` §18). |
| Autonomous roster changes without human sign-off | Prohibited System Rule | All AI agent proposals require explicit review and approval by a `HospitalAdministrator` (§14). |

---

## 2. Architecture and Backend Components

The Staff Management backend is implemented within the ASP.NET Core 8 Web API (`api/`) following a clean controller-service architecture:

```
api/
├── Controllers/Staff/
│   ├── AllocationsController.cs         // Allocation CRUD and cancellation
│   ├── LeaveRequestsController.cs       // Leave requests, approvals, and swaps
│   ├── MyRosterController.cs            // /me/shifts and clock-in/out
│   ├── ShiftsController.cs              // Shift scheduling and allocations
│   ├── SkillsController.cs              // Skill catalog and staff skill grants
│   ├── StaffLookupController.cs         // Inter-service batch lookup
│   ├── StaffMembersController.cs        // Staff CRUD and lifecycle toggles
│   ├── StaffReportsController.cs        // Coverage, leave, and agent performance reports
│   ├── WardCoverageController.cs        // Real-time ward coverage status
│   └── WardStaffingRulesController.cs   // Ward minimum headcount rules
├── Services/Staff/
│   ├── AllocationService.cs             // Deterministic allocation validation
│   ├── LeaveRequestService.cs           // Leave approval, swap logic, and release
│   ├── MyRosterService.cs               // Identity-scoped roster and clock events
│   ├── ShiftService.cs                  // Overnight rollover and shift queries
│   ├── SkillService.cs                  // Skill currency verification
│   ├── StaffLookupService.cs            // Batch ID resolution
│   ├── StaffMemberService.cs            // Profile lifecycle management
│   ├── StaffReportsService.cs           // Aggregation queries for management reports
│   └── WardCoverageService.cs           // Ward coverage status calculations
├── DTOs/Staff/                          // Strongly-typed request/response models
├── Validators/Staff/                    // FluentValidation rules for requests
└── Data/Entities/Staff/                 // EF Core entity definitions
```

### 2.1 Cross-Cutting Dependencies and Ports
- **Authentication & Claims:** Scoped identity resolution via `ICurrentUser` resolving `sub` (Staff ID) and `role` (`StaffRole`).
- **Time Abstraction:** Injected `TimeProvider` ensures predictable date and time calculations across unit and integration tests.
- **Problem Details:** Structured error responses adhering to RFC 7807 via `ProblemResponseWriter` and `MessageCode`.
- **Ward Adapter:** `IWardDirectory` port enables querying ward metadata from Patient Management without coupling to internal Patient domain models.

---

## 3. Database Entities and Relationships

All Staff Management tables are managed via Entity Framework Core and PostgreSQL. The initial schema migration is established in `api/Data/Migrations/20260914005931_Staff_AddStaffCore.cs`.

```
                    ┌───────────────────────────┐
                    │        StaffMember        │
                    │ (Common / Owned by Staff) │
                    └─────────────┬─────────────┘
                                  │ 1
        ┌─────────────────────────┼─────────────────────────┐
        │ 1..*                    │ 1..*                    │ 1..*
┌───────┴──────────┐      ┌───────┴──────────┐      ┌───────┴──────────┐
│ StaffMemberSkill │      │    Allocation    │      │   LeaveRequest   │
└───────┬──────────┘      └───────┬──────────┘      └──────────────────┘
        │ *..1                    │ *..1
┌───────┴──────────┐      ┌───────┴──────────┐
│      Skill       │      │      Shift       │
└───────┬──────────┘      └───────┬──────────┘
        │ 0..1                    │ 0..1 (WardId FK to Patient.Ward)
┌───────┴──────────┐              │
│ WardStaffingRule ├──────────────┘
└──────────────────┘
```

### 3.1 Entity Specifications

#### 1. `StaffMember` (`staff_members`)
- `id` (`uuid`, PK): Unique staff member identifier.
- `first_name`, `last_name` (`varchar(100)`): Personal name.
- `email` (`varchar(255)`, Unique): Login credential.
- `phone` (`varchar(30)`): Contact telephone.
- `role` (`varchar(40)`): Role enum (`hospital_administrator`, `ward_nurse`, `doctor`, `ambulance_crew`, `general_staff`, `duty_manager`, `equipment_manager`).
- `department` (`varchar(100)`): Department assignment.
- `is_active` (`bool`): Soft-delete / active status flag.
- `created_at`, `updated_at`, `deleted_at` (`timestamptz`).

#### 2. `Skill` (`skills`)
- `id` (`uuid`, PK): Unique skill identifier.
- `name` (`varchar(100)`, Unique): Clinical qualification name (e.g., "ICU Certified", "Triage Specialist", "Pediatric Life Support").
- `description` (`varchar(500)`): Clinical scope summary.
- `is_active` (`bool`): Deactivation flag.
- `created_at`, `updated_at`, `deleted_at` (`timestamptz`).

#### 3. `StaffMemberSkill` (`staff_member_skills`)
- `id` (`uuid`, PK): Junction identifier.
- `staff_member_id` (`uuid`, FK -> `staff_members`): Certified staff member.
- `skill_id` (`uuid`, FK -> `skills`): Acquired skill.
- `valid_from` (`date`, Nullable): Certification effective date.
- `expires_at` (`date`, Nullable): Certification lapse date.
- `created_at` (`timestamptz`).

#### 4. `Shift` (`shifts`)
- `id` (`uuid`, PK): Unique shift identifier.
- `ward_id` (`uuid`): Target ward reference (owned by Patient Management).
- `date` (`date`): Scheduled calendar date.
- `start_time` (`time`): Scheduled shift start.
- `end_time` (`time`): Scheduled shift end.
- `required_role` (`varchar(40)`): Clinical role needed.
- `required_skill_id` (`uuid`, FK -> `skills`, Nullable): Mandatory qualification.
- `headcount_needed` (`int`): Target staffing level.
- `minimum_headcount` (`int`): Safety floor threshold (`ck_shifts_headcount`: `minimum_headcount <= headcount_needed`).
- `created_at`, `updated_at` (`timestamptz`).

#### 5. `WardStaffingRule` (`ward_staffing_rules`)
- `id` (`uuid`, PK): Rule identifier.
- `ward_id` (`uuid`): Target ward.
- `required_role` (`varchar(40)`): Role required.
- `required_skill_id` (`uuid`, FK -> `skills`, Nullable): Skill required.
- `minimum_headcount` (`int`): Mandatory baseline headcount (`ck_wsr_min`: `> 0`).
- `created_at`, `updated_at` (`timestamptz`).

#### 6. `Allocation` (`allocations`)
- `id` (`uuid`, PK): Allocation identifier.
- `shift_id` (`uuid`, FK -> `shifts`): Assigned shift.
- `staff_member_id` (`uuid`, FK -> `staff_members`): Allocated clinician.
- `status` (`varchar(20)`): State enum (`proposed`, `confirmed`, `released`, `cancelled`).
- `source` (`varchar(20)`): Origin enum (`manual`, `agent_proposal`, `swap_request`).
- `ended_at` (`timestamptz`, Nullable): When allocation was terminated.
- `ended_reason` (`varchar(30)`, Nullable): `leave_approved`, `swapped_out`, `shift_cancelled`, `staff_deactivated`, `manual`.
- `replaced_by_allocation_id` (`uuid`, FK -> `allocations`, Nullable): Forward link for cascading swaps.
- `clocked_in_at`, `clocked_out_at` (`timestamptz`, Nullable): Time attendance timestamps.
- `created_by_staff_id` (`uuid`, FK -> `staff_members`, Nullable): Administrative creator.
- `roster_proposal_id` (`uuid`, Nullable): Link to `AgentWorkflow.Id` for agent-generated allocations.
- `created_at`, `updated_at` (`timestamptz`).

#### 7. `LeaveRequest` (`leave_requests`)
- `id` (`uuid`, PK): Request identifier.
- `staff_member_id` (`uuid`, FK -> `staff_members`): Requesting staff.
- `start_date`, `end_date` (`date`): Requested leave window.
- `reason` (`varchar(500)`): Explanation or category.
- `status` (`varchar(20)`): `pending`, `approved`, `rejected`, `cancelled`.
- `decision_by_staff_id` (`uuid`, FK -> `staff_members`, Nullable): Approving administrator.
- `decided_at` (`timestamptz`, Nullable): Approval/rejection timestamp.
- `decision_notes` (`varchar(500)`, Nullable): Feedback note.
- `shift_swap_target_staff_id` (`uuid`, FK -> `staff_members`, Nullable): Peer for swap requests.
- `target_shift_id` (`uuid`, FK -> `shifts`, Nullable): Target shift for swap requests.
- `created_at`, `updated_at` (`timestamptz`).

#### 8. Common Agent Workflow Entities (Shared Group Schema)
- `AgentWorkflow` (`agent_workflows`): Tracks workflow proposals with `AgentType = StaffAllocation`. Stores structured plan JSON, tool execution outputs, deterministic validation results, review notes, and final status (`draft`, `pending_approval`, `approved`, `rejected`, `applied`, `failed`).
- `AgentProposedChange` (`agent_proposed_changes`): Individual atomic changes proposed by the agent (e.g., `assign_to_shift`, `move_between_shifts`, `cancel_allocation`), with validation status, rationale, and applied entity references.

---

## 4. Staff Member CRUD and Staff Lookup

### 4.1 Staff Management Operations
- `GET /api/staff`: List staff with filters (`role`, `department`, `is_active`, `search`) and pagination (`page`, `page_size`).
- `POST /api/staff`: Create a new staff profile. Requires `HospitalAdministrator`.
- `GET /api/staff/{id}`: Detailed profile lookup including currently assigned skills.
- `PUT /api/staff/{id}`: Update personal and departmental information.
- `POST /api/staff/{id}/deactivate`: Soft-delete staff member.
  - **Constraint:** Deactivation is rejected if the staff member has upcoming confirmed shift allocations, unless those allocations are manually reassigned or cancelled.
- `POST /api/staff/{id}/reactivate`: Restore deactivated staff member.

### 4.2 Inter-Component Staff Lookup (`POST /api/staff/lookup`)
To maintain architectural boundaries, Patient Management, Equipment Management, and Emergency never store duplicated staff names or profiles. They store foreign `StaffMemberId` values and resolve them in batches using this endpoint:
- **Contract:** Accepts an array of up to 100 GUIDs (`{ "staff_ids": ["..."] }`).
- **Response:** Returns an ordered array of `{ staff_id, found, full_name, role, is_active }`.
- **Fault Tolerance:** Unmatched or soft-deleted IDs return `found: false` rather than failing the entire request with a 404, preventing rendering breaks in caller tables.
- **Authorization:** Accessible to any authenticated staff role.

---

## 5. Skills Management

Clinical skills represent qualifications (e.g., ICU Care, Trauma Life Support, Chemotherapy Administration) required for specialized ward shifts.

### 5.1 Skill Currency as a Date Range
A boolean flag (`has_skill = true`) is clinically invalid because medical certifications expire. CareLanka models skill holding through `StaffMemberSkill` with `valid_from` and `expires_at` dates:

$$\text{Skill Is Valid on Shift Date } D \iff (\text{valid\_from} \le D) \land (\text{expires\_at} \text{ is NULL} \lor D \le \text{expires\_at})$$

### 5.2 Endpoints
- `GET /api/skills`: Catalog listing with active status filter.
- `POST /api/skills`: Add skill definition (HospitalAdministrator).
- `GET /api/skills/{id}`: Skill details.
- `PUT /api/skills/{id}`: Modify skill metadata.
- `DELETE /api/skills/{id}`: Soft-delete skill definition.
- `POST /api/skills/{id}/reactivate`: Reactivate skill.
- `POST /api/staff/{id}/skills`: Assign skill to staff with `valid_from` and `expires_at`.
- `DELETE /api/staff/{id}/skills/{skillId}`: Revoke skill certification.

---

## 6. Shifts and Overnight Shift Handling

Shifts define staffing demand for a specific ward on a given date.

### 6.1 The Overnight Shift Rollover Problem
A shift stores `date` (`DateOnly`), `start_time` (`TimeOnly`), and `end_time` (`TimeOnly`). When a night shift runs from 22:00 to 06:00, `end_time < start_time`. The shift terminates on the following calendar day ($D + 1$).

#### Canonical Time Window Conversion
To ensure consistent overlap detection, double-booking prevention, and ward coverage calculation, `ShiftService` converts all shifts to canonical UTC DateTimeOffset intervals:

$$\text{Window Start} = \text{DateTimeOffset}(D, \text{start\_time}, \text{UTC})$$

$$\text{Window End} = \begin{cases}
\text{DateTimeOffset}(D, \text{end\_time}, \text{UTC}) & \text{if } \text{end\_time} > \text{start\_time} \\
\text{DateTimeOffset}(D + 1\text{ day}, \text{end\_time}, \text{UTC}) & \text{if } \text{end\_time} \le \text{start\_time}
\end{cases}$$

Two shifts $A$ and $B$ overlap if and only if:
$$\text{Start}_A < \text{End}_B \land \text{Start}_B < \text{End}_A$$

### 6.2 Shift Endpoints
- `GET /api/shifts`: Filter shifts by date range, ward, role, and coverage status.
- `POST /api/shifts`: Create single shift.
- `GET /api/shifts/{id}`: Retrieve shift with summary headcount metrics.
- `PUT /api/shifts/{id}`: Modify shift timings or headcount requirements.
- `DELETE /api/shifts/{id}`: Cancel shift and associated allocations.
- `GET /api/shifts/{id}/allocations`: View confirmed and proposed staff on this shift.

---

## 7. Ward Staffing Rules

Ward staffing rules define baseline policies for ward safety (e.g., "ICU requires at least 4 Ward Nurses with ICU Certification on every shift").

- **Entity:** `WardStaffingRule` references `ward_id`, `required_role`, `required_skill_id`, and `minimum_headcount`.
- `GET /api/wards/{wardId}/staffing-rules`: Retrieve staffing rules for a ward.
- `PUT /api/wards/{wardId}/staffing-rules`: Atomically replace the complete rule set for a ward to maintain policy consistency.

---

## 8. Staff Allocations and Deterministic Validation Rules

Allocations bind a staff member to a scheduled shift. The allocation lifecycle governs the workforce state:

```
[ proposed ] ──(approve)──> [ confirmed ] ──(release / cancel)──> [ released / cancelled ]
                                  │
                             (clock-in)
                                  ▼
                         [ clocked_in_at ]
                                  │
                             (clock-out)
                                  ▼
                         [ clocked_out_at ]
```

### 8.1 Hard Deterministic Validation Rules
Before any allocation is created or confirmed (`AllocationService`), plain C# validation rules are executed. A failure immediately rejects the operation with a 400 Bad Request or 409 Conflict:

1. **Active Employment:** Staff member must be active (`is_active = true`).
2. **Role Compatibility:** `StaffMember.Role` must match `Shift.RequiredRole`.
3. **Skill Currency:** If `Shift.RequiredSkillId` is specified, the staff member must possess that skill valid on the shift date.
4. **Double-Booking Prevention:** The staff member cannot have another confirmed allocation whose canonical time window overlaps the target shift window.
5. **Approved Leave Conflict:** The staff member cannot have an approved leave request covering the shift date.
6. **Headcount Ceiling:** Confirmed allocations cannot exceed `Shift.HeadcountNeeded`.

---

## 9. Leave Requests and Shift Swaps

### 9.1 Leave Request Lifecycle
1. **Creation (`POST /api/leave-requests`):** Any staff member submits a leave request for `start_date` to `end_date`.
2. **Decision (`POST /api/leave-requests/{id}/approve` or `reject`):** Hospital Administrator reviews the request.
3. **Automated Allocation Release on Approval:** When a leave request is approved, `LeaveRequestService` automatically finds all confirmed allocations for that staff member falling within the leave period, marks them `released`, and sets `ended_reason = leave_approved`.
4. **Coverage Alerts:** If releasing an allocation drops a shift below `minimum_headcount`, an alert indicator is flagged in the response to notify administrators that backfill is needed.

### 9.2 Shift Swaps
When two staff members agree to exchange shifts:
1. Staff member creates a leave request specifying `shift_swap_target_staff_id` and `target_shift_id`.
2. `LeaveRequestService` verifies that both staff members are qualified for each other's shifts and have no double-booking conflicts.
3. Upon administrator approval, the existing allocations are terminated with `ended_reason = swapped_out`, new confirmed allocations are created, and `replaced_by_allocation_id` is linked.

---

## 10. My Shifts and Clock-In / Clock-Out

Staff members access personal schedules and log attendance via `MyRosterController`:

### 10.1 Scoped Schedule (`GET /api/me/shifts`)
- Scoped strictly to the authenticated caller's identity via JWT `sub` claim.
- Accepts `from` and `to` date parameters.
- Returns assigned shifts, ward names, timings, and clock-in/out statuses.

### 10.2 Time Attendance Logging
- `POST /api/me/allocations/{id}/clock-in`:
  - Enforces that `Allocation.StaffMemberId` matches caller identity.
  - Verifies allocation status is `confirmed`.
  - Rejects if already clocked in (`clocked_in_at != null`).
  - Sets `clocked_in_at = UtcNow`.
- `POST /api/me/allocations/{id}/clock-out`:
  - Verifies caller ownership.
  - Rejects if not clocked in.
  - Rejects if already clocked out.
  - Sets `clocked_out_at = UtcNow`.

---

## 11. Ward Coverage Tracking

`WardCoverageController` (`GET /api/coverage/wards`) computes real-time workforce health across all hospital wards:

### 11.1 Status Calculation
For each ward, current active allocations are compared against minimum and target headcounts:

| Coverage Status | Mathematical Condition | Clinical Meaning |
| :--- | :--- | :--- |
| **`optimal`** | $\text{OnDutyCount} \ge \text{HeadcountNeeded}$ | Fully staffed to target capacity |
| **`adequate`** | $\text{MinimumHeadcount} \le \text{OnDutyCount} < \text{HeadcountNeeded}$ | Safe minimum met, but below ideal target |
| **`understaffed`**| $0 < \text{OnDutyCount} < \text{MinimumHeadcount}$ | Unsafe staffing deficit; urgent backfill needed |
| **`critical`** | $\text{OnDutyCount} = 0 \land \text{MinimumHeadcount} > 0$ | Ward completely uncovered; immediate danger |

- **Privacy & Security:** The endpoint aggregates counts and role breakdowns only. It does not expose staff identities, making it safe for cross-service consumption (e.g., Equipment reallocation heuristics).

---

## 12. Staff Reports

Management reports are served by `StaffReportsController` and `StaffReportsService`:

1. **Ward Coverage Report (`GET /api/reports/coverage`):** Analyzes historical coverage across wards over a specified date range, reporting planned vs actual hours, compliance rates, and understaffed shift instances.
2. **Leave Utilization Report (`GET /api/reports/leave`):** Summarizes leave patterns by department, approval rates, average leave duration, and staffing impact.
3. **Agent Performance Report (`GET /api/reports/staff/agent-performance`):** Tracks AI proposal quality, counting total proposals, approval rates, rejection reasons, average review latency, and cascading swap depths.

---

## 13. Staff Allocation Agent & Roster Proposal Design

### 13.1 Implementation Status Matrix

| Subsystem Component | Status | Implementation Details |
| :--- | :---: | :--- |
| Database Schema & Entities | ✅ **IMPLEMENTED** | `agent_workflows`, `agent_proposed_changes`, `Allocation.RosterProposalId`. |
| Domain Enums & DTOs | ✅ **IMPLEMENTED** | `AgentType.StaffAllocation`, `RosterProposalStatus`, `RejectionReason`, `ProposedChangeType`, `RosterValidationResult`. |
| Performance Report Endpoint | ✅ **IMPLEMENTED** | `GET /api/reports/staff/agent-performance` fully implemented and tested. |
| Agent Background Service | ❌ **PLANNED / MISSING** | Background worker in `api/Agents/Staff/` to execute automated solvers. |
| Allow-Listed Tools Port | ❌ **PLANNED / MISSING** | Read-only tool implementations for candidate search and occupancy. |
| Proposal API Endpoints (6 ops) | ❌ **PLANNED / MISSING** | `/roster-proposals` endpoints (list, create, get, approve, reject, revise). |
| Proposal Approval Executor | ❌ **PLANNED / MISSING** | Atomic execution service translating approved changes into live allocations. |

### 13.2 Agent Architectural Blueprint

When implemented, the Staff Allocation Agent operates under strict allow-listed constraints:

```
[ Understaffed Shift ] ──> [ Staff Allocation Agent ]
                                     │
                 ┌───────────────────┴───────────────────┐
                 ▼                                       ▼
       [ Allow-Listed Tools ]                  [ Cascading Solver ]
       - GetUnderstaffedShifts                 - Direct free-staff fill
       - GetEligibleStaff                      - Cross-ward surplus swap
       - GetWardCoverage                       - Source ward min-check
       - GetWardOccupancy (Patient API)
                 │
                 ▼
       [ Deterministic C# Validator ]
       - Skill currency verification
       - Overlap & double-booking check
       - Source ward minimum preservation
                 │
                 ▼
       [ AgentWorkflow Draft ] (Pending Human Approval)
```

#### Allow-Listed Agent Tools (Read-Only)
The agent possesses zero database write privileges. It gathers context strictly via read-only tools:
1. `get_understaffed_shifts(dateRange)`: Identify shifts with confirmed allocations below `minimum_headcount`.
2. `get_eligible_staff(shiftId)`: Query active staff members with matching roles and active certifications.
3. `get_ward_coverage(date)`: Check staffing margins across all wards.
4. `get_ward_occupancy(wardId)`: Query Patient Management's bed occupancy to evaluate patient acuity demand (`integration_of_functions.md` §5.3).

#### Cascading Swap Logic
When no completely free staff members exist:
1. The agent inspects wards with staffing levels *above* `minimum_headcount` (surplus wards).
2. It drafts a cascading swap: taking a qualified nurse from Ward A to cover Ward B.
3. **Hard Constraint:** The move is only proposed if Ward A remains at or above its own `minimum_headcount` after the removal.

---

## 14. Human Approval and Safety Boundary for AI Proposals

In accordance with assignment safety specification §9.1:
> **"No AI agent may autonomously alter staff allocations, rosters, or operational schedules without explicit human-in-the-loop authorization."**

### 14.1 The Approval Gate (`POST /api/roster-proposals/{id}/approve`)
1. **Draft Status:** All agent plans are saved to `agent_workflows` with status `pending_approval`.
2. **Human Authorization:** Only users holding the `HospitalAdministrator` role can approve proposals. The approving administrator cannot be the user who initiated the workflow run.
3. **Pre-Commit Re-Validation:** Real-world hospital state changes rapidly. Immediately upon an approval call, the API re-runs all deterministic validation rules in C#. If a proposed nurse was allocated elsewhere in the interim, the proposal is rejected with a 409 Conflict rather than applied on stale data.
4. **Atomic Transactional Application:**
   - Source allocation marked `released` with `ended_reason = swapped_out`.
   - Target allocation created with `status = confirmed`, `source = agent_proposal`, and `roster_proposal_id` linked.
   - `replaced_by_allocation_id` set on the original allocation.
   - Proposal status updated to `approved` and `applied`.

---

## 15. Authentication and Role Authorization

Staff Management endpoints enforce role-based access control (RBAC) configured in `api/Program.cs`:

| Authorization Policy | Permitted Roles | Accessible Operations |
| :--- | :--- | :--- |
| **`Policies.HospitalAdministrator`** | `HospitalAdministrator` | Full Staff CRUD, Skill CRUD & grants, Shift creation/deletion, Allocation creation/cancellation, Leave approval/rejection, Roster Proposal approval, Staff Reports. |
| **`Policies.DutyManager`** | `DutyManager`, `HospitalAdministrator` | Read shifts, view ward coverage, read-only staff directory, view coverage reports. |
| **`Policies.WardNurse`** | `WardNurse`, `Doctor`, `DutyManager`, `HospitalAdministrator` | View shift schedules, view ward allocations. |
| **Any Authenticated Staff** | All authenticated tokens (`sub` present) | `POST /staff/lookup`, `GET /skills`, `GET /coverage/wards`, `GET /me/shifts`, `POST /me/allocations/{id}/clock-in`, `POST /me/allocations/{id}/clock-out`, `POST /leave-requests`. |

---

## 16. API Endpoint Summary

The table below lists all 44 endpoints defined in `specs/staff-spec.yaml`:

| # | Method | Path | Status | Controller | Role Required |
| :- | :--- | :--- | :---: | :--- | :--- |
| 1 | `POST` | `/staff/lookup` | ✅ Implemented | `StaffLookupController` | Any Authenticated |
| 2 | `GET` | `/staff` | ✅ Implemented | `StaffMembersController` | Admin, DutyManager |
| 3 | `POST` | `/staff` | ✅ Implemented | `StaffMembersController` | HospitalAdministrator |
| 4 | `GET` | `/staff/{id}` | ✅ Implemented | `StaffMembersController` | Admin, DutyManager |
| 5 | `PUT` | `/staff/{id}` | ✅ Implemented | `StaffMembersController` | HospitalAdministrator |
| 6 | `POST` | `/staff/{id}/deactivate` | ✅ Implemented | `StaffMembersController` | HospitalAdministrator |
| 7 | `POST` | `/staff/{id}/reactivate` | ✅ Implemented | `StaffMembersController` | HospitalAdministrator |
| 8 | `GET` | `/skills` | ✅ Implemented | `SkillsController` | Any Authenticated |
| 9 | `POST` | `/skills` | ✅ Implemented | `SkillsController` | HospitalAdministrator |
| 10 | `GET` | `/skills/{id}` | ✅ Implemented | `SkillsController` | Any Authenticated |
| 11 | `PUT` | `/skills/{id}` | ✅ Implemented | `SkillsController` | HospitalAdministrator |
| 12 | `DELETE`| `/skills/{id}` | ✅ Implemented | `SkillsController` | HospitalAdministrator |
| 13 | `POST` | `/skills/{id}/reactivate` | ✅ Implemented | `SkillsController` | HospitalAdministrator |
| 14 | `POST` | `/staff/{id}/skills` | ✅ Implemented | `SkillsController` | HospitalAdministrator |
| 15 | `DELETE`| `/staff/{id}/skills/{skillId}` | ✅ Implemented | `SkillsController` | HospitalAdministrator |
| 16 | `GET` | `/shifts` | ✅ Implemented | `ShiftsController` | Admin, DutyManager, Nurse |
| 17 | `POST` | `/shifts` | ✅ Implemented | `ShiftsController` | HospitalAdministrator |
| 18 | `GET` | `/shifts/{id}` | ✅ Implemented | `ShiftsController` | Admin, DutyManager, Nurse |
| 19 | `PUT` | `/shifts/{id}` | ✅ Implemented | `ShiftsController` | HospitalAdministrator |
| 20 | `DELETE`| `/shifts/{id}` | ✅ Implemented | `ShiftsController` | HospitalAdministrator |
| 21 | `GET` | `/shifts/{id}/allocations` | ✅ Implemented | `ShiftsController` | Admin, DutyManager, Nurse |
| 22 | `GET` | `/wards/{wardId}/staffing-rules` | ✅ Implemented | `WardStaffingRulesController` | Admin, DutyManager |
| 23 | `PUT` | `/wards/{wardId}/staffing-rules` | ✅ Implemented | `WardStaffingRulesController` | HospitalAdministrator |
| 24 | `POST` | `/allocations` | ✅ Implemented | `AllocationsController` | HospitalAdministrator |
| 25 | `GET` | `/allocations/{id}` | ✅ Implemented | `AllocationsController` | Admin, DutyManager, Nurse |
| 26 | `DELETE`| `/allocations/{id}` | ✅ Implemented | `AllocationsController` | HospitalAdministrator |
| 27 | `GET` | `/leave-requests` | ✅ Implemented | `LeaveRequestsController` | Any Authenticated (Scoped) |
| 28 | `POST` | `/leave-requests` | ✅ Implemented | `LeaveRequestsController` | Any Authenticated |
| 29 | `GET` | `/leave-requests/{id}` | ✅ Implemented | `LeaveRequestsController` | Requester, Admin |
| 30 | `POST` | `/leave-requests/{id}/approve` | ✅ Implemented | `LeaveRequestsController` | HospitalAdministrator |
| 31 | `POST` | `/leave-requests/{id}/reject` | ✅ Implemented | `LeaveRequestsController` | HospitalAdministrator |
| 32 | `POST` | `/leave-requests/{id}/cancel` | ✅ Implemented | `LeaveRequestsController` | Requester |
| 33 | `GET` | `/me/shifts` | ✅ Implemented | `MyRosterController` | Any Authenticated (`sub`) |
| 34 | `POST` | `/me/allocations/{id}/clock-in` | ✅ Implemented | `MyRosterController` | Allocation Owner (`sub`) |
| 35 | `POST` | `/me/allocations/{id}/clock-out` | ✅ Implemented | `MyRosterController` | Allocation Owner (`sub`) |
| 36 | `GET` | `/coverage/wards` | ✅ Implemented | `WardCoverageController` | Any Authenticated |
| 37 | `GET` | `/reports/coverage` | ✅ Implemented | `StaffReportsController` | Admin, DutyManager |
| 38 | `GET` | `/reports/leave` | ✅ Implemented | `StaffReportsController` | HospitalAdministrator |
| 39 | `GET` | `/reports/staff/agent-performance`| ✅ Implemented | `StaffReportsController` | HospitalAdministrator |
| 40 | `GET` | `/roster-proposals` | ❌ Planned | *Unassigned* | HospitalAdministrator |
| 41 | `POST` | `/roster-proposals` | ❌ Planned | *Unassigned* | HospitalAdministrator |
| 42 | `GET` | `/roster-proposals/{id}` | ❌ Planned | *Unassigned* | HospitalAdministrator |
| 43 | `POST` | `/roster-proposals/{id}/approve` | ❌ Planned | *Unassigned* | HospitalAdministrator |
| 44 | `POST` | `/roster-proposals/{id}/reject` | ❌ Planned | *Unassigned* | HospitalAdministrator |
| (45)| `POST`| `/roster-proposals/{id}/request-revision`| ❌ Planned | *Unassigned* | HospitalAdministrator |

---

## 17. Testing Strategy and Current Test Coverage

The Staff Management implementation is rigorously tested using an end-to-end integration test suite running in `tests/CareLanka.Api.Tests`. Tests execute against ephemeral PostgreSQL instances via Testcontainers.

### 17.1 Test Suites Summary

| Test File | Covered Area | Passing Tests | Key Scenarios Tested |
| :--- | :--- | :---: | :--- |
| `StaffLookupEndpointTests.cs` | Batch Staff Lookup | 9 | Exact request order, missing IDs handling (`found: false`), deactivation status, max batch limits. |
| `StaffMemberEndpointTests.cs` | Staff Profiles & CRUD | 23 | Validation rules, email uniqueness, role assignment, deactivation allocation conflict checks. |
| `SkillEndpointTests.cs` | Skills & Certifications | 24 | Skill CRUD, duplicate naming guards, date range validity (`valid_from`/`expires_at`), revoking skills. |
| `ShiftEndpointTests.cs` | Shifts & Staffing Rules | 26 | Overnight shift rollovers, headcount constraints, ward staffing rule atomic replacement. |
| `AllocationEndpointTests.cs` | Staff Allocations | 29 | Double-booking prevention, skill qualification verification, leave overlap rejection, manual cancellation. |
| `LeaveRequestEndpointTests.cs` | Leave & Shift Swaps | 49 | Leave approval lifecycle, automatic allocation release (`leave_approved`), shift swap two-way qualification. |
| `MyRosterAndCoverageEndpointTests.cs` | My Roster & Coverage | 25 | Scoped schedule queries, clock-in/out state machine, double clock-in rejection, ward coverage calculations. |
| `StaffReportsEndpointTests.cs` | Management Reports | 57 | Coverage compliance calculation, leave department aggregates, agent performance metrics and latencies. |
| **Total Test Regression Suite** | **All Implemented Modules** | **314 Passed** | **100% Pass Rate across all 38 implemented endpoints.** |

---

## 18. Frontend and Mobile Responsibilities

The CareLanka system splits frontend responsibilities across two applications based on user persona:

### 18.1 React Web Application (`web-ui`)
- **Primary Persona:** `HospitalAdministrator`
- **Planned Responsibilities:**
  1. **Staff Directory (`/staff`):** Search, view, edit, activate/deactivate staff, and manage skill certifications.
  2. **Shift Roster Grid (`/roster`):** Visual calendar view of shifts by ward with drag-and-drop or modal-based allocation.
  3. **Ward Coverage Dashboard (`/coverage`):** Real-time monitoring board highlighting understaffed and critical wards in red/amber.
  4. **Leave Approval Queue (`/leave-requests`):** Review pending leave and shift swap requests with automated conflict warnings.
  5. **Roster Proposal Review (`/roster-proposals`):** Dedicated approval interface displaying proposed cascading swaps, agent rationale, and one-click approval/rejection.
  6. **Staff Reports (`/reports/staff`):** Visual charts for ward coverage compliance, leave metrics, and agent performance.
- **Current Status:** 0% implemented. Routes and components need to be added to `web-ui/src/App.tsx`.

### 18.2 Flutter Mobile Application (`mobile-ui`)
- **Primary Persona:** `GeneralStaff` (and Ward Nurses / Doctors on duty)
- **Planned Responsibilities:**
  1. **My Shifts (`MyShiftsScreen`):** Chronological agenda view of personal upcoming shifts.
  2. **Clock-In / Clock-Out:** One-tap action button on active shift cards calling `/me/allocations/{id}/clock-in` and `/clock-out`.
  3. **Leave Request Form (`RequestLeaveScreen`):** Date picker and reason submission interface.
  4. **Shift Swap Proposal (`RequestSwapScreen`):** Select peer clinician and target shift for peer-to-peer exchange.
- **Current Status:** 0% implemented. Directory `mobile-ui/lib/features/staff/` exists with empty scaffold directories (`.gitkeep`).

---

## 19. Known Gaps and Future Work

1. **Roster Proposal Endpoints (`/roster-proposals`):** Implement the 6 missing OpenAPI operations in `RosterProposalsController` to allow creating, listing, viewing, approving, and rejecting agent proposals.
2. **Staff Allocation Agent Solver:** Implement the deterministic C# cascading swap solver in `api/Agents/Staff/` to generate compliant `AgentWorkflow` proposals.
3. **OpenAPI Codegen Synchronization:** Run `npm run codegen` in `web-ui` and `dart run swagger_parser` in `mobile-ui` against the running API to produce strongly-typed TypeScript and Dart client libraries for all Staff endpoints.
4. **Web UI Development:** Implement the administrative React screens in `web-ui/src/pages/`.
5. **Mobile UI Development:** Implement the staff mobile screens in `mobile-ui/lib/features/staff/`.
