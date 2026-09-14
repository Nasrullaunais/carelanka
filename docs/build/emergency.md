# Build Track 1 — Emergency / Ambulance

**Owner:** Nasrulla Unais (Member 1)

**Status:** Phases 0–2 complete 2026-09-14; Phase 3 is next

**Contract:** `specs/emergency-spec.yaml`

**Design:** `specs/emergency-management-plan.md`

**Boundaries:** `specs/integration_of_functions.md` §22–§26

**Fresh-agent prompts:** `docs/build/emergency-agent-prompts.md`

This is the implementation plan for the Emergency / Ambulance component. It is
ordered as a series of usable vertical slices: establish the safety rules, make a
manual dispatch work end to end, add reliable maps and notifications, and only then
place the AI recommendation workflow over the proven dispatch operation.

Phase 0 aligned the management plan, OpenAPI contract, integration boundaries, entity
diagram and component plan to the decisions recorded here. `emergency-spec.yaml` is now
the concrete contract for the later implementation phases.

---

## 1. Agreed vision

A patient can report an emergency using one short text field. The mobile app captures
the scene location automatically and lets the caller correct it. The system finds the
best eligible ambulance by road travel time. A dispatcher confirms the recommendation,
the assigned crew acknowledges it, and the crew progresses the dispatch through arrival,
transport and hospital handover. The caller can track only the operational information
needed while waiting.

The dispatchable resource is an **ambulance with a ready crew**, not an individual
paramedic. One crew member can be assigned to only one ambulance at a time. Every
dispatch permanently records the crew who responded, even when the ambulance's current
crew later changes.

The normal path is:

```text
Patient reports emergency
        ↓
Call and corrected GPS location are recorded
        ↓
Eligible ambulances are ranked by driving ETA
        ↓
AI explains a recommendation
        ↓
Duty Manager confirms with one tap
        ↓
Current ambulance crew is recorded on the dispatch
        ↓
Crew receives and acknowledges the dispatch
        ↓
en route → at scene → transporting → handed over
        ↓
Ambulance and crew become available again
```

The system must still support the same flow manually when the AI or maps provider is
unavailable.

---

## 2. Invariants

These are hard rules enforced by ordinary C# and database constraints. They are not
left to an AI prompt or a screen convention.

1. A live dispatch has exactly one ambulance.
2. One ambulance cannot have two live dispatches.
3. One crew member cannot have two current ambulance assignments.
4. An ambulance is eligible only when it is active, serviceable, sufficiently crewed,
   free of another live dispatch, and has a usable recent location.
5. A dispatch snapshots the ambulance's current crew when it is created. Later crew
   changes never alter that dispatch history.
6. Current crew assignments cannot change while their ambulance has a live dispatch.
7. Only staff with the `ambulance_crew` role may be assigned to an ambulance.
8. Only a `duty_manager` can manage current ambulance crew assignments or confirm a
   routine dispatch.
9. A crew member can read and update only a dispatch on which they appear in
   `DispatchCrew`; identity always comes from the JWT.
10. One responding crew member may acknowledge or decline on behalf of the ambulance;
    the action applies to the response unit, not only that person's copy of the alert.
11. Once a crew reaches `at_scene`, that ambulance can never be diverted.
12. A patient is never told an ambulance is coming until a dispatch is confirmed.
13. Maps, AI and push-notification failures never prevent a manual dispatch.
14. Only the latest operational ambulance location is retained; this component does
    not build a permanent GPS trail.
15. A dispatch completes at hospital handover, not merely when the vehicle reaches the
    hospital grounds.

---

## 3. Target domain model

### 3.1 Existing entities retained

- `EmergencyCall` — a request for emergency assistance and its scene location.
- `Ambulance` — the registered response vehicle and its latest known position.
- `Dispatch` — one ambulance's response to one emergency call.
- `DispatchCrew` — the permanent record of who responded on that dispatch.
- `RouteLog` — the planned-route summary used for audit and fallback.

### 3.2 New `AmbulanceCrewAssignment`

This entity represents who is currently responsible for an ambulance:

| Field | Purpose |
| :--- | :--- |
| `id` | Assignment identifier |
| `ambulance_id` | The ambulance currently crewed |
| `staff_member_id` | Staff Management's crew-member identifier |
| `assigned_at` | When the current duty assignment began |
| `unassigned_at` | Null while current; set when the assignment ends |
| `assigned_by_staff_id` | Duty Manager responsible for the assignment |
| `unassigned_by_staff_id` | Duty Manager responsible for ending it, nullable |

Required constraints:

```text
UNIQUE(staff_member_id) WHERE unassigned_at IS NULL
UNIQUE(ambulance_id, staff_member_id) WHERE unassigned_at IS NULL
```

An ambulance may have several current crew members. A crew member may have only one
current ambulance. Historical rows are ended, not deleted.

The minimum ready crew size starts at `2` and lives in configuration. Eligibility reads
the configured value rather than embedding `2` in queries and controllers.

### 3.3 Responding crew snapshot

When a dispatch is created, every current `AmbulanceCrewAssignment` for the selected
ambulance is copied as a `DispatchCrew` relationship in the same transaction. Only the
staff IDs are copied; staff names and profiles remain owned by Staff Management.

### 3.4 Authoritative dispatch state

`DispatchStatus` is the authoritative journey state:

```text
assigned
  → acknowledged
  → en_route_to_scene
  → at_scene
  → transporting_to_hospital
  → handed_over
```

Terminal alternatives are:

```text
declined
cancelled
reassigned
```

The active-ambulance unique index must cover every non-terminal state:

```text
assigned, acknowledged, en_route_to_scene, at_scene,
transporting_to_hospital
```

`AmbulanceStatus` remains a useful fleet-board projection, but dispatch eligibility is
calculated from the invariant inputs rather than trusting that value alone. Call and
ambulance status changes are made by one transition service in the same transaction as
the dispatch change.

| Dispatch state | Ambulance projection | Patient wording |
| :--- | :--- | :--- |
| `assigned` | `dispatched` | Ambulance assigned |
| `acknowledged` | `dispatched` | Crew acknowledged |
| `en_route_to_scene` | `en_route` | Ambulance is on the way |
| `at_scene` | `at_scene` | Paramedics are assisting the patient |
| `transporting_to_hospital` | `transporting` | Travelling to CareLanka Hospital |
| `handed_over` | `available` | Patient handed over at hospital |

The component does not record diagnosis or treatment. `at_scene` is an operational
milestone, not a clinical treatment record.

---

## 4. Phased implementation

### Phase 0 — Align the design and contract — **COMPLETE 2026-09-13**

**Goal:** remove contradictions before creating another migration or public endpoint.

Work:

- Update `emergency-management-plan.md`, `emergency-spec.yaml`,
  `integration_of_functions.md`, `entity_diagram.md`, and the Emergency row in the
  component plan together.
- Replace “nearest paramedic” with “best eligible ambulance and current crew.”
- Add `AmbulanceCrewAssignment` to Emergency's ownership list and diagram.
- Replace the old dispatch states with the authoritative state machine in §3.4.
- Add crew acknowledgement and decline operations.
- Add a manual dispatch operation that later proposal confirmation can call.
- Change navigation from an in-app turn-by-turn promise to a Google Maps launch target,
  while keeping backend route/ETA calculation.
- Specify patient cancellation request behaviour after a dispatch is active.
- Specify the one-hospital assumption: routing targets CareLanka Hospital's emergency
  entrance; Patient Management separately prepares a ward/bed.
- Preserve the published UI ownership boundary: Emergency owns the call endpoint and
  downstream workflow; Patient Management owns the patient-facing report screen unless
  the group deliberately changes that boundary in all owning documents.
- Re-run `bun run check:specs` and update OpenAPI contract tests.

Settled Phase 0 contract operations:

```text
GET    /ambulances/{id}/crew
POST   /ambulances/{id}/crew
DELETE /ambulances/{ambulanceId}/crew/{staffMemberId}
POST   /emergency-calls/{id}/dispatch
POST   /me/dispatches/{id}/acknowledge
POST   /me/dispatches/{id}/decline
POST   /me/emergency-calls/{id}/cancel
POST   /me/emergency-calls/{id}/cancellation-request
GET    /emergency-cancellation-requests
POST   /emergency-calls/{id}/cancellation-request/approve
POST   /emergency-calls/{id}/cancellation-request/reject
```

`GET /dispatches/{id}/crew` remains the read-only responding-crew snapshot; its former
POST/DELETE mutations are removed. `GET /me/dispatches/{id}/navigation` now returns a
`NavigationTarget` for launching Google Maps, not directions, steps or a polyline.
The manual dispatch and proposal-confirm operations are Duty-Manager-only. Proposal
confirmation delegates to the same application service rather than implementing dispatch
twice.

**Exit criteria met:** all owning documents use the same entities, roles, statuses and
routes; all five OpenAPI specs validate without global collisions; Emergency's static
contract tests pin the settled operations, dispatch enum, immutable crew snapshot and
Google Maps launch target.

### Phase 1 — Current crew and dispatch eligibility — **COMPLETE 2026-09-13**

**Goal:** answer truthfully, before a call arrives, which ambulances can respond.

Backend work:

- Add `AmbulanceCrewAssignment`, EF configuration and migration.
- Add the two partial unique indexes from §3.2.
- Add `IAmbulanceCrewService` with assign, unassign and list-current operations.
- Use Staff Management's lookup contract to verify the `ambulance_crew` role; keep a
  documented service stub only while that dependency is unavailable.
- Add an `IAmbulanceEligibilityService` returning eligibility plus explicit block
  reasons: inactive, out of service, insufficient crew, busy, missing location, or
  stale location.
- Block crew changes while any non-terminal dispatch exists.
- Add Duty Manager endpoints and contract/integration tests.

Tests:

- A crew member cannot be current on two ambulances.
- Two concurrent assignments cannot bypass the unique index.
- Non-crew staff are rejected.
- An ambulance with fewer than the configured minimum crew is ineligible.
- Crew changes are rejected during a live dispatch.
- Ended assignments remain readable as history but no longer count toward readiness.

**Exit criteria met:** the fleet list explains why every ambulance is eligible or blocked,
and PostgreSQL rejects concurrent current assignments through the two partial unique indexes.

### Phase 2 — Patient emergency intake — **COMPLETE 2026-09-14**

**Goal:** persist a safe, simple emergency request without depending on AI or Maps.

Patient Management mobile experience (a dependency, not work owned by this track):

- One “What happened?” text field.
- Ask “Is the emergency for you?” and support the bystander case.
- Capture GPS automatically and show a correctable map pin.
- Allow manual pin placement when permission is denied or GPS is unavailable.
- Show location accuracy and require the caller to confirm a poor reading.
- Prevent repeat taps while submitting.

Backend work:

- Implement `POST /emergency-calls` with coordinates, optional details and
  `patient_is_caller`.
- Read `caller_user_id` from the JWT, never the request.
- Default a patient-submitted call to `high`; only a Duty Manager may change priority.
- Store the published `location_accuracy_metres`, `location_captured_at`, and
  `idempotency_key` fields; repeat submissions with the same caller/key return the
  original call.
- Implement own-call listing with strict caller scoping.
- Add the React call board's list/detail/update endpoints.
- Publish and integration-test the generated contract Patient Management consumes for
  its report screen.

Tests cover invalid coordinates, caller identity, bystander reporting, duplicate
submission, role restrictions and cross-patient access.

**Exit criteria met:** one correctly scoped patient submission is stored once under its
JWT caller and idempotency key, and the Duty Manager sees the received call through the
tested call-board API. Patient Management's screen handoff is documented in
`patient-emergency-intake.md`; the screen remains M4's integration-checkpoint work.

### Phase 3 — Manual dispatch and the state machine

**Goal:** make the complete backend workflow work without AI.

Backend work:

- Implement the central `DispatchService` command used by both manual dispatch and
  future proposal confirmation.
- Re-check eligibility inside the dispatch transaction.
- Create `Dispatch` and `DispatchCrew` snapshot rows atomically.
- Expand the active-ambulance unique index to every live state.
- Implement acknowledgement, decline and legal status transitions.
- A decline reopens the call for dispatcher action and records its reason.
- Raise an unacknowledged alert after 30 seconds; do not automatically double-dispatch.
- Implement cancellation and reassignment before `at_scene`.
- Reject every diversion or reassignment from `at_scene` onward.
- Make completion occur only through handover.

Concurrency tests must attempt two dispatches against the same ambulance and two
confirmations against the same call. Database constraints, not timing assumptions, must
decide the winner.

**Exit criteria:** a Duty Manager can manually dispatch an eligible ambulance and the
assigned crew can take it through handover using API tests.

### Phase 4 — Dispatcher React vertical slice

**Goal:** operate the manual workflow without Swagger or direct database access.

Screens:

- Live call board ordered by priority and waiting time.
- Call detail with location, caller report and priority adjustment.
- Eligible-ambulance list with ETA when available and block reasons when not.
- One-tap dispatch confirmation.
- Acknowledgement timer and unacknowledged warning.
- Fleet board with crew, latest location and operational state.
- Crew assignment screen restricted to Duty Managers.

Use only the generated API client. Every mutation has loading, failure and conflict
handling; a `409` caused by another dispatcher refreshes the affected call and fleet row.

**Exit criteria:** the Duty Manager can crew an ambulance and dispatch it from React.

### Phase 5 — Crew Flutter vertical slice

**Goal:** let the assigned crew complete a run from their phone.

Screens and behaviour:

- “My run” reads the dispatch scoped from the JWT.
- Poll for an active dispatch while the app is open; this remains the fallback after
  push notification is added.
- Acknowledge or decline with a short operational reason.
- Status buttons appear only for the next legal transition.
- “Open in Google Maps” launches scene coordinates while travelling outward and the
  configured CareLanka Hospital emergency entrance while transporting.
- Handover captures concise notes, condition on arrival and optional identity details.
- History shows the immutable responding crew snapshot.

**Exit criteria:** a crew member can acknowledge, navigate, progress and hand over; a
different crew member receives `403` for the same dispatch.

### Phase 6 — Live location and patient tracking

**Goal:** answer the waiting caller's two questions: “Is it coming?” and “How long?”

Work:

- Report the phone's location approximately every 10–15 seconds during a live run.
- Update only the ambulance's latest position and timestamp.
- Stop updates at cancellation, reassignment or handover.
- Treat stale location honestly; show “last updated” rather than presenting it as live.
- Implement the caller-scoped tracking endpoint.
- Show only progress, ambulance position, ETA, update time and cancellation request.
- Permit direct patient cancellation only before dispatch. Once assigned, create a
  request for Duty Manager confirmation.
- Never return crew identities, incident notes, AI reasoning or another patient's data.

**Exit criteria:** the caller sees the correct ambulance and status, and tracking closes
cleanly after handover.

### Phase 7 — Real Maps integration and graceful fallback

**Goal:** rank by road travel time without making dispatch depend on Google.

Work:

- Keep `IAmbulanceDistanceService` as the provider seam and replace the current
  straight-line stub with the configured Google routing provider.
- Reverse-geocode the scene for a readable address when possible.
- Store planned distance, duration and provider reference in `RouteLog`.
- Return a Google Maps launch URL or destination coordinates to Flutter; do not attempt
  to recreate Google's driver navigation UI.
- On provider failure, rank by straight-line distance, label the fallback in the UI and
  workflow record, and continue allowing manual dispatch.
- Protect API keys in server configuration and never ship a server key in Flutter.

Tests use a fake provider for success, timeout, malformed response, quota failure and
fallback ordering.

**Exit criteria:** normal ranking uses driving ETA; provider failure remains dispatchable.

### Phase 8 — Push notification reliability

**Goal:** deliver assignments while the crew app is backgrounded or closed.

Work:

- Use the Common-owned `DeviceToken` and `Notification` facilities rather than creating
  Emergency-owned duplicates.
- Send Firebase push after the dispatch transaction commits.
- Make delivery retryable and idempotent.
- Keep active-dispatch polling as a recovery path for missed or delayed pushes.
- Opening the notification routes only the assigned crew to “My run.”
- Do not include sensitive incident text in the lock-screen payload.

**Exit criteria:** a backgrounded assigned device receives the alert, and a missed push
is recovered through polling.

### Phase 9 — Hospital preparation and handover integration

**Goal:** prepare CareLanka Hospital without making the ambulance wait on bed selection.

Work:

- Treat the hospital emergency entrance as the transport navigation destination.
- After confirmed dispatch, call Patient Management's `POST /admissions/pre-admit`
  idempotently.
- Translate Emergency priority to Patient urgency using the agreed fixed mapping.
- Send no ward or bed choice; Patient Management owns preparation and clinical placement.
- A Patient Management outage records a retryable integration failure and does not undo
  the ambulance dispatch.

**Exit criteria:** one pre-admission is created per dispatch and handover remains possible
when Patient Management is temporarily unavailable.

### Phase 10 — Dispatch & Routing AI agent

**Goal:** add explained decision support over the proven manual workflow.

The agent may read only allow-listed facts:

- the emergency call
- eligible ambulances and explicit block reasons
- Maps travel times
- active pre-arrival dispatches for possible diversion

Ordinary code first filters invalid candidates. Maps calculates travel facts. The agent
ranks and explains the operational recommendation. It never diagnoses, sets priority,
changes status, writes a dispatch, or overrides an invariant.

Routine proposal:

```text
pending → pending_confirmation → executed or rejected
```

Diversion proposal:

```text
pending → pending_approval → approved/rejected → executed/failed
```

Confirmation and approval re-run all deterministic validations and then call the same
`DispatchService` used by the manual path. If AI execution fails, the Duty Manager keeps
the manual controls.

Diversion is considered only when no ambulance is free, the new call is more urgent, the
source ambulance has not reached `at_scene`, and the displaced call has a visible recovery
plan. A Duty Manager sees the delay imposed on the other patient before approving.

**Exit criteria:** agent recommendations are explainable, validated, human-confirmed,
audited and safely replaceable by manual dispatch.

### Phase 11 — Reports, history and production hardening

**Goal:** finish the non-critical breadth after the operational path is reliable.

Work:

- Response-time report by priority.
- Fleet utilisation report.
- Agent acceptance, rejection and fallback report.
- Ambulance dispatch and crew-assignment histories.
- Retry and reconciliation jobs for notifications and pre-admissions.
- Audit coverage for crew changes, dispatch decisions, cancellations and handovers.
- Rate limits and abuse controls on patient call creation.
- Accessibility, poor-network, permission-denied and app-resume testing.
- Full generated-client drift checks and end-to-end demonstration fixtures.

**Exit criteria:** reports agree with dispatch history and the complete demo survives Maps,
AI, notification and Patient Management failures.

---

## 5. Delivery checkpoints

| Checkpoint | Demonstrable outcome | Phases |
| :--- | :--- | :--- |
| A — truthful fleet | Manager assigns crew; system explains eligibility | 0–1 |
| B — manual backend | Patient call reaches handover through tested API operations | 2–3 |
| C — usable applications | Dispatcher and crew complete the same path in React/Flutter | 4–5 |
| D — visible response | Caller tracks the ambulance; real Maps routes with fallback | 6–7 |
| E — reliable operations | Closed app receives dispatch; hospital is prepared | 8–9 |
| F — agentic workflow | AI recommends; human confirms; manual path remains available | 10 |
| G — submission ready | Reports, audit, failure demonstrations and final tests | 11 |

Do not begin a later checkpoint to hide a broken earlier one. In particular, do not build
the agent until Checkpoint D works manually.

---

## 6. Dependencies and ownership

| Dependency | Owner | Emergency's treatment |
| :--- | :--- | :--- |
| Auth, JWT, workflows, notifications, device tokens | Common | Consume shared contracts; never duplicate |
| Staff role lookup | Staff Management | Store IDs only; stub at service boundary until live |
| Pre-admission and ward/bed preparation | Patient Management | Notify after dispatch; never write their tables or block dispatch on failure |
| Google routing/geocoding | Third party | Provider interface, fake tests and fallback |

Emergency owns `EmergencyCall`, `Ambulance`, `AmbulanceCrewAssignment`, `Dispatch`,
`DispatchCrew`, and `RouteLog`. Phase 0 records this six-entity ownership list in every
owning document.

---

## 7. Verification on every phase

Run the smallest relevant tests while developing, then the full gates before handoff:

```text
dotnet test
bun run check:specs
cd web-ui && bun run check:codegen
web-ui typecheck/test/build commands published by its package scripts
Flutter analyze/test commands published by mobile-ui
```

Every backend endpoint must publish all success and error responses in generated OpenAPI.
Regenerate clients in the same change as an API modification. Never hand-write CareLanka
HTTP clients in React or Flutter.

---

## 8. Explicitly out of scope

- Diagnosis, triage automation or treatment plans
- Medication and equipment carried inside ambulances
- Multiple hospitals or choosing between hospitals
- Permanent GPS breadcrumb history
- Building a replacement for Google Maps navigation
- Automatic dispatch without human confirmation
- Diverting a crew that has reached its patient
- Telephony, call recording or an emergency call centre system
- Exposing crew identity or clinical notes to the reporting patient

---

## 9. Current implementation baseline

Already present:

- The original five Emergency entities, configurations and foundation migration.
- Ambulance CRUD/list/detail/retire/reinstate backend endpoints.
- Current crew assignment/list/unassignment endpoints, history-preserving assignment rows and
  database-enforced current-assignment uniqueness.
- Fleet eligibility decisions with configured crew minimum and explicit block reasons.
- Repository-wide FluentValidation MVC integration; Emergency ambulance requests and query
  validation use separate validators.
- A straight-line `IAmbulanceDistanceService` stub.
- A documented `IStaffLookupService` stub until Staff Management publishes its implementation.
- Emergency OpenAPI and ambulance endpoint tests.
- Emergency-call create, caller-scoped list, Duty Manager board/detail/update endpoints,
  generated web client, and Patient Management's intake handoff example.

Not yet present:

- Manual or agent-assisted dispatch.
- Crew acknowledgement and progress transitions.
- Emergency React or Flutter screens.
- Real Maps, live tracking, Firebase delivery, pre-admission or reports.

With call intake now truthful and caller-scoped, Phase 3 manual dispatch is next rather
than the AI agent or mobile UI.
