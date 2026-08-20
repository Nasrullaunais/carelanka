# How the Four Components Connect

**CareLanka Hospital Management System · SE3090 Assignment 1**
**Status:** living document · Patient Management (Member 4) and Equipment Management (Member 3) sections are filled in; Emergency (Member 1) and Staff (Member 2) should add their own

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
  Controllers/  Emergency/  Staff/  Equipment/  Patient/
  Services/     Emergency/  Staff/  Equipment/  Patient/
  DTOs/         Emergency/  Staff/  Equipment/  Patient/
  Data/         one DbContext, all entities
```

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
| `EmergencyCall`, `Ambulance`, `Dispatch`, `RouteLog` | **Emergency (M1)** | Patient (dispatch ETA) | Emergency only |
| `StaffMember`, `Shift`, `Allocation`, `LeaveRequest` | **Staff (M2)** | All — everyone stores staff IDs | Staff only |
| `EquipmentItem`, `EquipmentCategory`, `PharmacyItem`, `PharmacyCategory`, `PharmacyTransaction`, `MaintenanceSchedule`, `Warning`, `ActionRequest` | **Equipment (M3)** | Patient (ward equipment readiness); any staff (search/availability) | Equipment only |
| **`Bed`** — exists, number, condition, repairs | **Equipment (M3)** | Patient (to find candidates) | Equipment only |
| `Patient`, `Admission`, `Discharge` | **Patient (M4)** | Emergency, Staff (aggregates only) | Patient only |
| **`BedAssignment`** — who is in a bed, holds, approvals | **Patient (M4)** | Equipment (before servicing a bed) | Patient only |
| `Ward` — name, type, gender policy | **Patient (M4)** — *see §11.1* | All | Patient only |
| Agent workflow state | **Group / leader** — *open, §11.2* | All four agents | All four agents |
| `User`, roles, JWT | **Group / leader** | All | Leader |

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

**Minimum details only.** An emergency form must be fast — a few required fields, nothing optional. Everything else is filled in later once the patient is in a bed and someone has time.

**Location and maps belong to M1, entirely.** They own the location capture, the map and the routing. M4 builds a form that posts to their endpoint and consumes whatever their call record provides — we do not implement location handling of any kind.

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
  "urgency": "emergency",
  "destination_ward_type_hint": "icu"
}
```

**`patient_is_caller` is the field that matters**, and M1 must pass it through from the question asked on the call screen.

| `patient_is_caller` | What Patient Management does |
| :--- | :--- |
| `true` | `patient_id` is set; link the admission to that existing record. Details usually complete already. |
| `false` | Create a new patient from `provisional_name` / `provisional_gender`, or a `temp_reference` if nothing is known. Record `caller_user_id` as the emergency contact and as `reported_by_user_id`. |

Getting this wrong means filing one person's emergency under another person's medical record, so it is asked explicitly rather than inferred.

Patient Management then creates an `Admission` with `source = emergency` and `dispatch_id` set, in status `awaiting_bed`.

> `destination_ward_type_hint` is a **hint, not an instruction.** The actual admission category is set by clinical staff — never by the Emergency component and never by any AI. See §7.

**Open (§11.3):** does M1 call M4's service directly, or does the orchestrator drive both?

### 4.3 Emergency reads ward capacity to choose a destination

M1's Dispatch & Routing Agent needs to know which ward has room. The group plan already routes this through Patient Management:

> "hospital ward capacity (**read-only, from Patient Management**)" — CareLanka_Component_Plan.md §1

```json
{
  "generated_at": "2026-08-19T14:05:00Z",
  "wards": [
    { "ward_id": "uuid", "name": "ICU",     "ward_type": "icu",     "gender_policy": "mixed", "total_beds": 8,  "free_beds": 1 },
    { "ward_id": "uuid", "name": "Ward 5B", "ward_type": "general", "gender_policy": "male",  "total_beds": 24, "free_beds": 7 }
  ]
}
```

`free_beds` counts beds that exist in Equipment's register, are `usable`, and have no live reservation in Patient Management's `BedAssignment`. Expired holds count as free. That combined logic lives in Patient Management's service so nobody re-implements it.

**No patient data crosses this boundary** — counts only.

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

> "ward staffing demand (**read-only, from Patient Management** and Emergency)" — CareLanka_Component_Plan.md §2

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
   │            proposes a downgrade — which always needs
   │            Duty Manager approval
```

**The hard rule: maintenance never evicts a patient.** If the bed is occupied or under a live hold, Equipment waits. M4 exposes the check; M3 respects the answer.

One equipment warning ends with a human approving a different bed for a patient. **Two components, two agents, one visible consequence** — a far better demo than either agent running alone, and exactly what the rubric means by orchestration.

**Reverse direction:** when servicing finishes, M3 returns the bed to `usable` and it re-enters M4's candidate pool automatically. No call needed — M4 reads the current condition every time.

### 6.3 Ward list for equipment allocation

M3 allocates equipment *to* wards, so they read the ward list — id, name, type — from Patient Management. Small, but it means M3 never keeps their own copy of ward names that drifts.

---

## 7. The rule that binds all four components

> **No component, and no AI agent, decides a patient's care category.**

`admission_category` (`icu` / `hdu` / `inpatient` / `day_case` / `outpatient`) is set by clinical staff and recorded with `category_set_by_staff_id`. It is an **input** to Patient Management's agent, never an output.

This is not caution — it is written into the group plan:

> "The AI never decides a patient's medical condition or diagnosis — it only works with the administrative category and checklist that clinical staff have already set." — CareLanka_Component_Plan.md §4

> "**Not a medical diagnosis system** — The Patient agent only works with administrative categories already set by staff — it never diagnoses" — CareLanka_Component_Plan.md §6

Emergency may pass a `destination_ward_type_hint` for routing. It stays a hint.

---

## 8. The full emergency scenario, with owners marked

```
Patient taps "I need an ambulance" in Flutter
  screen: M4   |   endpoint + EmergencyCall record: M1
        │
        ▼
Dispatch & Routing Agent                                        [M1]
  picks the nearest ambulance and a route (maps API)
  asks Patient Management for ward capacity  ─────read──────►   [M4]
  chooses a destination ward
        │
        │  dispatch notification: dispatch_id, patient_id, ETA
        ▼
Pre-admission created, status = awaiting_bed                    [M4]
  clinical staff set admission_category                       (human)
        │
        ▼
Patient Admission & Bed Agent                                   [M4]
  reads Equipment's bed register  ────────read──────────────►   [M3]
  filters on hard rules, ranks on soft rules
  proposes a bed, places a 30-minute hold in BedAssignment
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

| Interface method | Endpoint | For | Returns |
| :--- | :--- | :--- | :--- |
| `GetWardCapacityAsync()` | `GET /api/capacity/wards` | M1 | Free/total beds per ward, with type and gender policy |
| `GetWardOccupancyAsync(wardId)` | `GET /api/wards/{id}/occupancy` | M2 | Occupied counts, care mix, incoming next 2h |
| `ListWardsAsync()` | `GET /api/wards` | M3 | Ward id, name, type |
| `GetBedOccupancyAsync(bedId)` | `GET /api/beds/{id}/occupancy` | M3 | Whether a bed is occupied or held — **check this before servicing it** |
| `CreatePreAdmissionAsync(dispatch)` | `POST /api/admissions/pre-admit` | M1 | Creates an admission from a dispatch |

All JWT-protected and role-restricted. Aggregate endpoints return **counts, never patient identities.**

## 10. What Patient Management needs from others

| From | What | Why |
| :--- | :--- | :--- |
| **M1** | Dispatch notification — `dispatch_id`, `patient_id` (nullable), `expected_arrival`, `urgency` | Triggers pre-admission so a bed is ready before arrival |
| **M1** | An endpoint our patient-app screen can post an emergency call to | §4.1 |
| **M1** | Location capture and maps on the emergency form | §4.1 — entirely theirs; M4 builds only the form |
| **M1** | `caller_user_id` and `patient_is_caller` on the dispatch notification | §4.2 — without these we cannot tell whose medical record this is |
| **M2** | Look up a staff member's name and role by ID | Displaying "Approved by …" without copying their data |
| **M2** | `Doctor` as a role on the JWT | Gating `clinical_clearance` |
| **M3** | A readable bed register: bed id, ward, number, condition, isolation capability | Our agent's candidate list. **This is our hardest dependency** — without it the bed agent has nothing to reason over. |
| **M3** | Notification (or just a condition change we can read) when a bed goes in or out of service | §6.2 |
| **Leader** | Shared agent-workflow tables | §11.2 |

---

## 11. Open items

**11.1 — Does `Ward` sit with Patient Management or Equipment?**
Beds are settled (§6.1). Wards are not. Argument for M4: a ward's `gender_policy` and `ward_type` are admission-policy facts that drive the bed agent's hard rules — Equipment does not care whether a ward is male or female, only about frames and servicing. Written as M4's for now; M3 and the leader to confirm.

**11.2 — Who owns the agent-workflow tables?**
All four agents must persist workflow id, objective, plan, steps, tool results, validation results, errors, approval status and outcome (assignment §9.1). The rubric scores this under a **group** criterion — *"Integrated Architecture, Agent Orchestration and State Management (10)"* — not an individual one, and §10 requires one workflow crossing all four agents. Four separately designed schemas would make that trace a four-way join.
**Recommendation:** the leader owns one shared design, since `ai-orchestration-workflow.md` is already group-owned. Each component links by `workflow_id`.
**Flagged for the leader. Not decided.**

**11.3 — Does M1 call M4 directly for pre-admission, or does the orchestrator drive both?** Affects §4.2 and both specs.

**11.4 (RESOLVED) — A bystander can raise a call for someone else.** The call screen asks once; M1 passes `patient_is_caller` and `caller_user_id` through on the dispatch notification (§4.2). The caller is stored as the patient's emergency contact. **M1 needs to add these two fields** — confirm with Member 1.

**11.6 — Name collisions across the four specs. Each row needs an owner.**
The four `*-spec.yaml` files describe **one** ASP.NET application, so routes,
`operationId`s and schema names are global, not per-component. A duplicate route
throws at startup; a duplicate `operationId` or schema name silently collides in
the generated clients, and whichever one generates second wins. None of these is
one member's call — the two or three members sharing the name have to agree.

Re-verified against `main` on 2026-08-21:

| Collision | Where | Suggested fix |
| :--- | :--- | :--- |
| `GET /reports/agent-performance` | staff, equipment, patient | Namespace by component: `/reports/staff/agent-performance` |
| `GET /workflows/{workflowId}` | equipment, patient | Group-owned once §11.2 is settled — one workflow endpoint, not four |
| `operationId: getAgentPerformanceReport` | staff, patient | Follows whatever the route above becomes |
| `PagedResult` — staff says `total_count`, the others `total_items` | all three | `total_items`, on the 2-vs-1 count. Staff to confirm |
| `Bed`, `WorkflowSummary`, `WorkflowAccepted`, `AgentPerformanceReport` — one name, different shapes | across specs | Either make them byte-identical or give them different names (`EquipmentBed` / `AdmissionBed`) |

**Also blocking:** `emergency-spec.yaml` is still a 212-byte stub with
`paths: {}`. Everything §10 lists under M1 — the dispatch notification,
`patient_is_caller`, `caller_user_id` — has nowhere to live until M1 writes it.

**11.5 — Booking a visit.** A patient can register their details and an expected arrival ahead of a planned visit (`source = pre_registered`). This is deliberately **not** a full appointment system — no doctor calendars, no time slots, no rescheduling — because that is a component-sized feature on its own. If the group wants real appointments, it needs an owner and something else has to be dropped.

*Resolved:* `Doctor` is a Staff Management role — M4 only checks the JWT claim. Bed ownership split agreed (§6.1). No SMS integration; the group's Maps API covers the third-party requirement.

---

## 12. For the other three members

This file originally described every boundary **from the Patient Management side**, because that was the first component designed. Equipment Management (§13–§16 below) has since added its own sections, written against `equipment-management-plan.md` and `equipment-spec.yaml`. Emergency (M1) and Staff (M2) should do the same. If something here is wrong about your component, raise it in §11 rather than working around it.

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
| **Leader** | Shared agent-workflow tables | Same open item as §11.2 — `ActionRequest.workflow_id` and `Warning.workflow_id` point into whatever the group leader designs |
