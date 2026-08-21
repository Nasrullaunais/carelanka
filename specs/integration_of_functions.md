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
| Agent workflow state | **Group** — *open, §11.2* | All four agents | All four agents |
| `StaffMember`, login, JWT issuing | **Group** — shared plumbing, built once | All | Whoever takes it on |

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
| **Group** | Shared agent-workflow tables | §11.2 |

---

## 11. Open items

**11.1 — Does `Ward` sit with Patient Management or Equipment?**
Beds are settled (§6.1). Wards are not. Argument for M4: a ward's `gender_policy` and `ward_type` are admission-policy facts that drive the bed agent's hard rules — Equipment does not care whether a ward is male or female, only about frames and servicing. Written as M4's for now; M3 and the group to confirm.

**11.2 — Who owns the agent-workflow tables?**
All four agents must persist workflow id, objective, plan, steps, tool results, validation results, errors, approval status and outcome (assignment §9.1). The rubric scores this under a **group** criterion — *"Integrated Architecture, Agent Orchestration and State Management (10)"* — not an individual one, and §10 requires one workflow crossing all four agents. Four separately designed schemas would make that trace a four-way join.
**Recommendation:** one shared design, group-owned, since `ai-orchestration-workflow.md` is already group-owned. Each component links by `workflow_id`.
**Needs a group decision. Not decided.**

*Update:* `ai-orchestration-workflow.md` §5 now proposes exactly this — one `AgentWorkflow` row per agent run, chained by `correlation_id` and `parent_workflow_id`, group-owned. Settle it alongside the orchestration decision in that document, since the table design follows from it.

**11.3 — Does M1 call M4 directly for pre-admission, or does the orchestrator drive both?** Affects §4.2 and both specs. **Partially settled by implementation:** `patient-spec.yaml`'s `POST /admissions/pre-admit` is already documented as "Called by Emergency Service (Member 1) when an ambulance is dispatched", and `emergency-spec.yaml` (§22–§26, §24.2) now calls it directly from the Dispatch & Routing Agent's plan. That is the current build path. If the Coordinator Agent proposed in `ai-orchestration-workflow.md` §3 is adopted later, the coordinator takes over driving both calls and this direct call is replaced — not a breaking change, since the coordinator would just call the same endpoint in Emergency's place.

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

**Still open, deliberately:** `/bed-workflows/{workflowId}` is an **interim name**, not a
claim. §11.2 has not been settled, and once the group decides who owns the agent-workflow
tables this should collapse into **one** shared workflow endpoint rather than one per
component. Renaming was done only to stop the route clash from crashing startup.

**The rule that keeps this cleared:** the four specs describe one ASP.NET application, so
routes, `operationId`s and schema names are global. Before adding any of the three, check
it does not already exist in another spec. A shared name is fine *only* if the definition
is byte-identical — currently `PagedResult`, `ProblemDetails`, `ValidationProblemDetails`,
`AuditFields` and `BedCondition`. CI should run the uniqueness sweep so this cannot
silently regress.

*Also resolved:* `Doctor` is a Staff Management role — M4 only checks the JWT claim. Bed ownership split agreed (§6.1). No SMS integration; the group's Maps API covers the third-party requirement. `emergency-spec.yaml` is no longer the 212-byte stub — see §22–§26 for what it now publishes, including the dispatch notification, `patient_is_caller` and `caller_user_id` that §10 was waiting on.

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

- **`DispatchCrew` (Emergency's own table, §22–§26) stores `StaffMemberId`.** Same ID-only pattern as §5.1/§14.1/§18 — Emergency stores who crewed a dispatch, Staff owns the person. Emergency resolves names through `POST /staff/lookup`, the same endpoint Patient and Equipment already use; nothing Emergency-specific was needed on Staff's side.
- **`ambulance_crew` and `general_staff` are already staff roles** in `staff-spec.yaml`'s `StaffRole` enum, so Emergency's Flutter screens authorize against the same JWT claim every other component reads — no separate role system for crew.
- **Staff's roster does not currently model ambulance duty.** `Shift`/`Allocation` are ward-based (`Shift.WardId`); an ambulance run is not a ward shift. `docs/entity_diagram.md` keeps `DispatchCrew` as Emergency's own join table for exactly this reason (Decision 30: *"ambulance duty is real-time, not part of the ward-based shift roster"*) — so there is nothing to reconcile between the two schedules, they are deliberately separate concepts.

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

§4 above already documents this boundary from Patient Management's side, written before `emergency-spec.yaml` existed. `emergency-spec.yaml` (Member 1, Kaveesha) now agrees with every point found there:

- **The call screen split holds.** Patient Management builds the emergency-call form (a patient-role Flutter screen); Emergency owns `POST /emergency-calls`, the `EmergencyCall` record and everything downstream. Neither side writes the other's table.
- **`patient_is_caller` and `caller_user_id` are now on the wire, as §4.2 and §10 asked for.** `emergency-spec.yaml`'s `CreateEmergencyCallRequest` carries `patient_is_caller` as a required field. **`caller_user_id` is not a request field** — it is read from the JWT of whoever posts the call, because a client that could name its own caller id could file a call under somebody else's account. It is null for a call logged at the front desk on behalf of a walk-in or phone caller. Both are carried forward unchanged onto the dispatch notification's `DispatchNotification` schema, matching the JSON shape §4.2 already specified field-for-field: `dispatch_id`, `caller_user_id`, `patient_is_caller`, `patient_id`, `provisional_name`, `provisional_gender`, `expected_arrival`, `urgency`, `destination_ward_type_hint`.
- **`urgency` is translated by Emergency before the call, not by Patient afterwards.** The two components rank different things — `CallPriority` is how fast an *ambulance* is needed, `AdmissionUrgency` is how fast a *bed* is — so `DispatchNotification.urgency` carries Patient's vocabulary, not Emergency's. The table is fixed in C# on Emergency's side and is not something the agent decides: `critical` → `emergency`, `high` → `urgent`, `medium` and `low` → `routine`. Lossy on purpose, one-directional, and written in **four** places that must agree — here, `emergency-spec.yaml`'s `DispatchNotification`, `emergency-management-plan.md` §6, and `patient-spec.yaml`'s `PreAdmitRequest`. `POST /admissions/pre-admit` rejects anything outside Patient's three values with a 400, so a drift here fails loudly rather than filing an emergency as routine.
- **`caller_user_id` is a `PatientAccount.Id`, never a `Patient.Id`.** `docs/entity_diagram.md` Rev 2.5 adds the `PatientAccount` table and repoints `EmergencyCall.CallerUserId` at it. This is Patient Management's omission, not Emergency's — the patient login was resolved in Rev 2.3 and the table was never written down, so Rev 2.4 reasonably guessed `Patient.Id`. The bystander case is why it matters: `caller_user_id` is the helper's **login**, `patient_id` is the casualty's **medical record**, and one FK to one table would mean fabricating a record for the healthy person every time. `Patient.user_account_id` is the single optional link between them.
- **A call can also arrive by phone, and that needs no new contract.** Patient Management's emergency screen carries a `tel:` link to the hospital number beside the button (`patient-management-plan.md` §10.1); a staff member takes the details and posts the same `POST /emergency-calls`, which `emergency-spec.yaml` already documents as *"also used by staff logging a call at the front desk for someone with no phone or app"*. `caller_user_id` is null on that path — which is exactly what it is nullable for. No endpoint, table or field is added by either component, and no telephony system is being built.
- **Location and maps are entirely Emergency's**, per §4.1. `EmergencyCall.latitude` / `longitude` are required fields on Emergency's own create request; Patient's form only forwards whatever the caller enters, with no location logic of its own.
- **`destination_ward_type_hint` stays a hint.** `emergency-spec.yaml`'s Dispatch & Routing Agent proposes it, but `admission_category` is set by clinical staff in Patient Management — the same wall §7 and §4.2 already draw, and Emergency's spec does not attempt to cross it.
- **Open item 11.3 is now practically resolved** (see the updated §11.3 above): the agent's plan calls Patient's `POST /admissions/pre-admit` directly once a dispatch is created, matching what `patient-spec.yaml` already documented as the expected caller.
- **Emergency reads Patient's ward capacity, read-only, counts only** — `GET /capacity/wards` — exactly as §4.3 specifies. No patient data crosses this boundary from either side.

## 23. Emergency ↔ Staff Management (Member 2)

- **`DispatchCrew` stores `StaffMemberId`, ID only.** Same pattern as every other cross-component staff reference in this document (§5.1, §14.1, §19) — Emergency never copies a crew member's name, role or department; it resolves them through Staff's `POST /staff/lookup` (§18.1) at read time, for the React dispatch board and the call-outcome report.
- **`ambulance_crew` is a `StaffRole` Staff already issues.** Emergency's Flutter crew screens (§4.1 of `docs/CareLanka_Component_Plan.md`) authorize against that JWT claim; Emergency defines no role of its own.
- **Ambulance duty stays outside the ward roster, on both sides' account.** §19 already records Staff's reasoning (`docs/entity_diagram.md` Decision 30); Emergency's `DispatchCrew` table is the other half of that same decision — crew assignment is real-time and per-dispatch, not a `Shift`/`Allocation` row.

## 24. Emergency ↔ Equipment Management (Member 3)

There is close to no boundary here, and that is a design decision recorded in `docs/entity_diagram.md`, not an oversight:

- **Ambulances carry no tracked equipment.** `docs/entity_diagram.md`'s `Ambulance` note is explicit: *"Onboard equipment is explicitly not tracked (equipment stays ward-scoped only)"* (Decisions 9, 14). Equipment's `EquipmentItem.WardId` is non-nullable — every tracked item belongs to a ward, never to a vehicle — so there is no shared table, no read, no write between the two components.
- **The only indirect link is the destination ward**, and that link already runs through Patient Management (§22, §6.3), not directly to Equipment. Emergency never calls Equipment's API.

### 24.1 The Dispatch & Routing Agent's plan, for context on §22–§23

`emergency-spec.yaml`'s Dispatch & Routing Agent (`AgentType.DispatchRouting` in `docs/entity_diagram.md`) plans in this order:

1. Read the incoming `EmergencyCall` — location and priority. **The dispatcher sets the priority, not the agent**; triage is clinical judgement, the same wall §7 draws around `admission_category`.
2. List available ambulances (own data) and rank by **real driving ETA** via the maps API — the assignment's one required third-party integration (§4.1), and the reason it earns its place rather than being decorative.
3. Read Patient Management's `GET /capacity/wards` (§22) to attach a `destination_ward_type_hint` — a hint only, never `admission_category`.
4. **Routine case:** a free ambulance exists → the proposal lands in `pending_confirmation` and the dispatcher sends it with **one tap**.
5. **Diversion case:** nothing is free, but an ambulance is still *driving to* a lower-priority call → the proposal lands in `pending_approval` with a `DiversionImpact` block showing what it costs that other patient, and the Duty Manager decides in React (assignment §9.1).
6. **Neither:** outcome `no_ambulance_available`, workflow `failed`, recorded honestly rather than retried in a loop.
7. Once a human has confirmed or approved and a `Dispatch` exists, call Patient Management's `POST /admissions/pre-admit` (§22) so a bed search can start before the ambulance arrives.

**Two things here matter to the other three members, because they change what the group can claim:**

- **Nothing this agent proposes reaches the road without a person.** An earlier draft had the routine dispatch auto-approved with no human gate. That has been replaced by the one-tap confirm, which means **no agent in CareLanka — none of the four — now writes production data without human approval.** That is a cleaner story for the group's Agentic AI rubric line than "three of the four do." It does change `docs/CareLanka_Component_Plan.md` §4.1, which has been updated to match and is flagged there and in `emergency-management-plan.md` §14 as **needing group confirmation**.
- **An ambulance that has reached its patient is never diverted.** Enforced in C# at proposal time, at approval time and on the manual path. Worth knowing outside Emergency because it is why a dispatch can still be reassigned while its ambulance is `en_route` but not once that ambulance is `at_scene` or `transporting`. Note which enum that is: `at_scene` is an **`AmbulanceStatus`**, not a `DispatchStatus` — the vehicle reports it, the dispatch does not have that state. Anyone reading dispatch status from outside this component and expecting to find `at_scene` there will not, and needs the ambulance's status instead.

### 24.2 Note

This section exists so a reader of `integration_of_functions.md` does not have to separately open `emergency-management-plan.md` to see why §22 and §23 read the way they do; the full design, including both approval gates, the divertibility rule and the React/Flutter screen split, is in `emergency-management-plan.md`.

## 25. Contracts Emergency Service provides

Mirrors §9's, §15's and §20's format, from Emergency's side. All JWT-protected; role restrictions per `emergency-management-plan.md` §7.

| Endpoint | For | Returns |
| :--- | :--- | :--- |
| `POST /emergency-calls` | Patient Management's patient-facing "I need an ambulance" screen (§4.1, §22) | The created `EmergencyCall`, including `id` for the caller's app to track against, plus the dispatch proposal already raised |
| `GET /dispatches/{id}` | Patient Management / a future orchestrator, to check dispatch status without waiting on the push notification | Dispatch status, ambulance, crew, destination ward (once set), route and ETA |
| `GET /me/emergency-calls/{id}/tracking` | The patient's own Flutter screen | Ambulance position and ETA for a call **they** raised. Deliberately narrow — no crew names, no notes, no other calls. Emergency's own endpoint, not a filtered staff response |

## 26. What Emergency needs from others

Mirrors §10's, §16's and §21's format, from Emergency's side.

| From | What | Why |
| :--- | :--- | :--- |
| **M4 (Patient)** | `GET /capacity/wards` | The Dispatch & Routing Agent's destination-ward choice (§24.1 step 3) |
| **M4 (Patient)** | `POST /admissions/pre-admit` | Creates the pre-admission before the ambulance arrives (§24.1 step 6, §22) |
| **M2 (Staff)** | `POST /staff/lookup` | Resolving crew member names for the React dispatch board and the call-outcome report, without copying staff data (§23) |
| **M2 (Staff)** | `ambulance_crew` as a role on the JWT | Gating the Flutter crew screens (§23) |
| **Group** | Shared agent-workflow tables | Same open item as §11.2, §16 and §21 — the Dispatch & Routing Agent links to whatever the group agrees rather than inventing its own workflow schema |
