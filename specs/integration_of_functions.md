# How the Four Components Connect

**CareLanka Hospital Management System · SE3090 Assignment 1**
**Status:** draft · **Written from the Patient Management side (Member 4)** · other members should extend their own sections

This file explains where one member's component ends and another's begins, and exactly what crosses the line. Read it before designing anything that touches patients, wards or beds.

It is written to be read by people **and** by AI coding assistants. If your agent is helping you design your component, point it at this file first — it will stop you from building something a teammate already owns.

---

## 0. The most important thing to understand first

**We are building ONE ASP.NET Core application on ONE PostgreSQL database.** The assignment requires it:

> "React and Flutter must use the same ASP.NET Core Web API, PostgreSQL database, user identity, permissions and business rules. Disconnected prototypes will not satisfy the assignment."

So "integration" here does **not** mean four separate services calling each other over HTTP. There is one API project, one database, four owners. All four components are folders inside the same solution:

```
api/
  Controllers/  Emergency/  Staff/  Equipment/  Patient/
  Services/     Emergency/  Staff/  Equipment/  Patient/
  DTOs/         Emergency/  Staff/  Equipment/  Patient/
  Data/         one DbContext, all entities
```

That makes the ownership rules a **team convention**, not something the compiler enforces. Which is why they need to be written down.

The Agentic AI subsystem is the one part that may live outside the API (a Python service, for example). If it does, it is called *by* ASP.NET Core and never directly by React or Flutter — §2 of the assignment is explicit about that.

---

## 1. The one rule

> **One writer per table. Everyone else reads through the owner's service interface.**

Two halves, both matter.

**One writer.** Only Patient Management's code writes to `Bed`. Only Staff Management's code writes to `StaffMember`. If your feature needs to change a table you don't own, you call the owner's service and let their business rules run — you do not write a `DbContext` query against their tables.

**Read through the interface, not the tables.** When Emergency needs bed counts, it injects `IPatientCapacityService` and calls a method. It does not write `_db.Beds.Where(...)` in an Emergency controller.

Why this matters more than it looks:

- The owner can change their schema without breaking three other people's code
- Business rules live in one place — an expired bed hold is handled once, in the service, not re-implemented (differently, wrongly) by whoever queries the table next
- At the viva you can point at a clean boundary and explain it. "We all query each other's tables directly" is a bad answer to a question about architecture
- The rubric scores **Integrated Architecture** at 10 group marks

### What this looks like in code

```csharp
// Emergency Service needs to know where to send an ambulance.

// WRONG — reaching into Patient Management's tables
var freeBeds = await _db.Beds
    .Where(b => b.Condition == BedCondition.Usable && !b.Assignments.Any(...))
    .CountAsync();

// RIGHT — asking the owner
public class DispatchService(IPatientCapacityService capacity)
{
    var summary = await capacity.GetWardCapacityAsync();
}
```

The second version keeps working when Patient Management adds hold expiry, out-of-service beds, or a new ward type. The first version quietly goes wrong and nobody notices until the demo.

---

## 2. Ownership map

| Entity | Owner | Who reads it | Who writes it |
| :--- | :--- | :--- | :--- |
| `EmergencyCall`, `Ambulance`, `Dispatch`, `RouteLog` | **Emergency (M1)** | Patient (dispatch ETA) | Emergency only |
| `StaffMember`, `Shift`, `Allocation`, `LeaveRequest` | **Staff (M2)** | All — everyone stores staff IDs | Staff only |
| `EquipmentItem`, `StockLevel`, `MaintenanceSchedule`, `Warning` | **Equipment (M3)** | Patient (bed-frame status) | Equipment only |
| `Patient`, `Admission`, `Discharge` | **Patient (M4)** | Emergency, Staff (aggregates only) | Patient only |
| `Ward`, `Bed`, `BedAssignment` | **Patient (M4)** — *open, see §5.1* | Emergency, Staff, Equipment | Patient only |
| Agent workflow state | **Group / leader** — *open, see §7.2* | All four agents | All four agents |
| `User`, roles, JWT | **Group / leader** | All | Leader |

---

## 3. Patient Management ↔ Emergency Service (Member 1)

Three touch points, in the order they happen.

### 3.1 A patient raises an emergency call

A logged-in patient opens Flutter and taps **"I need an ambulance."**

| Piece | Owner |
| :--- | :--- |
| The screen in the patient app | **Patient (M4)** — it's a patient-role screen |
| The `POST /api/emergency-calls` endpoint | **Emergency (M1)** |
| The `EmergencyCall` record and everything downstream | **Emergency (M1)** |

M4 builds a form. M1 builds the data and the workflow. The form posts to M1's endpoint.

**Why this is worth doing:** because the caller is logged in, we already know exactly who they are — NIC, age, gender, contact, past visits. The emergency call carries a `patient_id`, so the pre-admission starts with complete details instead of guesses.

That gives the demo a nice contrast: the same system handling a known patient who called from their phone, and an unidentified patient who arrives unconscious as `UNKNOWN-2026-0142`.

**Open:** can a bystander raise a call *for someone else*? If yes, that path produces an unidentified patient. If no, record it as a stated limitation.

### 3.2 Dispatch happens → Patient Management pre-admits

When M1 dispatches an ambulance to a hospital, M4 needs to know so a bed can be found **before the ambulance arrives**.

**Emergency → Patient:**

```json
{
  "dispatch_id": "DSP-2026-0142",
  "patient_id": "uuid-or-null",
  "expected_arrival": "2026-08-19T14:30:00Z",
  "urgency": "emergency",
  "destination_ward_type_hint": "icu"
}
```

`patient_id` is present when the caller was a logged-in patient (§3.1) and null otherwise. When it's null, Patient Management creates an unidentified record.

Patient Management then creates an `Admission` with `source = emergency` and `dispatch_id` set, in status `awaiting_bed`.

> `destination_ward_type_hint` is a **hint, not an instruction.** The actual admission category is set by clinical staff on arrival or by the receiving nurse — never by the Emergency component and never by any AI. See §6.

**Open (§7.3):** does M1 call M4's service directly, or does the orchestrator drive both? Affects both specs.

### 3.3 Emergency reads ward capacity to choose a destination

M1's Dispatch & Routing Agent needs to know which hospital or ward has room. The group plan already routes this through Patient Management:

> "hospital ward capacity (**read-only, from Patient Management**)" — CareLanka_Component_Plan.md §1

**Patient → Emergency**, read-only:

```json
{
  "generated_at": "2026-08-19T14:05:00Z",
  "wards": [
    { "ward_id": "uuid", "name": "ICU",      "ward_type": "icu",     "gender_policy": "mixed",  "total_beds": 8,  "free_beds": 1 },
    { "ward_id": "uuid", "name": "Ward 5B",  "ward_type": "general", "gender_policy": "male",   "total_beds": 24, "free_beds": 7 }
  ]
}
```

`free_beds` counts beds that are usable, unoccupied, and not under a live reservation. Expired holds count as free. That logic lives in Patient Management's service so nobody re-implements it.

**No patient data crosses this boundary** — counts only, no names, no NICs, no conditions.

---

## 4. Patient Management ↔ Staff Management (Member 2)

This is the boundary that confuses people most, because staff are all over the patient workflow — nurses admit, doctors clear discharges, managers approve beds. **None of that makes patient data theirs, or staff data ours.**

### 4.1 We store staff IDs. We never store staff data.

Patient Management records *who did what*, for the audit trail. Every one of these is a foreign key to Staff Management's `StaffMember`:

| Our field | What it records |
| :--- | :--- |
| `Admission.category_set_by_staff_id` | Which clinician chose the care category — proof a human decided it |
| `BedAssignment.approved_by_staff_id` | Who approved the bed |
| `Discharge.confirmed_by_staff_id` | Who confirmed the discharge |
| Checklist item ticks | Who ticked each one |

We store the **ID only**. Name, role, department, qualifications, shift, leave — all live in `StaffMember` and belong to M2. When React needs to show "Approved by Dr. Perera," we ask M2's service for the name at read time.

**Why not copy the name in?** Because a staff member's name or role can change, and we'd be showing stale data with no way to notice. The ID is the fact; everything else is theirs to serve.

### 4.2 The doctor and clinical clearance

The discharge checklist has one item — `clinical_clearance` — that only a doctor may tick.

There is **no new entity and no coordination** for this. A doctor is a `StaffMember` with `role = doctor`, owned by M2. Patient Management just checks the role claim on the JWT:

```csharp
[Authorize(Roles = "Doctor")]
public async Task<IActionResult> SetClinicalClearance(...)
```

M2 owns the staff record. M4 owns the rule about who may tick which box on a discharge.

### 4.3 Staff reads ward occupancy to work out staffing demand

M2's Staff Allocation Agent needs to know how busy each ward is. The group plan again routes this through Patient Management:

> "ward staffing demand (**read-only, from Patient Management** and Emergency)" — CareLanka_Component_Plan.md §2

**Patient → Staff**, read-only:

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

`patients_by_category` is what makes this useful — fifteen routine inpatients and two high-dependency patients need very different staffing, even though both are "17 patients."

`incoming_next_2h` counts admissions in `bed_reserved` with an `expected_arrival` inside the window, so M2's agent can staff *ahead* of a rush instead of reacting to one.

Still counts only. **No patient identities cross this boundary.**

### 4.4 Optional: staffing feeding back into bed choice

Patient Management's agent has soft rules for ranking beds (spread the load, keep sicker patients near the nurses' station). A fourth could be *"prefer a ward that isn't short-staffed right now"* — reading M2's coverage data.

**Nice, but not in the first build.** It couples two agents to each other and both of you need working components before that's worth the risk. Revisit if there's time.

---

## 5. Patient Management ↔ Equipment Management (Member 3)

### 5.1 The bed question — OPEN, needs a group decision

A hospital bed is two different things at once, and the confusion comes from using one word for both:

| Thinking of it as… | Means | Natural owner |
| :--- | :--- | :--- |
| A **physical asset** — frame, serial number, purchase date, needs servicing | Inventory | Equipment (M3) |
| A **slot in a ward** — free, reserved, occupied | Capacity | Patient (M4) |

**Proposed split:** M3 owns the asset record, M4 owns the slot and its occupancy, joined by `Bed.equipment_asset_id`.

**Why occupancy cannot sit with Equipment.** A bed's occupancy changes for exactly two reasons: someone was admitted, or someone was discharged. Both are Patient Management operations. If Equipment owned that column, every single admission would have M4's code calling M3's service to say "mark bed 12 taken" — and then deciding what to do when that call fails, and whether to admit the patient anyway if M3's code is broken. That is two students' code on the critical path of the most common operation in the system, for nine weeks.

**Why Equipment doesn't lose anything.** M3 owns ventilators, monitors, defibrillators, wheelchairs, X-ray machines, syringes, gloves, drugs and oxygen — plus stock levels, usage rates, reorder points, maintenance schedules and calibration dates. Their agent forecasts what's about to run out and what's overdue for service. Roughly forty bed rows change none of that.

**Decision needed from M3 and the leader:** *does Equipment want bed frames as maintainable assets?* If yes, take the split above. If no, `Bed` sits entirely with Patient Management and `equipment_asset_id` is dropped.

### 5.2 Maintenance takes a bed out of service — the useful scenario

This is the integration worth building, and it goes both ways:

```
Equipment's Monitoring Agent
  "Bed frame ASSET-118 in Ward 5B is overdue for servicing"
        │
        │  M3 calls M4's service: mark this bed out of service
        ▼
Patient Management
  Bed.condition -> out_of_service
        │
        ▼
Patient Management's Bed Agent
  now has one fewer candidate bed
        │
        ▼
If that pushes a ward to full, the agent proposes a downgrade
  -> which needs Duty Manager approval
```

One equipment warning, and a human ends up approving a different bed for a patient. **Two components, two agents, one visible consequence** — that's a far better demo than either agent running alone, and it's exactly what the rubric means by orchestration.

**The one hard rule on this path:** a bed with a patient in it cannot be taken out of service. M4's service rejects it with `409 Conflict` and a message telling M3's agent to retry after discharge. Maintenance never evicts a patient.

**Reverse direction:** when servicing is done, M3 clears it and the bed returns to `usable`.

### 5.3 Ward list for equipment allocation

M3 allocates equipment *to* wards, so they need the ward list — id, name, type. Read-only from Patient Management. Small, but it means M3 never keeps their own copy of ward names that drifts out of date.

---

## 6. The rule that binds all four components

> **No component, and no AI agent, decides a patient's care category.**

`admission_category` (`icu` / `hdu` / `inpatient` / `day_case` / `outpatient`) is set by clinical staff and recorded with `category_set_by_staff_id`. It is an **input** to Patient Management's agent, never an output.

This is not caution — it's written into the group plan:

> "The AI never decides a patient's medical condition or diagnosis — it only works with the administrative category and checklist that clinical staff have already set." — CareLanka_Component_Plan.md §4

> "**Not a medical diagnosis system** — The Patient agent only works with administrative categories already set by staff — it never diagnoses" — CareLanka_Component_Plan.md §6

Emergency may pass a `destination_ward_type_hint` for routing. It is a hint. It never becomes the category on its own.

---

## 7. The full emergency scenario, with owners marked

```
Patient taps "I need an ambulance" in Flutter
  screen: M4   |   endpoint + EmergencyCall record: M1
        │
        ▼
Dispatch & Routing Agent                                        [M1]
  picks the nearest ambulance and a route
  asks Patient Management for ward capacity  ─────read──────►   [M4]
  chooses a destination hospital/ward
        │
        │  dispatch notification: dispatch_id, patient_id, ETA
        ▼
Pre-admission created, status = awaiting_bed                    [M4]
  clinical staff set admission_category                       (human)
        │
        ▼
Patient Admission & Bed Agent                                   [M4]
  filters beds on hard rules, ranks on soft rules
  proposes a bed, places a 30-minute hold
  deterministic validator re-checks every hard rule
        │
        ▼
Staff Allocation Agent                                          [M2]
  reads ward occupancy + incoming  ────────read──────────────►  [M4]
  flags the ward as short-staffed, proposes a reallocation
        │
        ▼
Equipment Monitoring Agent                                      [M3]
  checks the destination ward has the equipment it needs
  (if a bed frame is overdue, marks that bed out of service ──► [M4])
        │
        ▼
Duty Manager reviews the whole plan in React
        │
   ┌────┴────┐
APPROVE   REJECT / REVISE
   │           └──► back to the relevant agent, admission stays awaiting_bed
   ▼
Bed approval re-checked under a row lock, then committed        [M4]
Ambulance crew + ward nurse get their tasks in Flutter        [M1/M4]
   │
   ▼
Patient arrives, marked admitted, bed occupied                  [M4]
Patient sees "Ward 5B, Bed 12" on their own phone               [M4]
```

Every arrow between components is either **read-only** or **a call to the owner's service**. Nobody writes into anybody else's tables.

---

## 8. Contracts Patient Management provides

Injected as interfaces inside the API. Also exposed as REST endpoints so the AI agents (which may run outside ASP.NET Core) can reach them.

| Interface method | Endpoint | For | Returns |
| :--- | :--- | :--- | :--- |
| `GetWardCapacityAsync()` | `GET /api/capacity/wards` | M1 | Free/total beds per ward, with type and gender policy |
| `GetWardOccupancyAsync(wardId)` | `GET /api/wards/{id}/occupancy` | M2 | Occupied counts, category mix, incoming next 2h |
| `ListWardsAsync()` | `GET /api/wards` | M3 | Ward id, name, type |
| `CreatePreAdmissionAsync(dispatch)` | `POST /api/admissions/pre-admit` | M1 | Creates an admission from a dispatch |
| `SetBedConditionAsync(bedId, condition, reason)` | `POST /api/beds/{id}/condition` | M3 | 409 if the bed is occupied |

All JWT-protected and role-restricted. Aggregate endpoints return **counts, never patient identities.**

## 9. What Patient Management needs from others

| From | What | Why |
| :--- | :--- | :--- |
| **M1** | Dispatch notification — `dispatch_id`, `patient_id` (nullable), `expected_arrival`, `urgency` | Triggers pre-admission so a bed is ready before arrival |
| **M1** | An endpoint our patient-app screen can post an emergency call to | §3.1 |
| **M2** | Look up a staff member's name and role by ID | Displaying "Approved by …" without copying their data |
| **M2** | `Doctor` as a role on the JWT | Gating `clinical_clearance` |
| **M3** | Notification when a bed frame goes in or out of service | §5.2 |
| **Leader** | Shared agent-workflow tables | §10.2 |

---

## 10. Open items

**10.1 — Bed ownership (§5.1).** Blocks Patient Management's schema. Needs M3 and the leader.

**10.2 — Who owns the agent-workflow tables?** All four agents must persist workflow id, objective, plan, steps, tool results, validation results, errors, approval status and outcome (assignment §9.1). The rubric scores this under a **group** criterion — *"Integrated Architecture, Agent Orchestration and State Management (10)"* — and §10 requires one workflow crossing all four agents. Four separately designed schemas would make that trace a four-way join.
**Recommendation:** the leader owns one shared design, since `ai-orchestration-workflow.md` is already group-owned. Each component links to it by `workflow_id`.

**10.3 — Does M1 call M4 directly, or does the orchestrator drive both?** Affects §3.2.

**10.4 — Can a bystander raise an emergency call for someone else?** Affects §3.1 and whether the unidentified-patient path is reachable from the app.

**10.5 — Doctor role.** Confirmed as a Staff Management role. Nothing for M4 to build, but M2 needs it on the JWT.

---

## 11. For the other three members

This file currently describes every boundary **from the Patient Management side**. If you find something here that's wrong about your component, change it — don't work around it.

When you design yours, the two questions worth asking about anything you're unsure of:

1. **Who writes this?** Whoever's business rules cause the value to change owns the table. Everyone else reads through their service.
2. **Would this make my component depend on someone else's code being finished?** If yes, you can't demo alone, and you can't test alone. Push the dependency to a read-only call and keep a working fallback.
