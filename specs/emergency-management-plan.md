# Emergency / Ambulance Service — Component Design

**CareLanka Hospital Management System · SE3090 Assignment 1**
**Owner:** Member 1 · **Status:** aligned for implementation · **Version:** 0.3

This is the design document for the Emergency / Ambulance component. It explains what the component does, what data it owns, how it talks to the other three components, and how its AI agent works.

`emergency-spec.yaml` (the OpenAPI contract) is generated from the decisions in this file. If a decision changes here, that file changes too.

---

## 1. What this component is responsible for

Emergency handles one call from the moment it comes in to the moment the patient is handed over at CareLanka Hospital: taking the call, finding and dispatching an eligible ambulance with its current crew, routing it to the scene and the hospital's configured emergency entrance, and notifying Patient Management so it can prepare the patient and bed workflow.

It answers four questions:

1. **Someone needs an ambulance — who is it for, and where are they?** — the call record
2. **Which ready response unit goes?** — an eligible ambulance and its current crew
3. **How does the crew get there and back?** — backend route/ETA calculation plus a Google Maps launch target
4. **What happened once they arrived?** — the outcome recorded against the call

### What it deliberately does *not* do

| Not our job | Whose job |
| :--- | :--- |
| Deciding a patient's care category or ward | Patient Management and clinical staff. Emergency routes every transport to the one configured hospital emergency entrance. |
| Finding a bed, admitting, discharging | Patient Management (Member 4). We tell them a dispatch happened; they do the rest. |
| Nurse rosters, who is on shift in a ward | Staff Management (Member 2). Current ambulance assignment and responding-crew history are ours; ward shifts are theirs. |
| Ventilators, monitors, consumables | Equipment Management (Member 3). Ambulances carry no tracked equipment — see `docs/entity_diagram.md`, Decisions 9 & 14. |
| Diagnosis, treatment, deciding how sick someone is | Out of scope for the entire project. Even `CallPriority` is set by the human dispatcher, never by the agent. |

> **The line we do not cross:** the AI never decides *where the patient should be treated clinically*, and it never puts an ambulance on the road by itself. It ranks options and explains its reasoning; a person presses the button.

---

## 2. Roles that touch this component

| Role | App | What they can do here |
| :--- | :--- | :--- |
| **Duty / Dispatch Manager** | React | The whole desk: live call board, set and adjust call priority, confirm routine dispatches (one tap), approve or reject diversions, manage the ambulance register, assign crew, watch the fleet map, read the reports |
| **Ambulance Crew** | Flutter | Acknowledge or decline their response unit's dispatch, launch Google Maps, update run status, report position, hand over patient info, and see their own run history |
| **Patient** | Flutter | Raise an emergency call for themselves or another person, see narrow tracking for calls they raised, cancel before dispatch, or request cancellation after assignment. The screen is Patient Management's (`integration_of_functions.md` §4.1/§22); the endpoint and downstream workflow are ours |
| **Any authenticated staff role** | React / Flutter | Log a call at the front desk for someone with no phone or app |

---

## 3. Data model

All six entities are in `docs/entity_diagram.md`'s Emergency section and are owned here.

### 3.1 Entities

```
Patient (Patient's table, read-only FK) ──< EmergencyCall >── Dispatch ── Ambulance
                                                                  │           │
                                                                  ├─ DispatchCrew  └─ AmbulanceCrewAssignment
                                                                  │                     │
                                                                  └─ RouteLog               └─ StaffMember (Staff's table, ID only)
```

**EmergencyCall** — one row per call, from ring to outcome.

| Field | Type | Notes |
| :--- | :--- | :--- |
| `id` | uuid, PK | |
| `patient_id` | uuid, FK → Patient, nullable | Often unknown at the scene; set later via the link-patient operation (§8) *(Decision 25)* |
| `caller_user_id` | uuid, nullable | *(Rev 2.4 addition)* The logged-in app user who placed the call, if any |
| `patient_is_caller` | boolean | *(Rev 2.4 addition)* Answered once, on the call screen — closes `integration_of_functions.md` §11.4 |
| `caller_name` / `caller_phone` | text, nullable | Free-text fallback when the caller has no app account |
| `latitude` / `longitude` | numeric(9,6) | Required — the scene location |
| `location_accuracy_metres` | numeric | Device-reported accuracy, required |
| `location_captured_at` | timestamptz | When the submitted position was captured |
| `idempotency_key` | uuid | Unique per caller submission; retries return the original call |
| `address_label` | text, nullable | *Addition* — human-readable address from the maps API's reverse geocode, so the crew reads a street name rather than coordinates |
| `details` | text, nullable | Free-text description of the emergency |
| `priority` | `CallPriority` | `critical` `high` `medium` `low` — **set by the dispatcher, never the agent** |
| `status` | `CallStatus` | `received` `dispatched` `en_route` `completed` `cancelled` |
| `outcome` | text, nullable | Set once the crew hands over — see §8 |
| `transported` | boolean, nullable | *Addition* — not every call ends in a hospital trip |
| `cancellation_request_status` | `CancellationRequestStatus`, nullable | `pending`, `approved`, or `rejected`; null until a post-assignment request exists |
| `cancellation_request_reason` | text, nullable | Patient's reason, visible to the Duty Manager |
| `cancellation_requested_at` | timestamptz, nullable | When the caller requested review |
| `cancellation_reviewed_at` / `cancellation_reviewed_by_staff_id` | timestamptz / uuid, nullable | Duty Manager decision stamp |
| `cancellation_review_notes` | text, nullable | Optional explanation returned to the caller |
| `created_at` / `updated_at` | timestamptz | |

**Ambulance** — the vehicle register. Soft-deletable, so a retired vehicle keeps its dispatch history.

| Field | Type | Notes |
| :--- | :--- | :--- |
| `id` | uuid, PK | |
| `registration_number` | text, unique among active | |
| `current_latitude` / `current_longitude` | numeric(9,6), nullable | Current position only — no time-series trail *(Decision 9)* |
| `location_updated_at` | timestamptz, nullable | Used to reject stale positions during eligibility checks |
| `status` | `AmbulanceStatus` | `available` `dispatched` `en_route` `at_scene` `transporting` `out_of_service` |
| `out_of_service_reason` | text, nullable | *Addition* — "breakdown" and "scheduled service" are different operational facts |
| `is_active` | boolean | Soft delete |

**AmbulanceCrewAssignment** — one staff member's current or historical assignment to an ambulance.

| Field | Type | Notes |
| :--- | :--- | :--- |
| `id` | uuid, PK | |
| `ambulance_id` | uuid, FK → Ambulance | |
| `staff_member_id` | uuid, FK → StaffMember | Must hold the `ambulance_crew` role |
| `assigned_at` / `unassigned_at` | timestamptz | `unassigned_at` is null while current |
| `assigned_by_staff_id` / `unassigned_by_staff_id` | uuid | Duty Manager stamps; unassigning stamp is nullable |

Constraints: `UNIQUE(staff_member_id) WHERE unassigned_at IS NULL` and
`UNIQUE(ambulance_id, staff_member_id) WHERE unassigned_at IS NULL`. An ambulance may
have several current crew members, but one person may have only one current ambulance.
The ready-crew minimum starts at two and is read from configuration.

**Dispatch** — one confirmed ambulance response to one call.

| Field | Type | Notes |
| :--- | :--- | :--- |
| `id` | uuid, PK | |
| `emergency_call_id` | uuid, FK → EmergencyCall | |
| `ambulance_id` | uuid, FK → Ambulance | |
| `status` | `DispatchStatus` | `assigned` `acknowledged` `en_route_to_scene` `at_scene` `transporting_to_hospital` `handed_over`, plus terminal `declined` `cancelled` `reassigned` |
| `superseded_by_dispatch_id` | uuid, FK → Dispatch, nullable | *Addition* — on a diverted run, points at the dispatch that replaced it, so the chain is followable |
| `dispatched_at` | timestamptz | |
| `completed_at` | timestamptz, nullable | |

Constraint: `UNIQUE(ambulance_id) WHERE status IN ('assigned','acknowledged','en_route_to_scene','at_scene','transporting_to_hospital')` — one ambulance cannot be on two live runs.

**DispatchCrew** — the immutable snapshot of who was present when this dispatch was created.

| Field | Type | Notes |
| :--- | :--- | :--- |
| `dispatch_id` | uuid, FK → Dispatch | |
| `staff_member_id` | uuid, FK → StaffMember | Staff Management's table, ID only — see §7 |

Constraint: `UNIQUE(dispatch_id, staff_member_id)`. Rows are copied from the selected
ambulance's current `AmbulanceCrewAssignment` rows in the same transaction as the
dispatch. Later crew changes never rewrite this history.

**RouteLog** — one summary row per dispatch, not a GPS trail *(Decision 23)*.

| Field | Type | Notes |
| :--- | :--- | :--- |
| `id` | uuid, PK | |
| `dispatch_id` | uuid, unique, FK → Dispatch | 1:1 |
| `origin_latitude` / `origin_longitude` | numeric(9,6) | |
| `destination_latitude` / `destination_longitude` | numeric(9,6) | |
| `planned_distance_km` | numeric(7,2) | From the maps API |
| `planned_duration_minutes` | integer | From the maps API |
| `departed_at` / `arrived_at` | timestamptz, nullable | |
| `maps_api_reference` | text, nullable | The maps provider's own request id, so a disputed ETA can be traced |

### 3.2 Indexes

| Index | Why |
| :--- | :--- |
| `emergency_calls(status, created_at desc)` | The live call board — open calls first |
| `emergency_calls(priority, created_at)` | The board's default sort |
| `emergency_calls(idempotency_key)` **UNIQUE** | Network retries cannot create duplicate calls |
| `ambulances(status)` | "Which ambulances are free right now" |
| `ambulance_crew_assignments(staff_member_id) WHERE unassigned_at IS NULL` **UNIQUE** | One current ambulance per crew member |
| `ambulance_crew_assignments(ambulance_id, staff_member_id) WHERE unassigned_at IS NULL` **UNIQUE** | No duplicate current assignment |
| `dispatches(ambulance_id) WHERE status IN ('assigned','acknowledged','en_route_to_scene','at_scene','transporting_to_hospital')` **UNIQUE** | Enforces one active run per ambulance |
| `dispatches(emergency_call_id)` | Call detail view, and the diversion chain |
| `dispatches(status, dispatched_at desc)` | The fleet board and history search |
| `dispatch_crew(dispatch_id)` | Roster for one run |
| `dispatch_crew(staff_member_id)` | The crew app's "my runs" query |
| `route_logs(dispatch_id)` **UNIQUE** | 1:1 lookup |

### 3.3 Transactions and concurrency

**Assigning an ambulance must not double-book it.** The partial unique index in §3.2 is the actual guarantee — a second dispatch against an already-assigned ambulance is rejected by the database, not merely by an application check that two concurrent confirms could race past.

**Assigning crew must not double-book a person.** The current-assignment index is the
database guarantee. The service also blocks any crew change while that ambulance has a
live dispatch.

**Creating a dispatch is one transaction.** The Duty Manager's command re-checks the call
and ambulance, recomputes eligibility, creates the `Dispatch`, copies every current crew
assignment into `DispatchCrew`, and updates the call and ambulance projections. The same
command is used by manual dispatch and by a confirmed AI proposal.

**A diversion is three writes in one transaction:** the old `Dispatch` moves to `reassigned` with `superseded_by_dispatch_id` set, the new one is created `assigned`, and the original call is re-queued (a fresh proposal, or a replacement dispatch if one was named). Either all of it happens or none does — a half-applied diversion would leave a patient with no ambulance and nobody aware of it.

**Hospital preparation follows dispatch and cannot hold it up.** After the dispatch commits,
Emergency calls Patient Management's `POST /admissions/pre-admit`. Patient Management owns
ward and bed preparation. Failure is recorded for retry and never rolls back or delays the
ambulance.

### 3.4 Seed data

- 6–8 ambulances, mixed `available` / `en_route` / `out_of_service`, with current crew assignments
- 10–15 `EmergencyCall` rows across every `CallStatus`, a mix of `patient_is_caller = true/false`, and at least one still unidentified so the link-patient operation has something to demo
- Completed `Dispatch` + `RouteLog` pairs so the response-time and fleet reports render immediately
- One `reassigned` dispatch with its replacement, so the diversion chain is visible
- One call sitting in `pending_confirmation` and one diversion in `pending_approval`, so both approval screens have something waiting on load

---

## 4. Status workflows

### 4.1 `CallStatus`

```
received ──► dispatched ──► en_route ──► completed
    │              │             │
    └──────────────┴─────────────┴──► cancelled
```

### 4.2 `DispatchStatus`

```
assigned ──► acknowledged ──► en_route_to_scene ──► at_scene
    │                                      │                 │
    └──► declined                         └──► reassigned      └──► transporting_to_hospital ──► handed_over
    └──► cancelled
```

`declined` is legal only from `assigned`. `cancelled` and `reassigned` are legal only
before `at_scene`. A diverted run keeps its own row and gains
`superseded_by_dispatch_id`; the replacement gets a new row.

### 4.3 `AmbulanceStatus` — and the line that matters

Mirrors the crew's Flutter buttons one-to-one:

```
available ──► dispatched ──► en_route ──► at_scene ──► transporting ──► available
                                                                             (on handover)
```

**`at_scene` is the point of no return.** It is an authoritative dispatch state as well
as a fleet projection. From that moment the ambulance cannot be diverted by the agent,
the Duty Manager, or anyone.

`DispatchStatus` is authoritative. `CallStatus` and `AmbulanceStatus` are projections
updated by the same transition service and transaction; neither is used alone to decide
eligibility.

---

## 5. The two human gates

**No agent output in CareLanka is applied without a human, and dispatch is not the exception.** What changes between the two gates is how much reading the human has to do, because the two decisions are genuinely different sizes.

### 5.1 Confirm — the routine case

An eligible ambulance with enough current crew exists. The agent has ranked the options by
real ETA and deterministic validation has passed. A Duty Manager sees one recommendation
with a vehicle, crew readiness, ETA and a one-line reason, and taps **Send**. The same Duty
Manager can bypass the agent and use the manual dispatch endpoint.

That is the whole gate. It costs a couple of seconds, and it buys three things:

- **A person is accountable for every ambulance movement.** `reviewed_by_staff_id` is never null.
- **The dispatcher can override before anything happens** — they may know the crew is mid-handover in a way the data does not show yet.
- **Assignment §9.1 is satisfied cleanly.** Every high-impact action pauses for a human.

**Why not skip it entirely?** An earlier draft of this design did, arguing that seconds matter in an emergency. That was the wrong trade. The time saved is small — a dispatcher watching the board is already looking at the screen — and the thing given up is large: an AI moving emergency vehicles with no person in the loop is the single hardest decision in this project to defend at a viva, and the hardest to justify if it ever got one wrong.

**Why not a full approval form?** Because that overcorrects. A form with a reason field and a review step turns a two-second action into a minute, and dispatchers under load would start batching them, which is worse than either option.

### 5.2 Approve — the diversion case

Every ambulance is committed. A `critical` call comes in and the only way to reach it quickly is to turn around a vehicle currently driving to a lower-priority call.

This is a real trade-off between two patients, so the Duty Manager gets the full picture before deciding — `DiversionImpact` on the proposal spells out:

- which call loses its ambulance, and its priority
- how long that patient has been waiting already
- **how many extra minutes they will now wait**
- which ambulance is proposed to take over their call — or, pointedly, that none is free
- how much sooner the critical call gets reached

**The hard rule, enforced in C# and not left to anyone's judgement:**

> An ambulance may only be diverted in `assigned`, `acknowledged`, or
> `en_route_to_scene`. From `at_scene` onward that vehicle is untouchable.

This is what makes diversion a realistic operation rather than an absurd one. Nobody turns an ambulance around with a patient in the back, and nobody drives away from a patient standing on the pavement waiting. Real ambulance services divert *pre-arrival* units all the time; that is exactly the window this rule allows and no wider.

It is enforced in three places, deliberately:

1. The agent will not propose a diversion against a non-divertible run.
2. `source_dispatch_still_pre_pickup` is re-checked at approval time — so a proposal that has been sitting on screen while the crew arrived is refused, not applied.
3. `POST /dispatches/{id}/divert`, the manual path, applies the same check, so a human cannot do by hand what the agent is forbidden to propose.

**The original call is never abandoned.** Approving a diversion re-queues it in the same transaction. If nothing is free to take it, that is stated on the impact block before the decision, not discovered afterwards.

---

## 6. The Dispatch & Routing Agent

Follows the same gather → filter → rank → propose → validate → human gate → execute template `patient-management-plan.md` §8.7 sets out for the bed agent.

**Objective:** *"recommend the best eligible ambulance and explain why."*

**Tools — read-only, allow-listed, no write access to any table:**

| Tool | Reads |
| :--- | :--- |
| `list_eligible_ambulances` | Our deterministic eligibility service: active, serviceable, enough current crew, no live dispatch, recent location |
| `get_active_dispatches` | Our `Dispatch` table — needed to spot divertible pre-pickup runs |
| `get_route` | The maps API — real distance and duration to the scene |

**Plan:**

1. Read the call: location, priority, and whether the patient is already known.
2. Ask deterministic code for eligible ambulances and explicit block reasons.
3. Rank them by **real driving ETA through the maps API**, not straight-line distance — the nearest vehicle by map is regularly not the nearest by road, and this is the single place the third-party integration earns its keep.
4. **If something is free** → propose it, `pending_confirmation`. One tap sends it (§5.1).
5. **If nothing is free** → look at active dispatches for a pre-pickup run on a lower-priority call, work out the cost to that patient, and propose a diversion with a populated `DiversionImpact`, `pending_approval` (§5.2).
6. **If neither** → stop. Outcome `no_ambulance_available`, workflow `failed`, recorded honestly. No retry loop.
7. Once the Duty Manager confirms and a `Dispatch` exists, call Patient Management's
   `POST /admissions/pre-admit` so its ward/bed workflow can start before arrival. This
   integration is not part of the dispatch transaction and failure never stops the run.

**Translating priority on the way out.** Our `CallPriority` and Patient Management's `AdmissionUrgency` are different vocabularies for different jobs — ours ranks *how fast an ambulance is needed*, theirs ranks *how fast a bed is needed* — so the two enums were never going to be the same words. Step 8 sends theirs, not ours, using a fixed table in C#:

| Our `CallPriority` | We send `urgency` |
| :--- | :--- |
| `critical` | `emergency` |
| `high` | `urgent` |
| `medium` | `routine` |
| `low` | `routine` |

Deliberately lossy in one direction — `medium` and `low` both mean "no rush" once the
question is which bed. **The agent never chooses this value**; it is a fixed lookup. The
same table is written in `emergency-spec.yaml`'s `DispatchNotification`,
`integration_of_functions.md` §22, and `patient-spec.yaml`'s `PreAdmitRequest`.

**Deterministic validation — ordinary C#, never the model checking itself.** Run before a proposal reaches a human, and **re-run immediately before applying**, because the road situation moves while a proposal sits on screen:

| Check | Catches |
| :--- | :--- |
| `ambulance_still_eligible` | Vehicle became inactive, unserviceable, busy, insufficiently crewed, or stale/missing location |
| `source_dispatch_still_pre_pickup` | **The diversion rule** — the crew arrived while the approval waited |
| `source_call_has_replacement` | Diverting away from a patient with nothing to take their call |

**Safe failure:** `no_ambulance_available` is a recorded outcome with a reason, not a silent retry — assignment §9.1's "safe, clearly recorded failure", and the same principle as the bed agent's `no_bed_available`.

**What the agent never does:** set `CallPriority`, diagnose, choose a ward or bed, write a
dispatch, or move any vehicle. It recommends and explains; deterministic code validates;
a Duty Manager confirms. AI, maps, push, and Patient Management failures leave manual
dispatch available.

---

## 7. Connections to the other components

Full detail is in `integration_of_functions.md` §22–§26, written to agree with §4, which Patient Management wrote first.

| With | What crosses | Direction |
| :--- | :--- | :--- |
| **Patient Management (M4)** | Dispatch notification triggering `POST /admissions/pre-admit` | We notify after dispatch; they own patient, ward and bed preparation |
| **Staff Management (M2)** | Staff IDs on `AmbulanceCrewAssignment` and `DispatchCrew`, resolved through `POST /staff/lookup` | We store IDs; they own the person and role |
| **Equipment Management (M3)** | Nothing. Ambulances carry no tracked equipment (`docs/entity_diagram.md` Decisions 9 & 14) | No boundary |

**One writer per table, no exceptions.** Only our code writes `EmergencyCall`,
`Ambulance`, `AmbulanceCrewAssignment`, `Dispatch`, `DispatchCrew`, and `RouteLog`. We
never write `Admission`, `Ward`, `Bed`, or `StaffMember`.

---

## 8. API surface

Full contract is in `emergency-spec.yaml`. These paths are settled; they are not
provisional implementation suggestions.

| Area | Endpoints |
| :--- | :--- |
| **Calls** | `POST /emergency-calls`, `GET /emergency-calls`, `GET/PATCH /emergency-calls/{id}`, `POST /emergency-calls/{id}/link-patient`, `POST /emergency-calls/{id}/outcome`, `POST /emergency-calls/{id}/cancel`, `POST /emergency-calls/{id}/dispatch` |
| **Ambulances** | `GET/POST /ambulances`, `GET/PATCH /ambulances/{id}`, retire/reinstate/location/history operations, plus `GET/POST /ambulances/{id}/crew` and `DELETE /ambulances/{ambulanceId}/crew/{staffMemberId}` for current crew |
| **Dispatch Agent** | `GET /dispatch-proposals`, `POST /dispatch-proposals`, `GET /dispatch-proposals/{id}`, `POST /dispatch-proposals/{id}/confirm`, `POST /dispatch-proposals/{id}/approve`, `POST /dispatch-proposals/{id}/reject` |
| **Dispatches** | `GET /dispatches`, `GET /dispatches/{id}`, divert/cancel/route operations, and read-only `GET /dispatches/{id}/crew` snapshot |
| **My Run** (Flutter, crew) | active/history, `POST /me/dispatches/{id}/acknowledge`, `POST /me/dispatches/{id}/decline`, status progress, Google Maps navigation target, and handover |
| **My Calls** (patient APIs; screen owned by M4) | own-call list/tracking, direct pre-dispatch cancel, post-assignment cancellation request |
| **Cancellation review** | Duty Manager list plus approve/reject operations under `/emergency-cancellation-requests` |
| **Reports** | `GET /reports/emergency/response-times`, `GET /reports/emergency/fleet-utilisation`, `GET /reports/emergency/agent-performance` |

**The business operations beyond CRUD**, of which there are four:

1. **`POST /emergency-calls/{id}/dispatch`** — the manual command that validates the
   response unit and snapshots its current crew; confirmed proposals call the same service.
2. **`POST /dispatches/{id}/divert`** — turns a run around under the pre-pickup rule, ends one dispatch, creates another and re-queues a call, all in one transaction.
3. **`POST /emergency-calls/{id}/link-patient`** — stitches a call raised for an unidentified person to the real patient record once somebody identifies them.
4. **`POST /dispatch-proposals/{id}/approve`** — applies a diversion with re-validation under the same rule.

Route and schema names were checked against the other three specs; report routes are namespaced `/reports/emergency/...` and the agent report schema is `EmergencyAgentPerformanceReport`, so this component adds nothing to the collision list in `integration_of_functions.md` §11.6.

---

## 9. React screens — the Duty Manager's desk

React is where the decisions get made (`docs/CareLanka_Component_Plan.md` §2.1, *"React decides"*).

| Screen | What it does |
| :--- | :--- |
| **Live call board** | Every open call, priority then waiting time. Set/adjust priority, open a call, see which are still unassigned |
| **Dispatch queue** | Both gates in one list: one-tap **Send** on routine proposals, **Review** on diversions. Shows the agent's ranking, ETA and reasoning |
| **Diversion review** | The full `DiversionImpact` — who loses their ambulance, extra wait, replacement — with approve / reject and a required rejection reason |
| **Fleet map & board** | Live ambulance positions, active routes, status per vehicle, `is_divertible` at a glance |
| **Ambulance register** | Full CRUD: add, edit, retire, reinstate, plus per-vehicle dispatch history |
| **Crew assignment** | Manage current ambulance crew; changes are blocked during a live run |
| **Cancellation review** | Review post-assignment patient requests and approve or reject with an explanation |
| **Reports** | Response times by priority, fleet utilisation, agent performance |

Covers what assignment §7 asks of the React app: CRUD interfaces, validation, search, filters, sorting, pagination, dashboards, and agent workflow monitoring with approve/reject controls.

## 10. Flutter screens — two very different users

Flutter is where the work gets done, and this component has **two distinct Flutter audiences**, which is unusual and worth pointing at during the demo.

**Ambulance Crew — operational:**

| Screen | What it does |
| :--- | :--- |
| **My run** | The dispatch that just arrived: patient location, address, priority, and current state |
| **Navigate** | Launch Google Maps to the scene, then to CareLanka Hospital's configured emergency entrance |
| **Status buttons** | Acknowledge or decline, then en route to scene → at scene → transporting to hospital → handed over |
| **Handover** | Condition on arrival, notes, and any identity details a relative gave at the scene |
| **My history** | Past runs, paged |

**Patient — narrow self-service, with screens owned by Patient Management:**

| Screen | What it does |
| :--- | :--- |
| **I need an ambulance** | The one-question call screen. Screen built by Patient Management, posts to our endpoint (`integration_of_functions.md` §4.1) |
| **Track my ambulance** | Where it is and how many minutes away. Deliberately tiny — no crew names, no notes, no other calls |
| **My calls** | Calls this person raised, including ones raised for somebody else |
| **Cancel / request cancellation** | Cancel directly before assignment; after assignment ask a Duty Manager to review |

The split satisfies assignment §4.1's "meaningful and different purposes": React reviews and approves, Flutter does the driving and the waiting.

**A call that arrives by phone needs nothing extra from us.** Not every caller has the app
— the stranger who finds someone collapsed usually does not — so Patient Management puts
a `tel:` link to the hospital's emergency number beside their "I need an ambulance" button
(`patient-management-plan.md` §10.1). When it rings, the person at the desk fills in the
same `POST /emergency-calls` they already have on the React call board, with
`caller_user_id` null and `caller_name` / `caller_phone` written down as they are spoken.
The agent then runs identically — it never knew or cared how the call arrived.

**We are not building a phone system.** No IVR, no call queue, no recording, no telephony
integration, no entity of our own for a phone call. The number lives in configuration.
A hospital phone line answered by a human who then uses a screen is not a feature to
implement, and treating it as one would cost us the diversion gate we would rather spend
the time on.

---

## 11. Device feature and third-party integration

**GPS + maps** does real work in three places:

1. **Ranking** — the agent orders candidate ambulances by driving ETA, not straight-line distance (§6 step 3)
2. **Driving handoff** — `GET /me/dispatches/{id}/navigation` returns the next destination and a Google Maps launch URL; Flutter launches Google Maps rather than recreating navigation
3. **Reverse geocoding** — `address_label` turns coordinates into a street name the crew can read
4. **The record** — `RouteLog` stores what the API returned, with `maps_api_reference` so an ETA can be traced back to the request that produced it

This is the assignment's one required third-party integration (§11) for the **whole system** — no other component needs one. The GPS/map device feature (§8) is covered by the same work.

**Degradation matters:** the navigation target needs no live route request. If backend
route/ETA calculation fails, Emergency uses straight-line ordering, labels the fallback,
and retains raw destination coordinates. A maps outage never blocks manual dispatch or
the Google Maps launch target.

---

## 12. Testing

- **Unit:** eligibility and configured crew minimum; ETA ranking; the diversion cost calculation; every legal and illegal `DispatchStatus` move; the safe-failure path when nothing is available
- **Integration:** the partial unique index actually rejects a second confirm against the same ambulance under concurrent requests; a diversion approved after the crew reached the scene is refused; approving a diversion re-queues the original call in the same transaction; the pre-admission call fires once per dispatch, not once per retry
- **Contract:** `openapi-spec-validator` against `emergency-spec.yaml`, plus the cross-spec route/operationId/schema uniqueness check CI runs for all four

---

## 13. Decisions and why

| Decision | Why |
| :--- | :--- |
| **Every dispatch passes a human**, with a light gate for routine sends | An AI moving emergency vehicles unsupervised is indefensible at a viva, and the one-tap gate costs seconds. Replaces this document's v0.1 position, which had no gate on the routine path |
| **Two gates, not one** | A one-tap send and a two-patient trade-off are different decisions; one form for both would either be too slow for the first or too thin for the second |
| **Diversion only before pickup** | Anything else is fiction — nobody turns an ambulance around with a patient aboard. Enforced in C# in three places, not left to judgement |
| **The original call is re-queued, never dropped** | A diversion that silently abandons a patient is the failure mode worth designing against |
| **One hospital emergency entrance** | Emergency owns route/ETA to one configured destination; Patient Management owns ward and bed preparation |
| **The dispatcher sets `CallPriority`, not the agent** | Triage is clinical judgement; the project is explicitly not a diagnosis system |
| A diverted run keeps its own row + `superseded_by_dispatch_id` | Overwriting would destroy the audit trail the whole diversion feature depends on |
| `EmergencyCall` gains `caller_user_id` / `patient_is_caller` (Rev 2.4) | Closes group open item `integration_of_functions.md` §11.4, unanswerable until this component existed |
| No onboard equipment tracking | Already settled in `docs/entity_diagram.md` (Decisions 9, 14) |
| Current crew in `AmbulanceCrewAssignment`; responders in `DispatchCrew` | Current responsibility can change; dispatch history must not |
| Ready crew minimum starts at two and is configurable | The safety threshold is explicit without burying a permanent constant in query code |
| Patient cancellation becomes review after assignment | A caller may withdraw an unassigned request directly but cannot recall a moving response unit |

---

## 14. External decisions

No external group decision blocks Phase 1. Current crew ownership, the Duty Manager gate,
the one-hospital destination, authoritative dispatch status, and the patient-screen boundary
are settled for this release. The later Coordinator Agent may take over orchestration of the
same published operations without changing the Emergency domain contract.

---

## 15. Checked against the assignment

| Requirement | Where it is met |
| :--- | :--- |
| ≥4 meaningful endpoints + ≥1 business operation beyond CRUD (§5) | §8 — 33 paths, four non-CRUD operations |
| Search, filtering, sorting, pagination, history (§5) | §8 — on calls, ambulances, dispatches and both `/me` history lists |
| React: CRUD, dashboards, agent monitoring, approve/reject (§7) | §9 |
| Flutter: responsive screens, device feature (§8) | §10, §11 — two distinct audiences |
| A meaningful third-party integration (§11) | §11 — maps, doing real work in four places |
| Human approval before high-impact actions (§9.1) | §5 — both gates |
| A distinct agent with allow-listed tools and safe failure (§9) | §6 |
| Not a medical diagnosis system | §1, §6 — the agent sets neither `CallPriority` nor `admission_category` |
