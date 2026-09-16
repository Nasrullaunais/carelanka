# How the Four Components Connect

**CareLanka Hospital Management System · SE3090 Assignment 1**
**Status:** living document · all four components' boundary sections are now filled in — Patient (§4–§11), Equipment (§13–§16), Staff (§17–§21), Emergency (§22–§26)

This file explains where one member's component ends and another's begins, and exactly what crosses the line.

---

## 0. How to use this file

### If you are a team member

Read it before you design your component, and again before you write your `*-spec.yaml`. It will stop you building something a teammate already owns, and stop you assuming a teammate will build something nobody agreed to.

### If you are an AI assistant helping a team member

**Read this file before designing anything, writing any spec, or running a design or grilling session.** It is the shared contract between four people working in one repository.

Then follow these rules:

| | Rule |
| :--- | :--- |
| 1 | **Read before you write.** Check the ownership map (§3) before proposing any entity, table or endpoint. If another member already owns it, do not design it again. |
| 2 | **Update only your own member's sections.** Add or correct what *their* component owns, provides and needs. |
| 3 | **Never change another member's workflow, ownership or contracts.** Not to "improve" them, and not to make your member's design fit more neatly. |
| 4 | **Need something from another component? Ask, don't invent.** Add it to §10 as a request. Do not write an endpoint into someone else's spec on their behalf. |
| 5 | **Think another member's section is wrong? Flag it, don't fix it.** Add it to §11 Open Items with your reasoning, and let the two members settle it. |
| 6 | **Keep the ownership map (§3) accurate.** It is the first thing anyone reads. If your member's ownership changes, change it here in the same edit. |
| 7 | **Mark unconfirmed things as open.** A decision one member made alone is not a group decision. Write it as open, not as settled. |

The point of these rules: four people are editing one repository. Silent disagreements about who owns what surface in week eight, when they cost days.

---

## 1. The most important thing to understand first

**We are building ONE ASP.NET Core application on ONE PostgreSQL database.** The assignment requires it:

> "React and Flutter must use the same ASP.NET Core Web API, PostgreSQL database, user identity, permissions and business rules. Disconnected prototypes will not satisfy the assignment."

So "integration" here does **not** mean four separate services calling each other over HTTP. There is one API project, one database, four owners. All four components are folders inside the same solution:

```
api/
  Controllers/  Common/  Emergency/  Staff/  Equipment/  Patient/   thin: bind, delegate, return
  Services/     Common/  Emergency/  Staff/  Equipment/  Patient/   business logic + data access
  DTOs/         Common/  Emergency/  Staff/  Equipment/  Patient/   no `Dto` suffix
  Agents/                                                           the AI agents
  Data/
    CareLankaDbContext.cs      one context; OnModelCreating is one line and nobody edits it
    Entities/{Component}/      base classes at the top level, your entities in your folder
    Configurations/{Component}/  your IEntityTypeConfiguration classes — zero shared lines
    Enums/                     one file per enum
    Migrations/                generated; pull immediately before, push promptly after
  Common/                      cross-cutting infrastructure, group-owned
    Auth/                      policy names, claim names, JwtOptions, rate-limit policy names
    Errors/                    MessageCode, ErrorMessages.resx, the one IExceptionHandler
    Exceptions/                ApiException and its subclasses
    Persistence/               enum wire format, value converters, SaveChanges interceptors
```

`Common/` is infrastructure every component uses and nobody owns individually. Read it,
import from it, do not fork it — a second copy of the exception handler or the enum
converter looks right and behaves differently.

That makes the ownership rules a **team convention**, not something the compiler enforces. Which is exactly why they need writing down.

The Agentic AI subsystem is the one part that may live outside the API (a Python service, for example). If it does, it is called *by* ASP.NET Core and never directly by React or Flutter — §2 of the assignment is explicit about that.

---

## 2. The one rule

> **One writer per table. Everyone else reads through the owner's service interface.**

Both halves matter.

**One writer.** Only Patient Management's code writes to `BedAssignment`. Only Equipment's code writes to `Bed`. If your feature needs to change a table you don't own, call the owner's service and let their business rules run — do not write your own `DbContext` query against their tables.

**Read through the interface, not the tables.** When Emergency needs bed counts, it injects `IPatientCapacityService` and calls a method. It does not write `_db.Beds.Where(...)` in an Emergency controller.

Why this matters more than it looks:

- The owner can change their schema without breaking three other people's code
- Business rules live in one place — an expired bed hold is handled once, in the service, rather than re-implemented (differently, wrongly) by whoever queries the table next
- At the viva you can point at a clean boundary and explain it. "We all query each other's tables directly" is a bad answer to a question about architecture
- The rubric scores **Integrated Architecture** at 10 group marks

### What this looks like in code

```csharp
// Emergency Service needs to know where to send an ambulance.

// WRONG — reaching into another component's tables
var freeBeds = await _db.Beds
    .Where(b => b.Condition == BedCondition.Usable && !b.Assignments.Any(...))
    .CountAsync();

// RIGHT — asking the owner
public class DispatchService(IPatientCapacityService capacity)
{
    var summary = await capacity.GetWardCapacityAsync();
}
```

The second version keeps working when hold expiry, out-of-service beds or a new ward type appear. The first quietly goes wrong and nobody notices until the demo.

---

## 3. Ownership map

| Entity | Owner | Who reads it | Who writes it |
| :--- | :--- | :--- | :--- |
| `EmergencyCall`, `Ambulance`, `AmbulanceCrewAssignment`, `Dispatch`, `DispatchCrew`, `RouteLog` | **Emergency (M1)** | Patient (narrow caller tracking), Staff (none directly) | Emergency only |
| `StaffMember`, `Shift`, `Allocation`, `LeaveRequest` | **Staff (M2)** | All — everyone stores staff IDs | Staff only |
| `EquipmentItem`, `EquipmentCategory`, `PharmacyItem`, `PharmacyCategory`, `PharmacyTransaction`, `MaintenanceSchedule`, `Warning`, `ActionRequest` | **Equipment (M3)** | Patient (ward equipment readiness); any staff (search/availability) | Equipment only |
| **`Bed`** — exists, number, condition, repairs | **Equipment (M3)** | Patient (to find candidates) | Equipment only |
| **`LabReport`** — a finished laboratory result and the file itself | **Equipment (M3)** — *claimed 2026-09-13, see §11.15* | Doctor, ward nurse, duty manager | Equipment only (the laboratory) |
| `Patient`, `Admission`, `Appointment`, `Discharge`, `DischargeChecklistItem` | **Patient (M4)** | Emergency, Staff (aggregates only) | Patient only |
| **`PatientMedicalProfile`** — conditions, allergies, current symptoms, recent situation | **Patient (M4)** — *added 2026-09-16, see §11.17* | Nobody else. Not published cross-component and not patient-readable | Patient only — ward nurse or doctor |
| **`CareRecommendation`** — a patient's own report and the drafted reply | **Patient (M4)** | Nobody else | Patient only |
| **`Bill`, `BillLineItem`** — what a visit costs and whether it is paid | **Patient (M4)** — *claimed 2026-09-11, see §11.10* | Nobody yet | Patient only |
| **`BillingRate`, `AdmissionFeeRate`** — what the hospital charges | **Patient (M4)** — *added 2026-09-11, see §11.13* | Nobody yet | Read: any staff. Write: administrator only |
| **`BedAssignment`** — who is in a bed, holds, approvals | **Patient (M4)** | Equipment (before servicing a bed) | Patient only |
| `Ward` — name, type, gender policy | **Patient (M4)** — *see §11.1* | All | Patient only |
| `AgentWorkflow`, `AgentProposedChange` | **Common (group-owned)** — *DECIDED §11.2, **and still not built anywhere** as of 2026-09-16* | All five agents | All five agents, by `workflow_id` |
| `StaffMember`, `PatientAccount`, `RefreshToken`, login, JWT issuing | **Common (group-owned)** — `specs/common-spec.yaml` | All | Common only |
| `AuditLog`, `Notification`, `DeviceToken` | **Common (group-owned)** | All | Written by the audit interceptor, never by hand |

**The rule for everything else: if it does not belong to a specific member, it is
common.** Common parts are group-owned and built **once**, not four times.
That covers auth, the JWT, the exception handler, the audit interceptor, the
`DbContext`, the base entity classes, the agent workflow tables and the Coordinator
Agent. If you are about to build something that is not in your component's row above,
stop and check whether it is common.

**Roles are not owned by anyone.** The shared part is only the plumbing — one
`StaffMember` table, one login endpoint, one JWT issuer — so all four components
read the same token. **What each role is allowed to do is decided by the component
that role acts in:** Ward Nurse permissions come from Patient Management,
Equipment Technician from Equipment, and so on. Add a role to `StaffRole` when
your component needs it; that is not a request to anyone.

---

## 4. Patient Management ↔ Emergency Service (Member 1)

### 4.1 A patient raises an emergency call — DECIDED

A logged-in patient opens Flutter and taps **"I need an ambulance."**

| Piece | Owner |
| :--- | :--- |
| The screen in the patient app | **Patient (M4)** — it is a patient-role screen |
| The `POST /api/emergency-calls` endpoint | **Emergency (M1)** |
| The `EmergencyCall` record and everything downstream | **Emergency (M1)** |

M4 builds a form. M1 builds the data and the workflow. The form posts to M1's endpoint.

**Minimum details only.** The form asks what happened, whether the call is for the caller
or another person, and captures a correctable location. Everything else waits.

**The screen stays with M4; Emergency owns the contract and routing.** Patient Management's
Flutter screen captures a correctable GPS point and posts it to M1. Emergency validates and
stores the scene, calculates route/ETA on the backend, and returns narrow tracking. Crew
driving launches Google Maps; neither component builds turn-by-turn navigation.

**Why it is worth doing at all:** because the caller is logged in, we already know exactly who they are — NIC, age, gender, contact, past visits. The emergency call carries a `patient_id`, so the pre-admission starts complete instead of guessing.

That gives the demo a good contrast: the same system handling a known patient who called from their own phone, and an unidentified patient who arrives unconscious as `UNKNOWN-2026-0142`.

**The caller is often not the patient.** The emergency screen asks one question — *"Is this for you, or someone else?"* — because the person calling is best placed to answer it, and guessing later is worse. If it is for them, the admission links to their existing record and starts complete. If it is for someone else, a new patient record is created from whatever the caller can say, and **the caller is recorded as the emergency contact** — they are standing next to the patient and know how to be reached.

M1 needs to carry that answer through to us. See §4.2.

### 4.2 Dispatch happens → Patient Management pre-admits

When M1 dispatches an ambulance to this hospital, M4 needs to know so a bed can be found **before the ambulance arrives**.

**Emergency → Patient:**

```json
{
  "dispatch_id": "DSP-2026-0142",
  "caller_user_id": "uuid",
  "patient_is_caller": false,
  "patient_id": null,
  "provisional_name": "father of caller, approx 60",
  "provisional_gender": "male",
  "expected_arrival": "2026-08-19T14:30:00Z",
  "urgency": "emergency"
}
```

**`patient_is_caller` is the field that matters**, and M1 must pass it through from the question asked on the call screen.

| `patient_is_caller` | What Patient Management does |
| :--- | :--- |
| `true` | `patient_id` is set; link the admission to that existing record. Details usually complete already. |
| `false` | Create a new patient from `provisional_name` / `provisional_gender`, or a `temp_reference` if nothing is known. Record `caller_user_id` as the emergency contact and as `reported_by_user_id`. |

Getting this wrong means filing one person's emergency under another person's medical record, so it is asked explicitly rather than inferred.

Patient Management then creates an `Admission` with `source = emergency` and `dispatch_id` set, in status `awaiting_bed`.

Ward and bed preparation starts from this notification and remains entirely Patient
Management's decision. Emergency routes to the configured CareLanka Hospital emergency
entrance and sends no ward choice.

**Settled for this release (§11.3):** Emergency calls Patient Management after the
dispatch commits. A future Coordinator may orchestrate the same published operation.

### 4.3 Patient Management prepares the hospital side

This release serves one CareLanka Hospital emergency entrance. Emergency does not choose a
ward or bed and does not need ward capacity to dispatch. After a confirmed dispatch commits,
it calls `POST /admissions/pre-admit`; Patient Management applies its own capacity and
clinical workflow. A timeout or failure is recorded for retry and never rolls back or delays
the ambulance.

---

## 5. Patient Management ↔ Staff Management (Member 2)

This is the boundary that confuses people most, because staff are all over the patient workflow — nurses admit, doctors clear discharges, managers approve beds. **None of that makes patient data theirs, or staff data ours.**

### 5.1 We store staff IDs. We never store staff data.

Patient Management records *who did what*, for the audit trail. Each of these is a foreign key to `StaffMember`:

| Our field | What it records |
| :--- | :--- |
| `Admission.category_set_by_staff_id` | Which clinician chose the care category — proof a human decided it |
| `BedAssignment.approved_by_staff_id` | Who approved the bed |
| `Discharge.confirmed_by_staff_id` | Who confirmed the discharge |
| Checklist item ticks | Who ticked each one |

We store the **ID only**. Name, role, department, qualifications, shift and leave all live in `StaffMember` and belong to M2. When React needs to show "Approved by Dr. Perera," we ask M2's service for the name at read time.

**Why not copy the name in?** Because a staff member's name or role can change, and we would be showing stale data with no way to notice. The ID is the fact; everything else is theirs to serve.

### 5.2 The doctor and clinical clearance

The discharge checklist has one item — `clinical_clearance` — that only a doctor may tick.

There is **no new entity and no coordination** for this. A doctor is a `StaffMember` with `role = doctor`, owned by M2. Patient Management checks the role claim on the JWT:

```csharp
[Authorize(Roles = "Doctor")]
public async Task<IActionResult> SetClinicalClearance(...)
```

M2 owns the staff record. M4 owns the rule about who may tick which box.

### 5.3 Staff reads ward occupancy to work out staffing demand

> "ward staffing demand (**read-only, from Patient Management** and Emergency)" — docs/CareLanka_Component_Plan.md §2

```json
{
  "ward_id": "uuid",
  "name": "Ward 5B",
  "ward_type": "general",
  "occupied_beds": 17,
  "total_beds": 24,
  "patients_by_category": { "icu": 0, "hdu": 2, "inpatient": 15 },
  "incoming_next_2h": 3
}
```

`patients_by_category` is what makes this useful — fifteen routine inpatients and two high-dependency patients need very different staffing, even though both are "seventeen patients."

`incoming_next_2h` counts admissions in `bed_reserved` arriving within the window, so M2's agent can staff *ahead* of a rush instead of reacting to one.

Counts only. **No patient identities cross this boundary.**

### 5.4 Optional: staffing feeding back into bed choice

Patient Management's agent ranks candidate beds on soft rules. A further one could be *"prefer a ward that is not short-staffed right now"*, reading M2's coverage data.

**Nice, but not in the first build.** It couples two agents to each other, and both components need to work alone first.

---

## 6. Patient Management ↔ Equipment Management (Member 3)

### 6.1 The bed split — DECIDED

A hospital bed is two things at once, and using one word for both is what caused the confusion:

| Thinking of it as… | Means | Owner |
| :--- | :--- | :--- |
| A **physical asset** — frame, condition, repairs, adding and removing beds | Inventory | **Equipment (M3)** |
| **Who is currently in it** — holds, approvals, occupancy | Capacity | **Patient (M4)** |

**Equipment owns the `Bed` table.** They create beds, retire them, and mark them out of service for repair or servicing.

**Patient Management owns `BedAssignment`.** Who is in which bed, the 30-minute hold on a proposed bed, who approved it, and when it was released.

**Why this works cleanly — there are no cross-writes at all.** Occupancy is not a column on `Bed`; it is the presence or absence of a live row in `BedAssignment`. So:

```
"Is bed 12 free?"
    = it exists in Equipment's Bed table          (M3's data, M4 reads)
    AND its condition is 'usable'                 (M3's data, M4 reads)
    AND no live BedAssignment references it       (M4's data)
```

M4 never writes to `Bed`. M3 never writes to `BedAssignment`. Two reads, zero shared writes — which is what makes this better than putting an `is_occupied` column on `Bed` and having two people fight over it.

### 6.2 Servicing a bed — Equipment asks first

Equipment's Monitoring Agent decides a bed frame is overdue for servicing. Before taking it out of circulation, it asks whether anyone is in it:

```
Equipment's Monitoring Agent
  "Bed frame in Ward 5B is overdue for servicing"
        │
        │  asks M4: is this bed occupied or held?
        ▼
Patient Management answers               [M4 read]
        │
   ┌────┴────────────────┐
occupied              free
   │                     │
   │ M3 waits            │ M3 sets Bed.condition = out_of_service   [M3 write]
   │ for discharge       │
   │                     ▼
   │            Patient Management's Bed Agent
   │            now has one fewer candidate bed
   │                     │
   │                     ▼
   │            If that tips the ward to full, the agent
   │            suggests a downgrade — which always needs
   │            a Duty Manager to commit it
```

**The hard rule: maintenance never evicts a patient.** If the bed is occupied or under a live hold, Equipment waits. M4 exposes the check; M3 respects the answer.

One equipment warning ends with a human approving a different bed for a patient. **Two components, two agents, one visible consequence** — a far better demo than either agent running alone, and exactly what the rubric means by orchestration.

**Reverse direction:** when servicing finishes, M3 returns the bed to `usable` and it re-enters M4's candidate pool automatically. No call needed — M4 reads the current condition every time.

### 6.3 Ward list for equipment allocation

M3 allocates equipment *to* wards, so they read the ward list — id, name, type — from Patient Management. Small, but it means M3 never keeps their own copy of ward names that drifts.

### 6.4 A patient has a readable identifier now — `patient_code` *(added 2026-09-11, M4)*

Until now the only identifier M4 published was a Guid. Nobody reads `3f9c1a2e-8b44-4f31-9a7d-2c05e6b7d813` off a wristband and nobody types it into a form correctly, so any component that has to name a patient on its own screen had nothing usable to name them by.

Every patient now carries a second identifier, and the two do different jobs:

| | `id` | `patient_code` |
| :--- | :--- | :--- |
| Shape | Guid | eight characters, `P7K2X9QM` |
| For | every stored reference, every FK, every URL | a human, out loud and on paper |
| Changes? | never | never |

**It is published on `PatientSummary`,** so it appears on every response any component already reads that carries a patient: `Patient`, `PatientDetail`, `Admission.patient`, `Appointment.patient`, `WorklistRow.patient`. Nothing new to call for it.

**Nothing stores it as a reference.** A foreign key is still the Guid, and `EquipmentItem.assigned_to_admission_id` is unchanged — the code is what a person carries between screens, not what a table carries between rows.

**`search` matches it** on `GET /patients`, `GET /admissions` and `GET /patient-worklist`, alongside name and NIC. That is the whole of what M4 provides here. **What any other component does with the code is theirs to design** — M4 has built nothing on anyone else's side and is not proposing to.

---

## 7. The rule that binds all four components

> **No component, and no AI agent, decides a patient's care category.**

`admission_category` (`icu` / `hdu` / `inpatient` / `day_case` / `outpatient`) is set by clinical staff and recorded with `category_set_by_staff_id`. It is an **input** to Patient Management's agent, never an output.

This is not caution — it is written into the group plan:

> "The AI never decides a patient's medical condition or diagnosis — it only works with the administrative category and checklist that clinical staff have already set." — docs/CareLanka_Component_Plan.md §4

> "**Not a medical diagnosis system** — The Patient agent only works with administrative categories already set by staff — it never diagnoses" — docs/CareLanka_Component_Plan.md §6

Emergency passes administrative urgency for pre-admission, but no care category, ward or bed.

---

## 8. The full emergency scenario, with owners marked

```
Patient taps "I need an ambulance" in Flutter
  screen: M4   |   endpoint + EmergencyCall record: M1
        │
        ▼
Dispatch & Routing Agent                                        [M1]
  deterministic code filters eligible ambulance + current crew
  Google supplies backend route/ETA; agent ranks and explains
  Duty Manager confirms (or dispatches manually if dependencies fail)
        │
        │  dispatch notification: dispatch_id, patient_id, ETA, urgency
        ▼
Pre-admission created, status = awaiting_bed                    [M4]
  clinical staff set admission_category                       (human)
        │
        ▼
Bed & Patient Details Agent                                   [M4]
  reads Equipment's bed register  ────────read──────────────►   [M3]
  filters on hard rules H0-H6, ranks on soft rules
  suggests a best bed plus every other bed that passed
  deterministic validator re-checks every hard rule
  WRITES NOTHING and holds no bed — changed 2026-09-16.
  The 30-minute hold is written when a human commits, by
  POST /admissions/{id}/assign-bed, under a row lock.
        │
        ▼
Staff Allocation Agent                                          [M2]
  reads ward occupancy + incoming  ────────read──────────────►  [M4]
  flags the ward as short-staffed, proposes a reallocation
        │
        ▼
Equipment Monitoring Agent                                      [M3]
  checks the Patient-selected ward has the equipment it needs
  (before servicing any bed, asks M4 whether it is occupied ─►  [M4])
        │
        ▼
Duty Manager reviews the whole plan in React
        │
   ┌────┴────┐
APPROVE   REJECT / REVISE
   │           └──► back to the relevant agent; admission stays awaiting_bed
   ▼
Bed approval re-checked under a row lock, then committed        [M4]
Ambulance crew + ward nurse get their tasks in Flutter        [M1/M4]
   │
   ▼
Patient arrives, marked admitted, assignment becomes occupied   [M4]
Patient sees "Ward 5B, Bed 12" on their own phone               [M4]
```

Every arrow between components is either **read-only** or **a call to the owner's service**. Nobody writes into anybody else's tables.

---

## 9. Contracts Patient Management provides

Injected as interfaces inside the API, and exposed as REST endpoints so the AI agents (which may run outside ASP.NET Core) can reach them.

| Interface method | Endpoint | For | Returns | Status |
| :--- | :--- | :--- | :--- | :--- |
| `ICapacityService.GetWardCapacityAsync()` | `GET /api/capacity/wards` | M1 | Free/total beds per ward, with type and gender policy | **Live 2026-09-11** |
| `ICapacityService.GetWardOccupancyAsync(wardId)` | `GET /api/wards/{id}/occupancy` | M2 | Occupied counts, care mix, incoming next 2h | **Live 2026-09-11** |
| `IWardService.ListAsync()` | `GET /api/wards` | M3 | Ward id, name, type | Live 2026-09-09 |
| `IBedOccupancyService.GetStatusAsync(bedId)` | `GET /api/beds/{id}/occupancy` | M3 | Whether a bed is occupied or held — **check this before servicing it** | **Live 2026-09-11** |
| `IBedAssignmentService.ListAvailabilityAsync(…)` | `GET /api/bed-availability` | M4, and the bed agent | Equipment's register joined with our assignments, hold expiry applied | **Live 2026-09-11** |
| `CreatePreAdmissionAsync(dispatch)` | `POST /api/admissions/pre-admit` | M1 | Creates an admission from a dispatch | Not built |

**Every response carrying a patient now carries `patient_code` as well** — eight characters,
the handle a human uses where a Guid cannot be read out or typed in. §6.4 says what it is and
what it is not; §11.8 records the one permission change that came with it.

**`GET /api/patient-worklist` is Patient's own screen, not a contract for anybody else.**
Listed here only so nobody claims the route: it unions Patient's `Appointment` and `Admission`
tables into one ward board so the patients screen can say "not arrived" about somebody who has
booked but not turned up. Read-only, `AdmissionReader` roles, and its `WorklistStatus` is
derived from the two stored statuses rather than being a third one. Other components should
keep reading `GET /api/capacity/wards` and `GET /api/wards/{id}/occupancy` for counts — this
one carries patient identities and is the desk's view, not an aggregate.

**`POST /api/admissions/{id}/complete` finishes a visit that never needed a bed** — an
`outpatient` scan or blood test. Not the discharge workflow: a visit holding a bed is refused
with `cl_pat_020`, because discharge releases a bed and Equipment's register has to see that
happen. Nothing outside Patient calls it.

All JWT-protected and role-restricted. Aggregate endpoints return **counts, never patient identities** — `CapacityEndpointTests` asserts a patient's name appears in neither response body.

**Both capacity reads are `AnyStaff`.** Any authenticated staff token may call them, so Emergency and Staff Management need no special role for their own agents.

**`GET /beds/{id}/occupancy` is Equipment's side of the same rule, and M3's port over it is
already written** — `IBedOccupancyPort` in `Services/Equipment/`, now backed by
`BedOccupancyAdapter` instead of the stub that answered "occupied" for every bed. It is
`Scoped`, not `Singleton`, because it reaches a `DbContext`. **A lapsed hold does not block
servicing**, and an unknown bed id is a 404 rather than a confident "free".

**The free/occupied rule is written once, in `BedHold`, and §4.3's warning is the reason.** A bed is free when it exists in Equipment's register, its condition is `usable`, and no live `BedAssignment` of ours references it — where "live" means occupied, or reserved with a `reserved_until` still in the future. **A hold past its expiry counts as free with nobody having done anything to it.** Re-implementing that on the calling side is the mistake §4.3 spells out: it works today and quietly goes wrong the first time a hold lapses.

*(It lived in `CapacityService` until 2026-09-11, when bed availability, manual assignment and the occupancy answer became three more readers of it. Same rule, one home, four callers.)*

**One thing no caller can apply for itself:** the partial unique index `ux_bed_assignments_live_bed` covers any `reserved` row and cannot consult a clock, so a lapsed hold reads as free and still refuses the next `INSERT`. Whatever writes an assignment must close lapsed holds on that bed first. Only Patient Management writes `BedAssignment`, so only we have to know this — recorded here so it is not rediscovered.

**Define your own port over these, the way M4 does over M3's bed register.** `IPatientCapacityService` on Emergency's side (§4.3) and whatever M2 calls theirs, each delegating to `ICapacityService`. Same pattern as `IBedRegistryService` → `IBedService`, so one file breaks if a signature changes rather than every caller.

## 10. What Patient Management needs from others

| From | What | Why |
| :--- | :--- | :--- |
| **M1** | Dispatch notification — `dispatch_id`, `patient_id` (nullable), `expected_arrival`, `urgency` | Triggers pre-admission so a bed is ready before arrival |
| **M1** | An endpoint our patient-app screen can post an emergency call to | §4.1 |
| **M1** | Scene-coordinate contract, validation, tracking and backend route/ETA | §4.1 — M4 owns the patient screen and device capture; M1 owns Emergency processing |
| **M1** | `caller_user_id` and `patient_is_caller` on the dispatch notification | §4.2 — without these we cannot tell whose medical record this is |
| **M2** | Look up a staff member's name and role by ID | Displaying "Approved by …" without copying their data |
| **M2** | `Doctor` as a role on the JWT | Gating `clinical_clearance` |
| **M3** | A readable bed register: bed id, ward, number, condition, isolation capability | Our agent's candidate list. **This is our hardest dependency** — without it the bed agent has nothing to reason over. |
| **M3** | Notification (or just a condition change we can read) when a bed goes in or out of service | §6.2 |
| **Group** | Shared agent-workflow tables | §11.2 |

---

## 11. Open items

**11.1 — Does `Ward` sit with Patient Management or Equipment?**
Beds are settled (§6.1). Wards are not. Argument for M4: a ward's `gender_policy` and `ward_type` are admission-policy facts that drive the bed agent's hard rules — Equipment does not care whether a ward is male or female, only about frames and servicing. Written as M4's for now; M3 and the group to confirm.

**11.2 (DECIDED 2026-09-07) — The agent-workflow tables are common.**
`AgentWorkflow` and `AgentProposedChange` are one group-owned pair, built once as part of
the common bootstrap. Every component links by `workflow_id` and otherwise leaves them
alone. The HTTP surface is `specs/common-spec.yaml` — `GET /workflows`,
`GET /workflows/{workflowId}`, and the single high-impact gate
`POST /workflows/{workflowId}/{approve,reject,request-revision}`. Reasoning and
consequences are **ADR 3** in `docs/ADR.md`.

This also settles the general rule: **anything that is not a specific member's is common.**

The original question, kept for the record:
All four agents must persist workflow id, objective, plan, steps, tool results, validation results, errors, approval status and outcome (assignment §9.1). The rubric scores this under a **group** criterion — *"Integrated Architecture, Agent Orchestration and State Management (10)"* — not an individual one, and §10 requires one workflow crossing all four agents. Four separately designed schemas would make that trace a four-way join.
**Recommendation:** one shared design, group-owned, since `ai-orchestration-workflow.md` is already group-owned. Each component links by `workflow_id`.
**Needs a group decision. Not decided.**

*Update:* `ai-orchestration-workflow.md` §5 now proposes exactly this — one `AgentWorkflow` row per agent run, chained by `correlation_id` and `parent_workflow_id`, group-owned. Settle it alongside the orchestration decision in that document, since the table design follows from it.

**11.3 (DECIDED for this release) — Emergency calls Patient Management directly after
dispatch commits.** `POST /admissions/pre-admit` is non-blocking and retryable. A future
Coordinator Agent may drive the same published operation without changing either domain
contract.

**11.4 (RESOLVED) — A bystander can raise a call for someone else.** The call screen asks once; M1 passes `patient_is_caller` and `caller_user_id` through on the dispatch notification (§4.2). The caller is stored as the patient's emergency contact. **Done:** `docs/entity_diagram.md` Rev 2.4 adds `EmergencyCall.PatientIsCaller` / `CallerUserId`, and `emergency-spec.yaml` publishes both on `CreateEmergencyCallRequest` and `DispatchNotification` (§22).

**11.5 — Booking a visit.** A patient can register their details and an expected arrival ahead of a planned visit (`source = pre_registered`). This is deliberately **not** a full appointment system — no doctor calendars, no time slots, no rescheduling — because that is a component-sized feature on its own. If the group wants real appointments, it needs an owner and something else has to be dropped.

**11.6 — Name collisions across the four specs. Each row needs an owner.**
The four `*-spec.yaml` files describe **one** ASP.NET application, so routes,
`operationId`s and schema names are global, not per-component. A duplicate route
throws at startup; a duplicate `operationId` or schema name silently collides in
the generated clients, and whichever one generates second wins. None of these is
one member's call — the two or three members sharing the name have to agree.

**STATUS: CLEARED on 2026-08-21.** All four specs validate as OpenAPI 3.0.3 and the
cross-spec sweep reports **zero** route, `operationId` or schema-shape collisions.
Everything below is the record of what was fixed and why, kept so the same names are
not reintroduced.

Each of the four agent-performance reports was a *different* report about a *different*
agent — they were never a shared function, they had just independently picked the same
name. So nothing was given up; each component kept its own report under its own name.

| Was | Now | Whose |
| :--- | :--- | :--- |
| `GET /reports/agent-performance` ×4 | `/reports/{emergency,staff,equipment,patient}/agent-performance` | one each |
| `AgentPerformanceReport` ×4 shapes | `{Emergency,Staff,Equipment,Patient}AgentPerformanceReport` | one each |
| `operationId: getAgentPerformanceReport` | `get{Emergency,Staff,Equipment,Patient}AgentPerformanceReport` | one each |
| `GET /workflows/{workflowId}` in equipment + patient | Patient's is now `/bed-workflows/{workflowId}`; Equipment keeps `/workflows/{workflowId}` | **interim — see below** |
| `Bed` — two different shapes | Equipment keeps `Bed` (the physical frame); Patient's is `AdmissionBed` (adds `availability` and `occupied_by_admission_id` from `BedAssignment`) | M3 / M4 |
| `WorkflowSummary`, `WorkflowAccepted` | Patient's are `BedWorkflowSummary` / `BedWorkflowAccepted` | M4 |
| `PagedResult` — staff had `total_count` | `total_items` in all four, and `required` on all four | group-owned type |
| `staff-spec.yaml` did not validate | Fixed — the `LeaveReport` description containing a comma is now quoted, exactly the trap `CLAUDE.md` warns about | M2 |

**Second sweep, 2026-09-07** — after `specs/common-spec.yaml` was added. Five specs now,
still zero route, `operationId` and schema-shape collisions.

| Was | Now | Why |
| :--- | :--- | :--- |
| `GET /workflows/{workflowId}` in equipment | Common owns it; Equipment's richer view is `GET /equipment/workflows/{workflowId}` | §11.2 decided — the generic route is group-owned |
| `WorkflowSummary` (equipment) | `EquipmentWorkflowSummary` | Generic name now belongs to the shared shape |
| `ProposedChange`, `ProposedChangeType`, `ValidationResult` (staff) | `RosterProposedChange`, `RosterProposedChangeType`, `RosterValidationResult` | Same reason. Staff's are roster-shaped views with extra fields, so they get component names |
| `ProblemDetails` — two different `description` strings | One text in all five | Shared types must be byte-identical, not merely same-shaped |
| `StaffRole` (staff) | Byte-identical copy in common | It is the JWT role list, so it is group-owned |

**No longer open:** `/bed-workflows/{workflowId}` was an interim name pending §11.2. §11.2
is now decided and the name **stays** — deliberately, not by default. The shared trace
lives at `/workflows/{workflowId}`; `/bed-workflows/{workflowId}` is Patient Management's
own fuller view of its own runs, which is M4's individual work and worth keeping. Same
shape of answer as Equipment's.

**The rule that keeps this cleared:** the four specs describe one ASP.NET application, so
routes, `operationId`s and schema names are global. Before adding any of the three, check
it does not already exist in another spec. A shared name is fine *only* if the definition
is byte-identical — currently `PagedResult`, `ProblemDetails`, `ValidationProblemDetails`,
`AuditFields` and `BedCondition`. CI should run the uniqueness sweep so this cannot
silently regress.

*Also resolved:* `Doctor` is a Staff Management role — M4 only checks the JWT claim. Bed ownership split agreed (§6.1). No SMS integration; the group's Maps API covers the third-party requirement. `emergency-spec.yaml` is no longer the 212-byte stub — see §22–§26 for what it now publishes, including the dispatch notification, `patient_is_caller` and `caller_user_id` that §10 was waiting on.

**11.7 — `GET /auth/me` always answers `patient_id: null`, and now it should not.**
*(Raised by M4 on 2026-09-09, found while building `POST /patients/{id}/link-account`.)*

`CurrentPrincipal.PatientId` is documented as "the linked medical record, if staff have
linked one". `AuthService.ToPrincipal(PatientAccount)` hard-codes it to `null`, which was
correct while nothing could create the link. That endpoint now exists: a Duty Manager links
an account to a record and `Patient.UserAccountId` is set.

So today a patient signs in through the Flutter app and the API tells them they have no
medical record even when staff have linked one. Every `/me/*` screen in Patient Management
is scoped by that value.

The read is one line — `Patients.Where(p => p.UserAccountId == account.Id)` — but
`AuthService` is **common**, not M4's, so M4 has not written it. Whoever owns common picks
it up, or the group agrees M4 may. Until then the link is written and never read.

*(Update, 2026-09-12.)* **Still open, and no longer blocking.** M4 added
`GET /api/me/profile`, which answers the same question from inside Patient Management — 200
with the patient's own details, 404 (`cl_pat_033`) while the login has no record linked. The
Flutter app calls that on startup instead of reading `principal.patient_id`.

That is a work-around, not the fix. `CurrentPrincipal.PatientId` is still published, still
documented as the linked record, and still always `null`, so **anything that trusts it is
wrong today** — including any screen in Emergency or Staff that reaches for it. Either
common populates it or it comes off the schema; publishing a field that is always null is
the worst of the three options.

**11.8 (RESOLVED 2026-09-11) — an Equipment token can now read the patient register.**
*(Raised and closed by M4 on 2026-09-11, while adding `patient_code` — §6.4.)*

The problem: `GET /patients` was `PatientReader` — ward nurse, duty manager, hospital
administrator, doctor. An `EquipmentManager` typing a patient code into M3's assign screen
got a 403, so the code M4 had just published was unreachable from the one screen it was added
for.

**Fix, agreed with M3: `EquipmentManager` was added to `PatientReader`.** One line in
`Program.cs`; no new endpoint. M3 finds the patient, copies the eight characters, and builds
their own screen around them.

What that does and does not grant:

| | |
| :--- | :--- |
| `GET /patients`, `GET /patients/{id}` | **now allowed** for `EquipmentManager` |
| `POST /patients`, `PUT /patients/{id}` | still refused — `PatientRegistrar` / `PatientEditor` are untouched |
| ~~`GET /admissions`, `GET /patient-worklist`~~ | **now allowed too — changed 2026-09-11.** See below. |

**Amended 2026-09-11 — the last row of that table stopped being true, and the honest reading is
that it was never quite true.** `PatientReader` and `AdmissionReader` were collapsed into one
policy, `PatientDetails`, because the split was describing one level of access under two names:
`PatientDetail` inherits `Patient` and adds the patient's admissions, so **every**
`PatientReader` role — including this one — could already read care level, urgency and status
through `GET /patients/{id}`, whether or not it was on `AdmissionReader`. The door the table
promised was shut had a window next to it.

So rather than keep two names over one reality, there is now one policy and one sentence about
it. `PatientDetails` is **six of the seven staff roles**: `GeneralStaff`, `WardNurse`,
`DutyManager`, `HospitalAdministrator`, `Doctor`, `EquipmentManager`. `AmbulanceCrew` is the
one left out.

**What actually changed for M3**, as opposed to what was already possible: the admissions list
and the ward board are now reachable with an equipment token. If the group wants that tightened,
the seam is still one policy in `Program.cs`, and the narrower alternative is still the one from
before — a resolve-by-code endpoint answering a name and nothing else. Writing is unchanged and
remains `WardNurse` + `DutyManager` in every case.

**Worth being honest about the trade:** those two reads carry NIC, phone, address, date of
birth and, on the detail, the patient's admission history. An inventory role can now see all
of it. The narrower alternative was a resolve-by-code endpoint answering a name and nothing
else; the group took the simpler option knowingly. If that ever needs tightening, the seam is
one policy in `Program.cs` and one test,
`Equipment_management_can_look_a_patient_up_to_copy_their_code_but_cannot_change_anything`.

**M4 stops here.** How M3 uses the code — what their screen asks for, and what their assign
endpoint takes — is theirs to decide and theirs to build. M4 has written nothing on that side.

**11.9 (OPEN — raised by M4 on 2026-09-11, for Nasrulla Unais / M1) — the ambulance crew no longer
registers patients.**

**What changed.** `Policies.PatientRegistrar` is now `GeneralStaff`, `WardNurse`,
`DutyManager`. `AmbulanceCrew` came off it. The reasoning is that the crew are the emergency
response team and the paperwork is done at the hospital desk — and reception, who had no access
to this component at all, are who actually do it.

**Why this is M1's problem and not only M4's.** §4.2 of this file has the emergency flow
creating a patient record with a `temp_reference` for an unidentified casualty, and
`emergency-spec.yaml` is written around `DutyManager` and `AmbulanceCrew` — `GeneralStaff` does
not appear in it anywhere. After this change that record is created at the desk, not at the
scene.

**What is and is not affected today:**

| | |
| :--- | :--- |
| `POST /patients`, `POST /patients/lookup`, `POST /admissions` | `AmbulanceCrew` now gets 403 |
| `GET /patients`, `GET /admissions`, `GET /patient-worklist` | `AmbulanceCrew` was never on these and still is not — it is the one staff role on no Patient Management policy |
| `POST /admissions/pre-admit` | **not built.** Its `Roles:` line in `patient-spec.yaml` still says `AmbulanceCrew, DutyManager`, so the spec and `Policies.cs` currently disagree about that role. Nothing is broken today because there is no code behind it — but it has to be settled before there is. |

**M4 has not edited `emergency-spec.yaml`.** It is Nasrulla Unais's file. Three ways out, and the
choice is hers: put `GeneralStaff` into the emergency flow; keep `AmbulanceCrew` on
`pre-admit` alone as a documented exception, since a pre-admission is a dispatch record rather
than desk paperwork; or argue the crew should keep registration and M4 reverts.

---

**11.10 (OPEN — announced by M4 on 2026-09-11) — Patient Management has claimed billing.**

Not a question, and not a request. It is here because claiming an unowned area silently is
exactly what this section exists to prevent.

**What was built.** `Bill` and `BillLineItem`, migration `Patient_AddBilling`, six endpoints
under `/admissions/{id}/bill` and `/billing/outstanding`, and a React screen for reception.
Design is `patient-management-plan.md` §6.5; the contract is `patient-spec.yaml`.

**Why.** `billing_settled` has been a mandatory discharge checklist item since the first draft,
and §11 of the plan listed billing as out of scope in the same breath. A mandatory tick with
nothing behind it is a box somebody presses to make a screen go green, which is worse than not
having it.

**What it does not touch.** Nobody else's tables. It reads `Admission`, `BedAssignment` and
`Ward`, all of which are M4's, plus Equipment's bed register through the existing
`IBedRegistryService` port — the same read step 6 already does, no new dependency.

**What it deliberately cannot do, and why the other three should know.** A generated bill is an
admission fee and bed days, and nothing else, because **nothing in this project records a
treatment, a procedure or a drug against an admission.** Equipment's `PharmacyTransaction` has
a `performed_by_staff_id` and no admission id, so a dispensed medicine cannot be attributed to
a patient even in principle. Reception types clinical charges by hand.

**If M3 ever wants pharmacy on a bill**, the missing piece is on their side: an admission id (or
a `patient_code`) on `PharmacyTransaction`. That is their table and their call, and M4 has
written nothing on that side. Until then the by-hand line is the honest answer and the plan
says so out loud.

**Rates are a static C# table** (`Services/Patient/BillingRates.cs`), invented numbers, no
`billing_rates` table and no admin screen. If the group wants prices editable, that is one
file's worth of seam and somebody has to own the screen.

---


**11.11 (RESOLVED by M1 on 2026-09-13) — Emergency no longer mirrors `WardType`.**

The ward board went from ten wards split by sex to the eight the hospital actually has, and
three of them had no matching type: **`surgical`, `emergency`, `mental_health`** now sit
alongside `icu`, `hdu`, `general`, `maternity`, `pediatric` and `isolation`.

**Why they are types and not just names.** The bed-day rate is read off the ward type. Calling
a surgical ward a `general` one makes a surgical bed and an ordinary bed the same price, with
no way to tell them apart on a bill — and hard rules H2 and H4 would treat them as one ward
too.

**Nothing breaks.** The placement ladder puts all three on the same rung as `general`, which is
the catch-all arm of `BedPlacementRules.Rung` — so no existing rule changed, and an `inpatient`
is placeable in all three exactly as before.

Emergency routes every transport to the configured CareLanka Hospital emergency entrance.
Patient Management owns ward/bed preparation, so `WardTypeHint` and
`destination_ward_type_hint` were removed from `emergency-spec.yaml`. There is no duplicate
enum to drift when Patient adds a ward type.

**11.12 (DECIDED by M4 on 2026-09-11) — `Policies.PatientEditor` now includes general staff.**

It was ward nurse and duty manager. It is now **the same three roles as `PatientRegistrar`** —
general staff, ward nurse, duty manager.

**Why.** Reception types the patient record and could not correct it. Somebody who misspelt a
name, pressed Register and then noticed had no way back to it at all: the next lookup found the
record and offered to admit it, misspelling and all. That is not a safeguard, it is a typo that
has to be chased through a ward nurse.

**The rule, stated once:** whoever may create a record may correct it.

**Scope.** `PUT /patients/{id}` only. `AdmissionEditor` is untouched and is still ward nurse and
duty manager — chasing a visit's paperwork is a different job from fixing a name.

**11.13 (OPEN — announced by M4 on 2026-09-11) — prices are a table now, and the administrator
owns them.**

`BillingRates.cs` said out loud that a `billing_rates` table would be "a migration, a role, a
screen and a set of tests for a number that is edited once a year". That held while the numbers
were invented constants nobody could change. It stopped holding when the hospital administrator
was given the job of setting them.

**What was built.** `BillingRate` (ward type × expense) and `AdmissionFeeRate` (care level),
migration `Patient_AddBillingRates`, `GET`/`PUT /billing/rates`, and a React screen at
`/billing-settings`. Both tables are M4's and nobody else writes them.

**Two properties worth knowing, because they are what make it safe.** A cell with no row falls
back to the built-in default, so the grid is complete on a database that has never seen the
screen. And the price is still **copied onto the bill line when the line is written** — editing
a rate prices tomorrow's bills and never rewrites one a patient has already been handed.

**Read is `AnyStaff`, write is `HospitalAdministrator`.** Everyone at the desk needs the
suggested price in the box in front of them; deciding what that price is is a different job.

**11.14 (DECIDED by M4 on 2026-09-12) — a new `Policies.BedAssigner`, and the duty manager may
now place a patient in a ward more acute than assessed.**

Three changes, all inside Patient Management, but the first is a new entry in the group-owned
`Policies.cs` and the second changes a published hard rule, so both are recorded here.

**1. `Policies.BedAssigner` — general staff, ward nurse, duty manager.** New policy, now on
`POST /admissions/{id}/assign-bed` and `POST /admissions/{id}/correct-bed`, which previously
used `AdmissionEditor`. Reception registers, admits and beds a walk-in standing at the desk;
stopping one step short handed the final act to a ward nurse who is not there.

**Deliberately not just widening `AdmissionEditor`,** which also guards cancelling a visit and
confirming a discharge. Reception bedding a walk-in does not imply reception sending somebody
home. `AdmissionEditor` is untouched and is still ward nurse and duty manager.

**Widening the route did not widen which bed anyone may choose.** That reads the ward the
chosen bed stands in, which is in the request body and cannot be a route policy, so
`BedAssignmentService.EnsureMayApprove` still answers 403 (`cl_pat_012` / `cl_pat_013`) for
anything off the care level's own path — see change 2. Verified live: doctor, administrator,
equipment manager and ambulance crew are all still refused the route outright.

**2. The role split is now the care-level match, and nothing else.** `NeedsDutyManager` no
longer names `icu` or `hdu` at all — it is "does this ward give the care this patient was
assessed as needing, yes or no".

- **A matching ward is anybody's** who may place a patient, **intensive care included.** An ICU
  bed for an ICU patient is the right bed; making a nurse find a duty manager for it delayed the
  most urgent admission in the hospital for a decision nobody had to make. The scarcity argument
  is about not giving an ICU bed to somebody who does *not* need one — which is the step up, and
  that is still gated.
- **A mismatched ward is the duty manager's alone**, in both directions: a downgrade
  (`cl_pat_013`) or a ward more acute than assessed (`cl_pat_012`). Hard rule H2 upward used to
  be a flat 409 for everybody; the duty manager may now overrule it, for the night when the
  general ward is full and there is an empty ICU bed. The React bed picker colours these buttons
  **amber** so an off-path bed is never taken by accident.

**For the viva, since §5.2 used to call the ICU rule a high-impact approval gate.** The **AI
gate is untouched** — every agent proposal is still an `AgentProposedChange` a human approves,
and no agent places anybody. What changed is only which human approves a routine, correctly
matched placement. The surviving role gate is the off-path bed, which is where the judgement
call actually is.

**2b. Discharge went the same way.** New `Policies.DischargeConfirmer` — general staff, ward
nurse, duty manager — on `POST /discharges/{admissionId}/confirm`, replacing `AdmissionEditor`
there, and the ICU/HDU narrowing inside `DischargeService` is **deleted**. Any of those three
now confirms a discharge at any care level.

**`cl_pat_024` is retired. Do not reuse the number** — a client still branching on it would
silently match whatever took its place. It is the only code this project has ever withdrawn.

**Why this is not a loosening of patient safety.** The gate was never the role; it is the
checklist. `ConfirmAsync` refuses with `cl_pat_023` unless both mandatory items are ticked, and
`clinical_clearance` is **a doctor's alone** — no other role and no automated process can set
it. So no patient goes home without a doctor having cleared them, whoever presses confirm. The
duty manager's signature came *after* the doctor's and added a second wait, not a second
judgement. Reception is on the list because it settles the bill and hands over the discharge
document on that same screen.

**3. New hard rule H6, and a new code `cl_pat_030`.** A `pediatric` ward admits only patients
under 18. One-directional — a child is not confined to one, or a child needing intensive care
could not be given it. **A patient with no recorded date of birth is refused**, on the same
reasoning as H3's handling of `unknown`. Unlike H2 there is no duty-manager override: like the
gender policy it is a property of the ward, not a judgement call.

**Consequence for everyone else: a date of birth now matters clinically.** The React intake
form requires one for any patient who can give one. **M1 in particular** — an unidentified
casualty record with no date of birth cannot be placed in the children's ward, which is correct
but worth knowing before it surprises somebody at the demo.

**One unrelated bug fixed in passing.** `GET /bed-availability` capped `pageSize` at 100 like
every other paged route. The seeded hospital has 135 beds, so the picker silently cut off
mid-alphabet and pediatric and surgical beds could never be chosen at all. That one endpoint now
allows up to 500 — it feeds a complete candidate list, not a page anybody browses — and the
screen says so if the list is ever incomplete again.

**11.15 (OPEN — announced by M3 on 2026-09-13) — Equipment Management has claimed laboratory
reports.**

Not a question, and not a request. It is here for the same reason §11.10 is: claiming an unowned
area silently is exactly what this section exists to prevent.

**What was built.** `LabReport`, migration `Equipment_AddLabReports`, three endpoints under
`/lab-reports`, and a React screen. Design is `equipment-management-plan.md` §6.6; the contract
is `equipment-spec.yaml`.

**Why here rather than in Patient Management.** A result is produced by a hospital service unit,
which is what this component is about, and the row stores nothing clinical about the person: a
`patient_id` and a file, exactly the read-only reference `EquipmentItem.assigned_to_admission_id`
already is. Nothing in Patient Management changes, and nothing here copies a name, a NIC or a
ward.

**Why it exists at all.** A ward currently waits for paper to be carried up from the basement. A
result that is filed the moment the lab issues it is readable at the nursing station straight
away, and the specimen's own journey does not change.

**Two things other people should know.**

**1. `Policies.LabReportReader`, `Policies.LabReportAuthor` and
`Policies.PatientLocationReader` are new entries in the group-owned `Policies.cs`.** Reader is
doctor, ward nurse, duty manager and the laboratory. Author is the laboratory alone.
`PatientLocationReader` gates `GET /ward-patients` and holds the same four roles as Reader today,
under a separate name because knowing which ward somebody is in is not reading a test result. The administrator is on neither, for the same reason they are
not on `AdmissionReader`: reading one patient's blood result is clinical work.

**2. The laboratory rides on `equipment_manager`, and it should not forever.** `StaffRole` has
no laboratory value, and adding one is a change to `staff-spec.yaml` and `common-spec.yaml`
together — M2's and the common owner's call, the same conversation as Open Decision 11. Until
then those two policies are where a `laboratory` role would be added, and nothing else changes.

**3. Two screens browse by ward, and they read it through M4's service rather than their tables.**
`GET /ward-patients` lists who is in the hospital now, optionally filtered to one ward. The
laboratory uses it to file a result against the right person, and the equipment register uses it
to assign an item to a bedside - **that screen used to take a pasted admission id**. The row
carries both ids, because an assignment points at the visit and a result points at the person.
Searching by code, name or NIC is still there as the lab's second way in, for an outpatient who is
in no ward at all.

**Nothing of M4's changed for it, and nothing new is disclosed.** It calls
`IAdmissionService.ListAsync`, the same shape as the bed-occupancy adapter. Every
field it publishes — name, code, ward, bed, visit status — is already visible to these roles
through `GET /patients/{id}`, which carries a patient's admissions with the ward on them. This
saves opening one patient at a time; it does not widen who can see what. **M4's admissions list
endpoint and its read policy are untouched.**

**One thing M3 would still like, and is not blocked on.** The filter matches on **ward name**,
because `AdmissionSummary` publishes `ward_name` and not a ward id, and the rows are filtered in
Equipment after the read. That is honest at a few hundred beds and wrong at ten thousand. When M4
publishes the `wardId` filter on `GET /admissions` that `STUBS.md` already calls unblocked,
`WardPatientService` collapses to one delegating call. Recorded in `STUBS.md`.

---

**11.16 (OPEN — announced by M4 on 2026-09-16) — a patient can now attach their app login to a
record the desk created, using the patient code.**

Here because it sits next to common auth without being part of it, and everybody should be able
to see where the line was drawn.

**The problem.** `POST /me/pre-register` links a login to an existing record by matching on NIC,
and `CreatePatientRequest.Nic` is optional — a walk-in or an emergency arrival is often
registered without one, which is what `temp_reference` is for. That patient installs the app
afterwards, fills in the form, and gets a **second, empty record**, while their real stay sits on
the record staff created. `POST /patients/{id}/link-account` exists for this but takes a raw
account GUID and no screen calls it, so in practice the gap was open.

**What was built.** `POST /me/claim/preview` and `POST /me/claim`, both Patient-only, both taking
`patient_code` + `nic`. Preview answers a **masked** summary; claim links through the
same `IPatientService.LinkAccountAsync` the desk override already uses. Design is
`patient-management-plan.md` §7.6b.

**What it does not touch — and this is the part for the group.** **Nothing in common auth
changed.** No new auth endpoint, no change to `PatientAccount`, no change to registration or the
JWT. The account is created first through the existing `POST /auth/patient/register`, and the
claim is an authenticated call from that login. An anonymous "enter a code and set a password"
flow would have been the other design, and it was rejected twice over: it would have put M4's
hands in common auth, and it would have handed a stranger holding a dropped hospital slip a
patient's name, NIC, address and emergency contact before asking anybody to prove anything.

**The one thing another member might care about.** `Patient.Nic` is now the second factor on the
claim, not just the pre-register match key. A record with no NIC on file cannot be claimed from
the app at all and has to go through the Duty Manager link endpoint — the same walk-in-without-NIC
gap described above, so a record created with `temp_reference` and no NIC still needs the desk
override. Nobody outside M4 writes that column today, so this is a note, not a request.

**Still open on M4's side:** attempt rate-limiting on the claim endpoints. Authenticated, so
every attempt is attributable to an account, but nothing stops a login trying repeatedly.

---

**11.17 (OPEN — announced by M4 on 2026-09-16) — both Patient Management agents were redesigned,
and one of the three changes affects everybody.**

Full reasoning is `patient-management-plan.md` §8, rewritten the same day. Three changes; the
first is the only one anybody else needs to read.

**1. The bed agent holds no write tool at all, and this is worth copying.** It used to place a
30-minute hold on its chosen bed before a human saw anything, on the reasoning that a hold is
not an admission so it is low-impact. That is wrong in a way only visible at scale: a hold takes
a real bed out of circulation, so **an agent run nobody acts on quietly makes a ward look full
to every other component** — to M1's dispatch agent choosing a destination, to M2's staffing
agent reading occupancy, and to the Coordinator assembling a plan. All four of its tools are now
read-only. Committing is a human pressing a button on `POST /admissions/{id}/assign-bed`, the
manual endpoint that has existed since 2026-09-11, with its row lock and its hard rules.

*The question for the other three: if your agent's "proposal" reserves, locks or allocates
something real, what does a run nobody approves cost the rest of the hospital?* Not a request to
change anything — an argument that landed here and might land there.

**Two contract consequences.** `POST /bed-assignments/{id}/approve` and `/reject` are
**withdrawn from `patient-spec.yaml`** — neither was ever built, and nothing to approve exists
any more. `POST /admissions/{id}/bed-suggestion` became `POST /bed-suggestions`, taking either
an `admission_id` or an NIC / patient code. `AdmissionStatus.AwaitingApproval` is consequently
**never set** — it stays in the shared enum because Equipment's ward-patient list reads it, and
removing a value from a shared enum is a cross-component change for no gain. **M3: nothing
breaks, but no admission will ever appear in that state again.**

**2. A new table, `PatientMedicalProfile`.** One row per patient, four free-text fields a nurse
types: conditions, allergies, current symptoms, recent situation. It exists because the care
advisory agent was reading demographics and the administrative shape of past visits, which is
nothing to reason over. **Not published cross-component, not patient-readable, and not an EHR** —
no vitals, no lab results, no coded diagnosis. Announced rather than asked, on the same footing
as §11.10 and §11.13: an unowned thing inside one component's own boundary, claimed in the open
so nobody builds a second one. **M3, one note:** this is *not* where laboratory results go.
`LabReport` is yours (§11.15) and stays yours; this is four sentences a clinician typed.

**3. Care recommendation review widened from `Doctor` to `Doctor` or `WardNurse`.** The agent now
only runs for admitted patients, and the person who will actually walk over and look at one is
the nurse on shift. `clinical_clearance` on the discharge checklist did **not** move and is still
Doctor-only. **M2, this is a note not a request** — `Doctor` and `WardNurse` are both already in
`StaffRole` and M4 only reads the claim.

**11.18 (OPEN — announced by M3 on 2026-09-16) — a new equipment item waits for the hospital
administrator to confirm it.**

Not a request. Announced because it gives `hospital_administrator` — a role every component
shares — a new job inside Equipment Management, and a first mobile screen.

**What changed.** `POST /equipment-items` now saves the item with `awaiting_confirmation = true`.
It stays off `GET /equipment-items`, and cannot be edited, assigned, faulted or serviced, until the hospital
administrator confirms it in the Flutter app (`POST /equipment-items/{id}/confirm`) or rejects it
(`/reject`). Migration `Equipment_AddItemConfirmation` adds three columns to `equipment_items` and
marks every existing row as already confirmed. Design is `equipment-management-plan.md` §4.3; the
contract is `equipment-spec.yaml`.

**The confirmation code.** Listing, confirming and rejecting also need an `X-Confirmation-Code`
header, checked by the API against `Equipment:ConfirmationCode` in `appsettings.json`. The demo
value is in `TEST_ACCOUNTS.md`. Change it anywhere real.

**What it means for the others.**
- **M4:** nothing you read changes. Equipment still never writes a `Ward` or an `Admission`.
- **M2:** no new role. `HospitalAdministrator` already exists in `StaffRole`; Equipment only reads
  the claim, through the new `EquipmentConfirmer` policy.
- **Anyone listing equipment** (a readiness check, a report): an unconfirmed item is not in
  `GET /equipment-items`, which is the point — it is not usable stock yet.

---

## 12. For the other three members

This file originally described every boundary **from the Patient Management side**, because that was the first component designed. Equipment Management (§13–§16) added its own sections, written against `equipment-management-plan.md` and `equipment-spec.yaml`. Staff Management (§17–§21) and Emergency (§22–§26) now have theirs too, written against `staff-spec.yaml` and `emergency-management-plan.md`/`emergency-spec.yaml` respectively. If something here is wrong about your component, raise it in §11 rather than working around it.

Two questions worth asking about anything you are unsure of:

1. **Who writes this?** Whoever's business rules cause the value to change owns the table. Everyone else reads through their service.
2. **Would this make my component depend on someone else's code being finished?** If yes, you cannot demo alone and you cannot test alone. Push the dependency to a read-only call and keep a working fallback.

---

## 13. Equipment Management ↔ Patient Management (Member 4) — confirmed from Equipment's side

§6 above already documents this boundary from Patient Management's side. Equipment's own design (`equipment-management-plan.md` §3.3, §13.1–§13.2) agrees on every point, written here for the record so a reader doesn't have to cross-check two documents to be sure they match:

- **The bed split holds.** Equipment owns `Bed` — frame, condition, repairs, adding/retiring beds. Patient owns `BedAssignment` — who is in it, holds, approvals. Neither writes the other's table.
- **Equipment asks before touching a bed, every time, no exceptions.** Before `PATCH /api/beds/{id}` or `POST /api/beds/{id}/retire`, and before scheduling maintenance against a bed (`asset_type = bed`), Equipment calls Patient's `GetBedOccupancyAsync` **inside the same request, before committing anything**. Occupied or held → `409 Conflict`, nothing written. This is the one place Equipment's correctness depends on another component's live answer rather than its own row lock, because "is anyone in this bed" is not Equipment's data to lock (equipment-management-plan.md §3.3).
- **Equipment reads the ward list, never duplicates it.** `ward_id` on every `EquipmentItem` and `Bed` is a read-only reference into Patient's `Ward` table (`GET /api/wards`). Equipment stores no copy of `ward_type` or `gender_policy` — it has no use for either.
- **`EquipmentItem.assigned_to_admission_id` is ID-only.** Equipment never writes into `Admission`; it stores the ID and reads an admission summary from Patient's service at display time to show "assigned to: [patient], Ward 5B" without copying patient data.

No open disagreement between the two write-ups. If one changes, check the other.

---

## 14. Equipment Management ↔ Staff Management (Member 2)

### 14.1 We store staff IDs. We never store staff data.

Same rule Patient Management states in §5.1, applied to Equipment's tables. Each of these is a foreign key into `StaffMember`, ID only:

| Our field | What it records |
| :--- | :--- |
| `PharmacyTransaction.performed_by_staff_id` | Who recorded a stock movement — received, dispensed, adjusted, expired-removed |
| `MaintenanceSchedule.performed_by_staff_id` | Who carried out a service, calibration or repair |
| `ActionRequest.approved_by_staff_id` | Who approved (or rejected) a proposed action |
| `Warning.acknowledged_by_staff_id` | Who acknowledged an open warning |

Name, role and department stay in `StaffMember`, owned by M2. To show "Approved by …" or "Serviced by …" in React or Flutter, we ask M2's service for the name at read time rather than copying it in — the same staleness argument Patient gives in §5.1.

### 14.2 No Equipment action is gated on a specific clinical role

Unlike Patient Management's `clinical_clearance` checklist item (§5.2), nothing in Equipment Management requires a `Doctor` claim. Write actions gate on **Inventory Administrator** (add stock, approve/reject an `ActionRequest`, manage the bed register) or **Equipment Technician** (complete a service, report a fault, assign/release equipment); search and availability are open to any authenticated staff role (equipment-management-plan.md §2).

---

## 15. Contracts Equipment Management provides

Mirrors §9's format, from the Equipment side. All JWT-protected; role restrictions per `equipment-management-plan.md` §7.

| Endpoint | For | Returns |
| :--- | :--- | :--- |
| `GET /api/beds` | Patient Management's bed agent | The full bed register — id, ward, number, condition, isolation, distance. This is what §10 calls Patient's "hardest dependency." |
| `GET /api/wards/{wardId}/equipment-readiness` | The group orchestrator, as a step in the shared admission workflow | `ready` / `not_ready` for a ward against a list of required equipment categories (§8.7 "readiness check") |
| `GET /api/equipment-items?wardId=` | Any staff, including other components' agents | Equipment currently in a given ward |
| `GET /api/pharmacy-items?search=&availableOnly=` | Any authenticated staff | Stock search and availability — the literal "search and check availability" requirement (equipment-management-plan.md §5.2) |
| A readable condition change on `Bed` (or a notification) when a bed goes in or out of service | Patient Management | So Patient's bed agent's candidate pool stays current — the item Patient asks for in §10 |

## 16. What Equipment Management needs from others

Mirrors §10's format, from the Equipment side.

| From | What | Why |
| :--- | :--- | :--- |
| **M4 (Patient)** | `GetBedOccupancyAsync(bedId)` | Blocks any bed-servicing or bed-retirement action against an occupied or held bed — §13, equipment-management-plan.md §3.3 |
| **M4 (Patient)** | Ward list — id, name, type | `ward_id` on every `EquipmentItem` and `Bed` |
| **M4 (Patient)** | Admission summary by ID | Displaying who an assigned item belongs to, without copying patient data |
| **M2 (Staff)** | Staff member's name and role by ID | Displaying "Approved by …" / "Serviced by …" without copying their data |
| **Group** | Shared agent-workflow tables | Same open item as §11.2 — `ActionRequest.workflow_id` and `Warning.workflow_id` point into whatever the group agrees |

---

## 17. Staff Management ↔ Patient Management (Member 4) — confirmed from Staff's side

Written from `staff-spec.yaml`, which Nasrullah (Member 2) committed but had not yet cross-referenced into this file (§0's rule 6 asks each member to add their own section; this one is written on his behalf from what his spec already publishes, not invented — nothing here goes beyond what `staff-spec.yaml`'s `info.description` and schemas already say).

- **The boundary, in Staff's own words:** *"Staff Management owns the staff record and the roster. It does not own patients, beds or wards. Ward occupancy is read from Patient Management to work out staffing demand (`integration_of_functions.md` §5.3); ward identity is theirs too, pending §11.1."* This matches §5 and §11.1 exactly — no disagreement to raise.
- **`Ward` stays Patient's.** `Shift.WardId` and `WardStaffingRule.WardId` are read-only references into Patient's `Ward` table. `GET /wards/{wardId}/staffing-rules` in `staff-spec.yaml` says it plainly: *"Ward identity belongs to Patient Management; the staffing rule is ours."*
- **Staff reads Patient's ward occupancy for two things**, both already documented on Patient's side (§5.3): the roster grid's understanding of demand, and the Staff Allocation Agent's tool list — `staff-spec.yaml`'s `ToolCall.tool_name` enum includes `get_ward_occupancy` alongside its own `get_ward_coverage`, confirming the agent actually calls out to Patient rather than only reading its own tables.
- **Staff stores no patient data at all.** Nothing in `staff-spec.yaml`'s schemas references `Patient`, `Admission` or any patient-identifying field — consistent with §5's "none of that makes patient data theirs."

## 18. Staff Management ↔ Equipment Management (Member 3)

- **§14 already documents this from Equipment's side** and Staff's spec agrees on every point found: `PharmacyTransaction.performed_by_staff_id`, `MaintenanceSchedule.performed_by_staff_id`, `ActionRequest.approved_by_staff_id` and `Warning.acknowledged_by_staff_id` are all ID-only FKs into `StaffMember`, resolved through Staff's lookup endpoint (§18.1) rather than copied.
- **No Equipment action is gated on a Staff-defined role beyond the shared JWT.** `staff-spec.yaml`'s `bearerAuth` description lists `EquipmentManager` as one of the roles it issues claims for, but the gating logic itself (Inventory Administrator vs Equipment Technician, §14.2) is Equipment's to enforce — Staff only issues the token and the role claim.
- **Open Decision 11 (`docs/entity_diagram.md`) is unresolved from Staff's side too.** `staff-spec.yaml`'s `StaffRole` enum still carries a single `equipment_manager`, not the two capabilities (`Inventory Administrator` / `Equipment Technician`) that `equipment-management-plan.md` §2 is written against. Staff has not proposed a fix; this stays open for Members 2 and 3 to settle, as already recorded in `docs/entity_diagram.md`.

## 18.1 Staff's lookup contract, used by both

`POST /staff/lookup` is the one endpoint both §5.1 (Patient) and §14.1 (Equipment) point at, so it is worth stating once, here, rather than twice: batch resolve of staff ids to `{ staff_id, found, full_name, role, is_active }`, name/role/active-flag only, "any authenticated staff member" may call it. Unknown or deactivated ids come back `found: false` rather than 404ing the whole batch, so a caller rendering twenty rows never gets a hard failure over one stale id.

## 19. Staff Management ↔ Emergency (Member 1)

`staff-spec.yaml` does not mention Emergency directly — no shared schema, no cross-reference in its `info.description`. The boundary here follows purely from the ownership map (§3) and the general-purpose contracts Staff already publishes to "any authenticated staff member," so nothing below is new plumbing Staff would need to build:

- **`AmbulanceCrewAssignment` and `DispatchCrew` (Emergency's own tables, §22–§26)
  store `StaffMemberId`.** The first records current ambulance responsibility; the second
  permanently snapshots responders when a dispatch is created. Staff owns the person and
  Emergency resolves names/roles through `POST /staff/lookup`.
- **`ambulance_crew` and `general_staff` are already staff roles** in `staff-spec.yaml`'s `StaffRole` enum, so Emergency's Flutter screens authorize against the same JWT claim every other component reads — no separate role system for crew.
- **Staff's roster does not model ambulance duty.** `Shift`/`Allocation` are ward-based.
  Emergency owns current vehicle assignment and run history; one crew member may have at
  most one current ambulance assignment.

## 20. Contracts Staff Management provides

Mirrors §9's and §15's format, from Staff's side. All JWT-protected; role restrictions per `staff-spec.yaml`.

| Endpoint | For | Returns |
| :--- | :--- | :--- |
| `POST /staff/lookup` | Patient, Equipment, Emergency — any component displaying "who did this" without copying staff data | Batch of `{ staff_id, found, full_name, role, is_active }`, in request order |
| `GET /coverage/wards` | Patient's bed agent as an optional soft ranking rule (§5.4, not in the first build); the Equipment agent when deciding where to move equipment | Per-ward `{ on_duty_count, minimum_headcount, headcount_needed, status, by_role }` — counts only, no staff identities |
| `GET /wards/{wardId}/staffing-rules` | Any component that needs to know a ward's staffing policy | The minimum-headcount rules for that ward, by role and skill |

## 21. What Staff Management needs from others

Mirrors §10's and §16's format, from Staff's side — read from `staff-spec.yaml`'s own stated dependencies, not invented.

| From | What | Why |
| :--- | :--- | :--- |
| **M4 (Patient)** | Ward occupancy / ward capacity data | The roster agent's `get_ward_occupancy` tool and the coverage report both read Patient's ward numbers to judge staffing demand — §5.3 |
| **M4 (Patient)** | Ward id and name | `Shift.WardId` and `WardStaffingRule.WardId` are read-only references; Staff keeps no copy of ward type or gender policy, having no use for either |
| **Group** | Shared agent-workflow tables | Same open item as §11.2 and §16 — `staff-spec.yaml` already links its own `RosterProposalDetail.workflow_id` into whatever the group agrees, rather than inventing its own workflow schema |
| **Group** | A decision on `Allocation.clocked_in_at` / `clocked_out_at` | `staff-spec.yaml` publishes `POST /me/allocations/{id}/clock-in` and `/clock-out`, and its own `info.description` and `Allocation` schema flag that these two fields do not yet exist on `docs/entity_diagram.md`'s `Allocation` entity (Rev 2). This is a genuine schema gap, not a boundary disagreement — raised here rather than silently assumed, since `entity_diagram.md` is group-owned and not Staff's alone to add fields to |

---

## 22. Emergency ↔ Patient Management (Member 4) — confirmed from Emergency's side

§4 above documents the same boundary from Patient Management's side:

- **The call screen split holds.** Patient Management builds the patient-role Flutter
  report, tracking and cancellation screens. Emergency owns the `/emergency-calls` and
  `/me/emergency-calls` operations, `EmergencyCall`, and everything downstream.
- **The patient may report for self or another person.** `patient_is_caller` is required;
  `caller_user_id` comes from the JWT and is never accepted from the request. Both flow into
  the pre-admission notification. The caller sees only their own narrow tracking data.
- **`urgency` is translated by Emergency before the call, not by Patient afterwards.** The two components rank different things — `CallPriority` is how fast an *ambulance* is needed, `AdmissionUrgency` is how fast a *bed* is — so `DispatchNotification.urgency` carries Patient's vocabulary, not Emergency's. The table is fixed in C# on Emergency's side and is not something the agent decides: `critical` → `emergency`, `high` → `urgent`, `medium` and `low` → `routine`. Lossy on purpose, one-directional, and written in **four** places that must agree — here, `emergency-spec.yaml`'s `DispatchNotification`, `emergency-management-plan.md` §6, and `patient-spec.yaml`'s `PreAdmitRequest`. `POST /admissions/pre-admit` rejects anything outside Patient's three values with a 400, so a drift here fails loudly rather than filing an emergency as routine.
- **`caller_user_id` is a `PatientAccount.Id`, never a `Patient.Id`.** `docs/entity_diagram.md` Rev 2.5 adds the `PatientAccount` table and repoints `EmergencyCall.CallerUserId` at it. This is Patient Management's omission, not Emergency's — the patient login was resolved in Rev 2.3 and the table was never written down, so Rev 2.4 reasonably guessed `Patient.Id`. The bystander case is why it matters: `caller_user_id` is the helper's **login**, `patient_id` is the casualty's **medical record**, and one FK to one table would mean fabricating a record for the healthy person every time. `Patient.user_account_id` is the single optional link between them.
- **A call can also arrive by phone, and that needs no new contract.** Patient Management's emergency screen carries a `tel:` link to the hospital number beside the button (`patient-management-plan.md` §10.1); a staff member takes the details and posts the same `POST /emergency-calls`, which `emergency-spec.yaml` already documents as *"also used by staff logging a call at the front desk for someone with no phone or app"*. `caller_user_id` is null on that path — which is exactly what it is nullable for. No endpoint, table or field is added by either component, and no telephony system is being built.
- **Location ownership follows the screen/API seam.** M4 captures and lets the caller
  correct the GPS point in its screen. M1 validates/stores it and calculates route/ETA on
  the backend. Crew Flutter launches Google Maps for driving.
- **There is no Emergency ward choice.** This release has one configured CareLanka
  Hospital emergency entrance. Patient Management owns ward and bed preparation.
- **Open item 11.3 is now practically resolved** (see the updated §11.3 above): the agent's plan calls Patient's `POST /admissions/pre-admit` directly once a dispatch is created, matching what `patient-spec.yaml` already documented as the expected caller.
- **Patient Management failure is non-blocking.** Pre-admission happens after dispatch
  commits and is retried without stopping manual response or handover.

## 23. Emergency ↔ Staff Management (Member 2)

- **`AmbulanceCrewAssignment` stores current duty and `DispatchCrew` stores immutable
  response history.** Both hold StaffMember IDs only. Emergency resolves names and verifies
  the `ambulance_crew` role through Staff's `POST /staff/lookup`.
- **`ambulance_crew` is a `StaffRole` Staff already issues.** Emergency's Flutter crew screens (§4.1 of `docs/CareLanka_Component_Plan.md`) authorize against that JWT claim; Emergency defines no role of its own.
- **Ambulance duty stays outside the ward roster.** One crew member has at most one current
  ambulance assignment. Dispatch creation copies that ambulance's current crew into
  `DispatchCrew`; later assignment changes do not rewrite the run.

## 24. Emergency ↔ Equipment Management (Member 3)

There is close to no boundary here, and that is a design decision recorded in `docs/entity_diagram.md`, not an oversight:

- **Ambulances carry no tracked equipment.** `docs/entity_diagram.md`'s `Ambulance` note is explicit: *"Onboard equipment is explicitly not tracked (equipment stays ward-scoped only)"* (Decisions 9, 14). Equipment's `EquipmentItem.WardId` is non-nullable — every tracked item belongs to a ward, never to a vehicle — so there is no shared table, no read, no write between the two components.
- **Emergency never calls Equipment's API.** Ward and bed preparation are Patient
  Management's responsibility and do not affect the dispatch transaction.

### 24.1 The Dispatch & Routing Agent's plan, for context on §22–§23

`emergency-spec.yaml`'s Dispatch & Routing Agent (`AgentType.DispatchRouting` in `docs/entity_diagram.md`) plans in this order:

1. Read the incoming `EmergencyCall` — location and priority. **The dispatcher sets the priority, not the agent**; triage is clinical judgement, the same wall §7 draws around `admission_category`.
2. Deterministic code filters eligible ambulances: active, serviceable, configured minimum
   current crew, no live dispatch, and a usable recent location.
3. Google supplies backend driving ETA when available. The agent ranks and explains only;
   maps failure falls back to straight-line ordering.
4. **Routine case:** an eligible ambulance exists → `pending_confirmation`; a Duty Manager
   confirms with one tap. Manual dispatch uses the same command and remains available.
5. **Diversion case:** nothing is free, but a lower-priority response is still in `assigned`,
   `acknowledged`, or `en_route_to_scene` → `pending_approval` with `DiversionImpact`.
6. **Neither:** outcome `no_ambulance_available`, recorded honestly.
7. Once dispatch commits, call Patient Management's `POST /admissions/pre-admit`. Failure
   is retryable and never rolls back or delays the ambulance.

**Two things here matter to the other three members, because they change what the group can claim:**

- **Nothing this agent proposes reaches the road without a Duty Manager.** AI recommends
  and explains; deterministic code validates; the human confirms.
- **A dispatch at `at_scene` or later is never diverted.** `DispatchStatus` is
  authoritative, so every caller reads the same boundary without consulting a second enum.

### 24.2 Note

This section exists so a reader of `integration_of_functions.md` does not have to separately open `emergency-management-plan.md` to see why §22 and §23 read the way they do; the full design, including both approval gates, the divertibility rule and the React/Flutter screen split, is in `emergency-management-plan.md`.

## 25. Contracts Emergency Service provides

Mirrors §9's, §15's and §20's format, from Emergency's side. All JWT-protected; role restrictions per `emergency-management-plan.md` §7.

| Endpoint | For | Returns |
| :--- | :--- | :--- |
| `POST /emergency-calls` | Patient Management's patient-facing report screen (§4.1, §22) | Created call; intake does not wait for AI, Maps, push, or Patient Management |
| `POST /emergency-calls/{id}/dispatch` | Duty Manager manual path and the service reused by proposal confirmation | Assigned dispatch with immutable responding-crew snapshot |
| `GET/POST /ambulances/{id}/crew`; `DELETE /ambulances/{ambulanceId}/crew/{staffMemberId}` | Duty Manager fleet readiness | Current ambulance crew; one current ambulance per crew member |
| `POST /me/dispatches/{id}/acknowledge`; `POST /me/dispatches/{id}/decline` | Responding crew | One crew member's decision for the response unit |
| `GET /dispatches/{id}` | Patient Management / a future orchestrator | Authoritative dispatch status, ambulance, responding crew, route and ETA |
| `GET /me/emergency-calls/{id}/tracking` | The patient's own Flutter screen | Ambulance position and ETA for a call **they** raised. Deliberately narrow — no crew names, no notes, no other calls. Emergency's own endpoint, not a filtered staff response |
| `POST /me/emergency-calls/{id}/cancel` | Patient Management's patient screen | Direct cancellation only before dispatch |
| `POST /me/emergency-calls/{id}/cancellation-request` and Duty Manager review operations | Patient screen / Duty Manager queue | A request that does not move or recall an ambulance until reviewed |

## 26. What Emergency needs from others

Mirrors §10's, §16's and §21's format, from Emergency's side.

| From | What | Why |
| :--- | :--- | :--- |
| **M4 (Patient)** | `POST /admissions/pre-admit` | Starts patient/ward/bed preparation after dispatch; failure is non-blocking and retryable |
| **M4 (Patient)** | Patient-facing emergency report, tracking and cancellation screens | M4 owns the patient experience; M1 publishes the generated Emergency contract |
| **M2 (Staff)** | `POST /staff/lookup` | Verify `ambulance_crew` and resolve current/responding crew without copying staff data |
| **Group** | Shared agent-workflow tables | The Dispatch & Routing Agent links to the common workflow contract |
