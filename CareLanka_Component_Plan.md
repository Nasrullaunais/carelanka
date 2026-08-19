# CareLanka: Hospital Management System — Component Plan
**SE3090 — Assignment 1**

Four business components, one per team member, each with its own AI agent.
The system coordinates hospital operations across four areas: emergency response, staff management, equipment management, and patient management. Each area is owned by one team member and has its own AI agent. All four connect into one workflow, and all AI recommendations are approved by a human before they take effect.

## Roles

| App | Role | What they do |
| :--- | :--- | :--- |
| **Flutter (field)** | Ambulance Crew | Receives dispatch, navigates to the call, updates status, hands over patient info |
| **Flutter (field)** | Ward Nurse | Admits patients, updates patient status, requests discharge |
| **Flutter (field)** | Staff Member | Views own shifts, clocks in/out, requests leave or a shift swap |
| **React (admin)** | Duty / Dispatch Manager | Oversees emergency dispatch and approves patient admission plans |
| **React (admin)** | Hospital Administrator | Manages staff records and approves shift/roster plans |
| **React (admin)** | Equipment & Inventory Manager | Monitors equipment stock and approves procurement/maintenance |
| **Flutter (patient)** | Patient | Registers before a planned visit, views own admission status, bed/ward, and discharge details |

*Flutter is for people doing the actual work (crews, nurses, staff) and for patients following their own stay. React is for people managing operations and approving what the AI recommends. Staff roles and the patient role see completely different screens: staff act on other people's records, a patient may only ever read their own.*

**Patient accounts are optional and separate from patient records.** A `Patient` record is created by staff and exists whether or not that person ever logs in — an unconscious emergency arrival has a record and no account. An account, when one exists, is linked to the record afterwards. Patient Management owns the record; the account is only a read-only window onto it.

---

## 1. Ambulance / Emergency Service
Handles emergency calls from start to finish: taking the call, finding and dispatching the nearest available ambulance, routing it to the scene, and deciding which hospital/ward it should go to.

### Main functions
* Log an incoming emergency call with location and details
* Track ambulance location and availability in real time
* Dispatch the nearest available ambulance
* Route the ambulance using a maps/navigation service
* Decide which hospital or ward the patient should be taken to
* Record the outcome of each call

### Main data it owns
`EmergencyCall`, `Ambulance`, `Dispatch`, `RouteLog`

### AI agent: Dispatch & Routing Agent
* **What it does:** Given a new emergency call, works out which ambulance to send, the best route, and which hospital/ward to take the patient to.
* **What it looks at:** Ambulance locations and availability, live map/route data (external Maps API), and hospital ward capacity (read-only, from Patient Management)
* **What it produces:** A proposed ambulance, route, and destination hospital/ward
* **Who approves / uses it:** Sending the nearest ambulance happens immediately — speed matters in a real emergency. The Duty Manager only needs to approve it when the plan reassigns an ambulance already committed elsewhere, or sends the patient to a hospital other than the nearest one.

---

## 2. Staff Management
Manages hospital staff records and works out who should be covering which ward and shift, based on who is available and what skills are needed.

### Main functions
* Maintain staff records: role, department, qualifications
* Manage shift schedules and rosters
* Handle leave and shift-swap requests
* Allocate staff to wards/departments based on need
* Track staff coverage per ward

### Main data it owns
`StaffMember`, `Shift`, `Allocation`, `LeaveRequest`

### AI agent: Staff Allocation Agent
* **What it does:** Looks at which wards need coverage and proposes a shift roster or reallocation to fill the gaps, matching staff skills to the ward's needs.
* **What it looks at:** Staff availability and skills, current leave requests, and ward staffing demand (read-only, from Patient Management and Emergency)
* **What it produces:** A proposed roster or reallocation plan, with any understaffed wards flagged
* **Who approves / uses it:** The Hospital Administrator reviews and approves the roster before it becomes the live schedule.

---

## 3. Health Equipment
Tracks hospital equipment and medical stock, and keeps an eye on what is running low, overdue for maintenance, or needs to be moved to a ward that needs it more.

### Main functions
* Track equipment stock and condition
* Track maintenance and calibration schedules
* Raise warnings for low stock or overdue maintenance
* Recommend reordering, maintenance, or reallocating equipment between wards

### Main data it owns
`EquipmentItem`, `StockLevel`, `MaintenanceSchedule`, `Warning`

### AI agent: Equipment Monitoring Agent
* **What it does:** Reviews stock levels and maintenance schedules, and produces warnings and recommended actions before something actually runs out or fails.
* **What it looks at:** Stock levels, usage rates, and maintenance/calibration due dates
* **What it produces:** A prioritised list of warnings (low stock, overdue maintenance) with a recommended action for each
* **Who approves / uses it:** The Equipment & Inventory Manager approves any procurement or maintenance action above an agreed cost or urgency threshold.

---

## 4. Patient Management
Manages the patient's stay from admission to discharge: what category of care they need, which bed/ward they are assigned to, and when they are ready to be discharged.

### Main functions
* Register a new patient and record admission details
* Assign an administrative category (inpatient, outpatient, ICU, day-case) — set by clinical staff, never by the AI
* Maintain the ward and bed register, and the live free/occupied state of every bed
* Assign a bed/ward based on availability, category, and ward admission policy
* Track patient status during their stay
* Manage the discharge process
* Give the patient a read-only view of their own stay (status, ward/bed, discharge details) in Flutter

### Main data it owns
`Patient`, `Admission`, `Ward`, `Bed`, `BedAssignment`, `Discharge`

`Ward` and `Bed` sit here because occupancy only ever changes as a result of an admission or a discharge — the component that writes the data owns it. Equipment Management owns the movable medical equipment that is allocated *to* a ward (monitors, ventilators, consumables); Patient Management owns the ward itself and whether each bed is free.

### AI agent: Patient Admission & Bed Agent
* **What it does:** Given a patient's admission category (set by clinical staff, not the AI) and the current bed availability, proposes which bed/ward to assign them to, and flags patients who meet the criteria for discharge.
* **What it looks at:** Bed/ward availability, the admission category already set by clinical staff, and discharge checklist status
* **What it produces:** A proposed bed/ward assignment, and a list of patients flagged as ready for discharge review
* **Who approves / uses it:** A nurse or Duty Manager confirms the bed assignment and confirms discharge. The AI never decides a patient's medical condition or diagnosis — it only works with the administrative category and checklist that clinical staff have already set.

---

## 5. How the Four Agents Connect
The four agents are not four separate features — one emergency call can trigger all four in sequence, before a human approves the full plan.

```text
Emergency call comes in (Flutter / call intake)
 |
 v
Dispatch & Routing Agent
 -> proposes ambulance + route + destination ward
 |
 v
Patient Admission & Bed Agent
 -> checks bed availability, proposes admission + bed
 |
 v
Staff Allocation Agent
 -> checks ward staffing, flags if short-staffed
 |
 v
Equipment Monitoring Agent
 -> checks required equipment is available at that ward
 |
 v
Duty Manager reviews the full plan in React
 |
 +----+----+
 v         v
APPROVE   REJECT / REVISE
 |
 v
Ambulance crew + ward nurse get their tasks in Flutter
 |
 v
Patient arrives, intake completed, outcome recorded
```
*Every step — who proposed what, what was approved, and by whom — is saved so the full chain can be reviewed later.*

---

## 6. Quick Check Against the Assignment

| Requirement | How this plan meets it |
| :--- | :--- |
| **4 major business components, one per member** | Emergency, Staff, Equipment, Patient Management — one each |
| **At least 4 distinct AI agents** | Dispatch & Routing, Staff Allocation, Equipment Monitoring, Patient Admission & Bed — each with its own data and job |
| **At least 3 roles in Flutter, 3 in React, clearly different purposes** | 4 Flutter roles (crew, nurse, staff, patient) and 3 admin roles (dispatch, HR, equipment) — field work and patient self-service vs. oversight and approval |
| **Human approval before high-impact actions** | Reassigning committed resources, staff rosters, equipment spend above threshold, and bed/discharge decisions all need human sign-off |
| **At least one external API** | Maps/navigation API for ambulance routing |
| **Not a medical diagnosis system** | The Patient agent only works with administrative categories already set by staff — it never diagnoses |

**Bottom line:**
All four components are comparable in size, each has real business rules and a real AI job to do, and the emergency-call scenario gives a clean single workflow to demo end to end.
