# CareLanka: Hospital Management System — Component Plan

**SE3090 — Assignment 1**

Four business components, one per team member, sharing one API and one database.
Each component has its own AI agent, and every AI recommendation is approved by
a human before it takes effect.

---

## 1. What the system is

One hospital, multiple wards. The system coordinates hospital operations across
four areas: emergency response, staff management, equipment management and
patient management. Each area is owned by one team member.

| Layer | Technology |
| :--- | :--- |
| Mobile | Flutter (Dart) |
| Web | React (plain React + Vite, not Next.js) |
| API | ASP.NET Core Web API (.NET 8) |
| Database | PostgreSQL via Entity Framework Core |
| AI | Agentic AI subsystem, four agents |
| CI | GitHub Actions |

---

## 2. User roles

The assignment requires **at least three user roles** with different
responsibilities and permissions (§4.1). We have seven. Role names match the
`StaffRole` enum in the database schema.

| Role | Uses | Owned by | What they can do |
| :--- | :--- | :--- | :--- |
| **Ambulance Crew** | Flutter | Member 1 | Receive dispatch, navigate to the scene, update run status, hand over patient info |
| **Duty / Dispatch Manager** | React | Member 1 | Oversee emergency calls, approve dispatch reassignments, approve ICU and downgrade bed decisions |
| **Hospital Administrator** | React | Member 2 | Manage staff records, approve rosters and reallocations, approve leave |
| **General Staff** | Flutter | Member 2 | View own shifts, clock in/out, request leave or a shift swap |
| **Equipment & Inventory Manager** | React | Member 3 | Monitor stock and maintenance, approve procurement and servicing |
| **Ward Nurse** | Flutter | Member 4 | Admit patients, approve normal-ward beds, update patient status, complete missing details, request discharge |
| **Patient** | Flutter | Member 4 | Book a visit, view own admission status, ward/bed and discharge details. Read-only, own record only |

**Two things worth being clear about:**

1. **A role is not a component.** The Ward Nurse role is used by Member 4's
   screens, but a nurse also reports faulty equipment (Member 3) and views their
   own shift (Member 2). Roles cut across components.
2. **A patient *record* is not a patient *account*.** Staff create a `Patient`
   row for every arrival. The account is optional and separate — an unconscious
   emergency arrival has a record and no login, forever. When an account does
   exist it is linked to the record afterwards, and is only a read-only window
   onto it.

### 2.1 Why React and Flutter are genuinely different

The assignment requires "meaningful and different purposes" for the two apps
(§4.1), and §7 and §8 spell out what each is for:

| | React (web) | Flutter (mobile) |
| :--- | :--- | :--- |
| **Purpose** | Administration, dashboards, reporting, AI monitoring and approval | User-facing and operational work done away from a desk |
| **Who uses it** | Managers and administrators | Field workers, ward staff, patients |
| **Typical action** | "Approve this roster / this bed / this purchase" | "I'm on my way", "bed confirmed", "where am I staying?" |

The rule of thumb: **React decides, Flutter does.** Anything that reviews an AI
recommendation and says yes or no belongs in React. Anything done while walking
around a ward, sitting in an ambulance, or lying in a bed belongs in Flutter.

---

## 3. Who builds what

Assignment §3 requires every student to contribute across the backend,
database, React, Flutter, tests and their own AI agent. **Every member builds
screens in both apps.**

| | **Member 1** Emergency | **Member 2** Staff | **Member 3** Equipment | **Member 4** Patient |
| :--- | :--- | :--- | :--- | :--- |
| **Owns (data)** | EmergencyCall, Ambulance, Dispatch, DispatchCrew, RouteLog | Shift, Allocation, LeaveRequest, Skill, StaffMemberSkill, WardStaffingRule | EquipmentType, EquipmentItem, StockLevel, MaintenanceSchedule, Warning | Patient, Admission, Ward, Bed, BedAssignment, BedReservation, Discharge, DischargeChecklistItem, Appointment |
| **React screens** | Live call board, dispatch approvals, route/map view, call outcome report | Staff records CRUD, roster approval, ward coverage dashboard, leave approval | Stock dashboard, warning queue, procurement/maintenance approval | Admissions dashboard, bed board, ICU/downgrade bed approval, discharge confirmation, occupancy report |
| **Flutter screens** | Crew: receive dispatch, navigate, update status, handover. Patient: make emergency call | Staff: my shifts, clock in/out, request leave, request swap | Ward staff: report faulty equipment, view ward stock, take a bed out of service | Nurse: approve normal-ward bed, update status, complete details, request discharge. Patient: my stay, book a visit, discharge instructions |
| **AI agent** | Dispatch & Routing | Staff Allocation | Equipment Monitoring | Patient Admission & Bed |
| **Device feature** | GPS + maps | Date/time picker for leave dates | Camera for fault photos | Local notifications on status change, date/time picker for booking |
| **Third-party API** | Maps / navigation | — | — | — |

> Only one third-party integration is required for the whole system (§4.1), and
> Member 1's maps API covers it. Others are optional.

---

## 4. The components in detail

### 4.1 Emergency / Ambulance — Member 1

Handles an emergency call end to end: taking the call, finding and dispatching
the nearest ambulance, routing it, and deciding which ward the patient goes to.

- **Main functions:** log an incoming call with location; track ambulance
  availability; dispatch the nearest ambulance; route it via a maps API; record
  the call outcome
- **Agent:** given a call, proposes which ambulance, which route, which ward
- **Approval:** sending the nearest ambulance happens immediately — speed
  matters. The Duty Manager approves only when the plan pulls an ambulance off
  another job

### 4.2 Staff Management — Member 2

Manages staff records and works out who covers which ward and shift.

- **Main functions:** staff records with roles, departments and skills; shift
  scheduling; leave and swap requests; ward coverage tracking
- **Agent:** proposes a roster or a reallocation to fill gaps, matching skills
  to what the ward needs
- **Approval:** the Hospital Administrator approves before a roster goes live

### 4.3 Health Equipment — Member 3

Tracks equipment and stock, and flags what is running low or overdue.

- **Main functions:** equipment and stock tracking; maintenance and calibration
  schedules; low-stock and overdue warnings; reallocation between wards
- **Agent:** reviews stock and maintenance data and produces a prioritised list
  of warnings with a recommended action
- **Approval:** the Equipment Manager approves spend or servicing above an
  agreed threshold

### 4.4 Patient Management — Member 4

Manages a patient's stay from admission to discharge: what category of care they
need, which bed and ward they get, and when they are ready to leave.

- **Main functions:** create a patient record and an admission; record the care
  category (set by clinical staff, never the AI); maintain the ward and bed
  register; assign a bed based on availability, category and ward policy; track
  status through the stay; run the discharge checklist; give the patient a
  read-only view of their own stay
- **Agent:** **one job only — bed assignment.** Given a category already set by
  staff and the current bed availability, it proposes a specific ward and bed.
  It never diagnoses, never sets the category, never admits or discharges anyone
- **Approval:** two human gates. A **ward nurse** confirms a normal-ward bed in
  Flutter; the **Duty Manager** confirms ICU, high-dependency or any downgrade in
  React. A second human confirms discharge

**Three arrival paths** all end in the same admission workflow:

| Path | How it starts |
| :--- | :--- |
| Emergency | Member 1's call creates the record before the patient arrives |
| Booked | Patient books a visit; checks in on the day |
| Walk-in | Staff create the record at the front desk |

---

## 5. How the components stay separate

One app, one database, four owners. Ownership is a **team agreement**, not
something the compiler enforces — so it only works if we all follow it.

**The rule: one writer per table.** Everyone else reads through the owner's
service.

```csharp
// WRONG — Staff Management writing into Patient Management's table
_db.BedAssignments.Add(new BedAssignment { ... });

// RIGHT — ask the owner's service to do it
await _bedService.ReserveAsync(admissionId, bedId);
```

Why it matters: if two components write the same table, no one can reason about
its state, and the audit trail stops being trustworthy. It also means a bug in
your component can only ever corrupt your own tables.

**`Ward` is the exception to watch.** It belongs to Patient Management but is
referenced by Staff (`Shift.WardId`), Equipment (`EquipmentItem.WardId`,
`StockLevel.WardId`) and Emergency (`Dispatch.DestinationWardId`). Treat its
schema as frozen once agreed — changing it breaks three other people.

Detailed boundaries, and what each member needs from the others, are in
[`specs/integration_of_functions.md`](specs/integration_of_functions.md).

---

## 6. The end-to-end workflow

One emergency call triggers all four agents, then one human approves the plan.
This is the cross-platform workflow the assignment requires in §4.1, and the one
assessed Agentic AI workflow §9.1 requires.

How the four agents get chained together is designed in
`specs/ai-orchestration-workflow.md`, once the agents themselves exist.

```text
Emergency call comes in                              [Flutter — M1]
        |
        v
Dispatch & Routing Agent                             [M1]
  -> proposes ambulance + route + destination ward
        |
        v
Patient Admission & Bed Agent                        [M4]
  -> checks bed availability, proposes admission + bed
        |
        v
Staff Allocation Agent                               [M2]
  -> checks ward staffing, flags if short-staffed
        |
        v
Equipment Monitoring Agent                           [M3]
  -> checks required equipment is at that ward
        |
        v
Duty Manager reviews the whole plan                  [React — M1/M4]
        |
   +----+----+
   v         v
APPROVE   REJECT / REVISE
   |
   v
Crew and ward nurse get their tasks                  [Flutter — M1/M4]
        |
        v
Patient arrives, is admitted, and can see
their own ward and bed on their phone                [Flutter — M4]
```

Every step — who proposed what, who approved it, and when — is persisted, so the
whole chain can be replayed later.

---

## 7. Checked against the assignment

| Requirement | Where it is met |
| :--- | :--- |
| 4 components, one per student (§3) | Emergency, Staff, Equipment, Patient |
| Every student across the full stack (§3) | §3 table — each member has React screens, Flutter screens, an agent and owned data |
| At least 3 user roles (§4.1) | 7 roles in §2 |
| React and Flutter have different purposes (§4.1) | §2.1 — React approves and reports, Flutter does the work |
| ≥4 endpoints + 1 non-CRUD operation per student (§5) | Each member's own `specs/*-spec.yaml` |
| At least one third-party integration (§4.1) | Maps API for ambulance routing |
| One cross-platform workflow (§4.1) | §6 — starts in Flutter, approved in React, result returns to Flutter |
| 4 distinct agents with allow-listed tools (§9) | One per member, each with its own data and job |
| Human approval before high-impact actions (§9) | Dispatch reassignment, rosters, equipment spend, bed assignment, discharge |
| A meaningful device feature (§8) | Notifications, date/time picker, GPS, camera |
| Not a medical diagnosis system | The patient agent only handles logistics using a category a human already set |

---

## 8. Still to be agreed

Group-level decisions that are not one member's call.

| # | Decision | Why it matters |
| :--- | :--- | :--- |
| 1 | **Who owns the orchestration, and the shared agent-workflow tables** | `AgentWorkflow` and `AgentProposedChange` are used by all four agents, and the rubric scores orchestration and state as a *group* criterion. `specs/ai-orchestration-workflow.md` proposes a fifth group-owned Coordinator Agent above the four domain agents, plus one shared table design, group-owned. Decide once the four agents exist |
| 2 | **Enum storage strategy** | Native PostgreSQL enums vs `int` vs string conversion. Affects every migration and is worth an ADR entry |
| 3 | **Flutter state management** | `provider`, Riverpod or Bloc. All four of us must use the same one, and switching after screens are built is painful — see `mobile-ui/README.md` |
| 4 | **Notification delivery** | In-app only, device push, or SMS. Decides whether the backend needs a push integration |
| 5 | **Deployment target** | Where the API, database and React app are hosted, and what the CI pipeline deploys to |
