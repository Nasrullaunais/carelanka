# Emergency Phase Prompts

These prompts hand exactly one Emergency implementation phase to a fresh agent. Run them
in order. Paste one prompt into a new agent only after the preceding phase meets its exit
criteria.

Every prompt treats `docs/build/emergency.md` as the phase checklist, `CONTEXT.md` as the
domain language, and the repository as the source of truth for commands and current state.
The agent must preserve unrelated work already in the worktree and must not implement a
later phase early.

---

## Phase 0 — Align the design and contract

```text
Work in the CareLanka repository and complete Phase 0, “Align the design and contract,”
from docs/build/emergency.md. This phase changes documentation, the hand-written Emergency
OpenAPI contract, and contract tests only. Do not implement entities, migrations, services,
controllers, or UI yet.

Before editing, read CLAUDE.md, CONTEXT.md, docs/build/emergency.md in full,
specs/emergency-management-plan.md, specs/emergency-spec.yaml, the Emergency sections of
specs/integration_of_functions.md and docs/entity_diagram.md, and Emergency's rows/flow in
docs/CareLanka_Component_Plan.md. Inspect git status and preserve all unrelated changes.

Make every owning document agree with the accepted target in docs/build/emergency.md:
- the dispatchable resource is an eligible ambulance with its current crew;
- Emergency owns the new AmbulanceCrewAssignment entity;
- one crew member has at most one current ambulance assignment;
- DispatchCrew permanently records the crew present when a dispatch is created;
- a ready ambulance requires at least two current crew members, configurable later;
- dispatch status is assigned → acknowledged → en_route_to_scene → at_scene →
  transporting_to_hospital → handed_over, with declined/cancelled/reassigned terminal paths;
- a Duty Manager confirms routine dispatches; one responding crew member acknowledges or
  declines for the response unit;
- the patient may report for self or another person and sees only narrow tracking data;
- direct patient cancellation is allowed only before dispatch; after assignment it becomes
  a Duty Manager-reviewed cancellation request;
- Google calculates route/ETA on the backend and Flutter launches Google Maps for driving;
- this release serves one CareLanka Hospital emergency entrance; ward/bed preparation stays
  in Patient Management;
- maps, AI, push, and Patient Management failures never block manual dispatch;
- AI recommends and explains only; deterministic code validates and a human confirms;
- diversion is impossible from at_scene onward.

Make the API contract concrete rather than leaving “likely” routes unresolved. Include
current ambulance crew management, manual dispatch, crew acknowledgement/decline, the new
status enum, and patient cancellation-request operations. Preserve globally unique routes,
operationIds, and schema names. Keep Emergency's endpoint ownership and Patient Management's
ownership of the patient-facing report screen explicit. Update the build plan wording if an
exact contract decision makes one of its provisional phrases stale.

Update Emergency OpenAPI contract tests where useful, but do not add tests that require
unimplemented controllers. Run the repository's spec validation and lint commands, the
Emergency OpenAPI contract tests, and git diff --check. Review the final diff for stale old
status names, the old five-entity ownership list, in-app turn-by-turn promises, and any
claim that an ambulance moves without confirmation.

Mark Phase 0 complete in docs/build/emergency.md only when every owning document agrees and
all applicable checks pass. Finish with a concise handoff listing changed files, settled
contract operations, validation evidence, and any genuinely external group decision that
still blocks Phase 1. Do not call the phase complete with contradictions remaining.
```

---

## Phase 1 — Current crew and dispatch eligibility

```text
Work in the CareLanka repository and complete only Phase 1, “Current crew and dispatch
eligibility,” from docs/build/emergency.md. Confirm Phase 0 is marked complete and its specs
validate before editing. Read CLAUDE.md, CONTEXT.md, the full Emergency build plan, the
aligned Emergency design/spec, docs/build/common.md §7, STUBS.md, and existing Emergency
entities, configurations, services, controllers, migrations, and endpoint tests. Inspect git
status and preserve unrelated changes.

First establish the repository's FluentValidation foundation because it is not currently
installed: add the appropriate .NET 8 packages, automatic MVC validation, validator
discovery, and suppression of MVC's implicit non-nullable Required rule while preserving the
existing ValidationProblemDetails response factory. Add an integration test proving invalid
input returns the published 400 without entering business logic. Do this once in the
composition root; do not create component-specific validation pipelines.

Implement AmbulanceCrewAssignment, its EF configuration, DbSet, and a correctly generated
Emergency migration. Enforce the two partial unique indexes for current assignments. Add
current-crew request/response DTOs, FluentValidation validators, service operations, and
Duty-Manager-only endpoints from the aligned contract. Verify staff roles through the
published Staff lookup service; if it is not built, use a service-interface stub and record
it in STUBS.md exactly as CLAUDE.md requires.

Implement one eligibility service that returns both a decision and explicit block reasons:
inactive, out_of_service, insufficient_crew, active_dispatch, missing_location, and
stale_location. Minimum crew starts at two and comes from validated configuration. Block
crew changes during any live dispatch. Do not treat Ambulance.Status alone as proof of
eligibility.

Migrate any Emergency request DTO or query surface you deliberately touch away from
DataAnnotations and into separate FluentValidation validators. Do not widen this into an
unrelated repository-wide DTO migration.

Add unit, integration, authorization, concurrency, database-constraint, and generated
OpenAPI contract tests covering every Phase 1 invariant and exit criterion. Regenerate the
web client if the repository's current workflow requires it. Run focused tests, the full
.NET test suite, spec checks, code-generation drift checks, and git diff --check.

Mark Phase 1 complete only when the fleet API can explain why every ambulance is eligible or
blocked and the database rejects concurrent double-assignment. Report files, migration,
tests, contract changes, stub status, and anything that blocks Phase 2. Do not start call
intake or dispatch creation.
```

---

## Phase 2 — Patient emergency intake

```text
Work in the CareLanka repository and complete only Phase 2, “Patient emergency intake,”
from docs/build/emergency.md. Verify Phases 0–1 and their exit criteria first. Read CLAUDE.md,
CONTEXT.md, the Emergency build plan, aligned Emergency contract/design, Patient integration
boundary, auth/current-user code, representative controller/service/validator patterns, and
all existing Emergency tests. Preserve unrelated worktree changes.

Implement Emergency's call-intake backend: create, own-call list, dispatcher list/detail,
and permitted Duty Manager updates exactly as published. Read caller identity and principal
type from the JWT; never accept caller_user_id from the client. Support self and bystander
reports, correct coordinate rules, optional report text, default patient-call priority, and
idempotent submission using the published `idempotency_key`. Keep every request
DTO attribute-free and give it a separate FluentValidation validator. Keep business rules in
the service and controllers bind/delegate/return.

Do not implement Patient Management's Flutter screen. Publish the complete generated
contract it consumes and add an integration fixture or documented example that lets its
owner connect the screen without guessing. Do not create a patient record or write Patient
Management tables from Emergency.

Test missing/invalid coordinates, nullable versus required fields, caller identity,
patient-versus-staff access, bystander reporting, duplicate submission, default priority,
priority authorization, pagination/filtering, and attempts to access another caller's call.
Assert invalid models return the standard ValidationProblemDetails shape automatically.

Run focused and full backend tests, spec validation/lint, generated-client checks, and git
diff --check. Mark Phase 2 complete only when one valid submission is stored once and appears
on the Duty Manager's call board through tested APIs. Report the Patient Management handoff.
Do not create a dispatch or agent proposal.
```

---

## Phase 3 — Manual dispatch and state machine

```text
Work in the CareLanka repository and complete only Phase 3, “Manual dispatch and the state
machine,” from docs/build/emergency.md. Verify Phases 0–2 are complete. Read CLAUDE.md,
CONTEXT.md, the aligned Emergency design/spec, all Emergency entity configurations and
services, exception/message-code conventions, current-user authorization, and existing
concurrency-test patterns. Preserve unrelated worktree changes.

Implement a single DispatchService command path used by manual Duty Manager dispatch now
and AI proposal confirmation later. In one transaction re-check the call and ambulance,
recompute eligibility, create Dispatch, copy current AmbulanceCrewAssignment staff IDs into
DispatchCrew, and update projected call/ambulance state. Expand the database's unique active
dispatch index to every non-terminal status in the aligned contract.

Implement crew-scoped acknowledgement and decline, legal progress transitions,
Duty-Manager cancellation/reassignment, the 30-second unacknowledged alert, and handover as
the only successful completion path. One responding crew member acts for the response unit.
A decline records its reason and returns the call to dispatcher attention. Enforce the
at_scene diversion boundary in normal C# on every applicable path. Keep transitions and side
effects in the service, never controllers or validators.

Use clean request DTOs with separate FluentValidation validators. Publish every success and
problem response. Add tests for the complete transition matrix, role/ownership boundaries,
responding-crew history, decline, timeout alert, cancellation, reassignment, immutable
terminal states, and handover. Add real concurrent attempts against the database for two
dispatches on one ambulance and two live dispatches on one call; assert one wins and the
other receives the published conflict.

Run focused and full backend tests, spec checks, generated-client drift checks, and git
diff --check. Mark Phase 3 complete only when an API test takes a patient call from received
through handed_over without AI. Do not build React, Flutter, Maps, or agent code.
```

---

## Phase 4 — Dispatcher React vertical slice

```text
Work in the CareLanka repository and complete only Phase 4, “Dispatcher React vertical
slice,” from docs/build/emergency.md. Verify the manual backend Phase 3 exit criterion first.
Read CLAUDE.md, CONTEXT.md, the Emergency build plan, generated Emergency client/types,
existing React shell/auth/query patterns, and current page/navigation conventions. Preserve
unrelated work and work only within Emergency-owned frontend surfaces plus the smallest
shared navigation change required to expose them.

Build the Duty Manager workflow using only the generated API client and TanStack Query:
live call board, call detail/priority update, eligible-ambulance choices with block reasons,
one-tap manual dispatch, acknowledgement countdown/warning, fleet board, and current crew
assignment. Hide unauthorized actions, invalidate every affected query after mutations, and
handle loading, empty, retry, validation, and 409 concurrency states truthfully. Do not put
server state or transition rules in ad hoc useEffect fetches.

Add focused component/UI tests using the repository's established framework. If no test
framework exists, do not silently introduce a large stack: add the smallest agreed setup or
document the missing shared decision and still verify typecheck/build. Test the critical
operator path and a stale two-dispatcher conflict.

Run code generation first, then the published web typecheck/test/build commands, relevant
backend contract tests, spec checks, and git diff --check. Mark Phase 4 complete only when a
Duty Manager can crew an ambulance and manually dispatch it without Swagger. Do not build
crew Flutter, patient tracking, real Maps, push, or AI.
```

---

## Phase 5 — Crew Flutter vertical slice

```text
Work in the CareLanka repository and complete only Phase 5, “Crew Flutter vertical slice,”
from docs/build/emergency.md. Verify Phase 3's backend and Phase 4's dispatcher flow are
complete. Read CLAUDE.md, CONTEXT.md, mobile-ui/README.md, the Emergency build plan, aligned
contract, generated Dart-client conventions, Flutter auth/routing/state patterns, and shared
pubspec guidance. Preserve unrelated changes. Work inside lib/features/emergency; change
shared core or pubspec only when required and call the shared-file change out explicitly.

Build the ambulance-crew workflow: authenticated “My run,” foreground polling fallback,
acknowledge/decline, only-the-next-legal status actions, Open in Google Maps using raw scene
or hospital coordinates, concise handover, and paged run history. The active dispatch and
responding-crew membership come from the server/JWT, never a selectable crew ID. Do not put
sensitive report text into local notifications or logs.

Use the generated API client rather than handwritten HTTP/models. Add state, widget and
integration tests for successful progression, decline, 403 ownership, stale/conflict
responses, app resume, empty active run, and Maps-launch failure. Provide usable loading,
retry, offline, and validation states.

Run the published Flutter generation/analyze/test commands, backend contract tests, spec
checks, and git diff --check. Mark Phase 5 complete only when an assigned crew member can
acknowledge, navigate, progress and hand over from Flutter while an unrelated crew member is
refused. Do not add live location, patient tracking, Firebase, or AI yet.
```

---

## Phase 6 — Live location and patient tracking

```text
Work in the CareLanka repository and complete only Phase 6, “Live location and patient
tracking,” from docs/build/emergency.md. Verify the manual dispatcher/crew vertical slice is
complete. Read CLAUDE.md, CONTEXT.md, the aligned tracking/cancellation contract, Emergency
location entities/services, Patient Management screen boundary, Flutter permission guidance,
and privacy/auth conventions. Preserve unrelated changes.

Implement crew-device location reporting approximately every 10–15 seconds during a live
dispatch, storing only the ambulance's latest coordinates and update time. Stop on
cancelled, reassigned, or handed_over and recover correctly after app pause/resume. Make
staleness explicit. Keep location authorization scoped to responding crew and their vehicle.

Implement caller-scoped tracking and cancellation request APIs. A patient may cancel
directly only before a dispatch; afterwards create a Duty Manager-reviewed request. Return
only progress, latest ambulance position, ETA when known, last update time, and cancellation
state. Never expose crew identities, report/medical notes, AI reasoning, or other calls.

Emergency owns the APIs and crew-side location work. Respect the published ownership of the
patient-facing Flutter screen; provide its owner generated types and an integration fixture,
and modify that screen only if the owning documents now explicitly assign it to this track.

Test coordinate validation, wrong crew/caller access, stale positions, lifecycle start/stop,
completed-call tracking closure, direct pre-dispatch cancellation, post-dispatch request,
and privacy response shape. Run all relevant backend/Flutter tests, specs, codegen checks,
and git diff --check. Mark Phase 6 complete only when tracking is accurate, narrow and stops
cleanly. Do not integrate real Maps or Firebase yet.
```

---

## Phase 7 — Real Maps integration and fallback

```text
Work in the CareLanka repository and complete only Phase 7, “Real Maps integration and
graceful fallback,” from docs/build/emergency.md. Verify Phase 6 works with raw coordinates.
Read CLAUDE.md, CONTEXT.md, the Emergency route contract, current
IAmbulanceDistanceService/stub, configuration/secret conventions, RouteLog model, and Google
provider documentation relevant to the selected API. Preserve unrelated changes and never
commit credentials.

Replace the runtime straight-line stub with a production Google routing/geocoding adapter
behind existing provider interfaces while retaining deterministic fakes for tests. Rank
eligible ambulances by driving ETA, reverse-geocode scene coordinates when possible, and
persist the planned route summary/provider reference. Give Flutter a Google Maps launch
target; do not recreate turn-by-turn navigation inside CareLanka.

Implement explicit fallback: provider timeout, quota error, bad payload, or outage produces
straight-line ordering with a visible fallback indicator and never blocks manual dispatch.
Keep server API keys on the server and use platform-appropriate Maps launch mechanisms in
Flutter.

Test success, timeout, cancellation, quota/error status, malformed data, partial candidate
failure, fallback ordering, secret absence, and launch failure. Avoid live network calls in
the automated suite. Run focused/full backend and Flutter checks, spec validation, codegen
drift, and git diff --check. Mark Phase 7 complete only when road ETA is used normally and
the full dispatch remains usable during provider failure. Do not implement push or AI.
```

---

## Phase 8 — Push notification reliability

```text
Work in the CareLanka repository and complete only Phase 8, “Push notification reliability,”
from docs/build/emergency.md. Verify foreground polling already recovers assignments. Read
CLAUDE.md, CONTEXT.md, the Emergency build plan, Common ownership/docs for DeviceToken and
Notification, STUBS.md, Flutter secure configuration, and Firebase's current official setup
documentation. Preserve unrelated changes and credentials.

Use the Common-owned notification/device-token facilities; do not create Emergency copies.
If Common has not implemented them, integrate through a narrow interface stub, record it in
STUBS.md, and do not take ownership of Common tables. Send an idempotent Firebase assignment
notification only after the dispatch transaction commits. Retry delivery safely. Keep
active-dispatch polling as the source-of-truth recovery path.

The lock-screen payload contains no incident text, patient detail, or precise coordinates.
Opening it routes an authenticated responding crew member to My Run; the server still
authorizes access. Handle expired/replaced tokens, duplicate delivery, delayed delivery,
logout, and notification tap after reassignment or cancellation.

Use fakes/emulators for automated tests rather than real production delivery. Run backend,
Flutter, spec, codegen, and diff checks. Mark Phase 8 complete only when a backgrounded app
receives an assignment and a missed push is recovered by polling. Report any Common-owned
stub clearly. Do not implement hospital integration or AI.
```

---

## Phase 9 — Hospital preparation and handover integration

```text
Work in the CareLanka repository and complete only Phase 9, “Hospital preparation and
handover integration,” from docs/build/emergency.md. Verify manual handover and Maps routing
work first. Read CLAUDE.md, CONTEXT.md, Emergency's integration sections, Patient's
PreAdmitRequest/endpoint contract, STUBS.md, priority-to-urgency mapping in every owning
document, and current retry/idempotency patterns. Preserve unrelated changes.

After a confirmed dispatch commits, invoke Patient Management's pre-admission operation
idempotently. Emergency sends the fixed agreed priority translation and the published caller,
patient and ETA data. It sends no ward or bed choice, never writes Patient tables, and never selects a
clinical ward/bed. Navigation targets the configured CareLanka Hospital emergency entrance.

Treat Patient Management failure as a recorded, retryable integration failure; do not roll
back or delay the ambulance dispatch. Guarantee one logical pre-admission per dispatch across
retries. If the endpoint remains unavailable, keep a service-interface stub recorded in
STUBS.md and test both success and outage behaviour.

Test exact request mapping, every priority translation, null/unknown patient, idempotency,
timeout/retry, permanent 4xx, temporary 5xx, and continued handover during outage. Run full
backend/integration/spec/codegen checks and git diff --check. Mark Phase 9 complete only when
one pre-admission is created per dispatch and its outage cannot stop the run. Do not build
the AI agent.
```

---

## Phase 10 — Dispatch & Routing AI agent

```text
Work in the CareLanka repository and complete only Phase 10, “Dispatch & Routing AI agent,”
from docs/build/emergency.md. Do not begin unless the complete manual path, real Maps with
fallback, notifications, and hospital integration meet their exit criteria. Read CLAUDE.md,
CONTEXT.md, docs/ADR.md agent decisions, common AgentWorkflow contracts, the Emergency agent
sections/spec schemas, manual DispatchService, validation rules, and diversion tests.
Preserve unrelated work.

Implement the agent as decision support with only the published read-only allow-listed
tools. Deterministic code supplies eligible candidates and block reasons; Maps supplies
travel facts. The model ranks and explains a routine recommendation or proposes a tightly
bounded pre-arrival diversion. It never diagnoses, sets priority, writes entities, changes
status, or bypasses a human.

Persist the proposal through Common AgentWorkflow/AgentProposedChange infrastructure. A
routine recommendation pauses for one-tap Duty Manager confirmation. A diversion pauses for
full Duty Manager approval with the displaced patient's extra wait and recovery plan shown.
Immediately before execution, re-run every deterministic invariant and delegate to the same
DispatchService as the manual path. Keep manual dispatch available on model/provider failure.

Test tool allow-list enforcement, structured-output parsing, invalid/hallucinated IDs,
ranking explanations, no-candidate failure, model timeout, repeated confirmation,
stale-proposal rejection, race with crew reaching at_scene, routine confirmation, diversion
approval/rejection, audit linkage, and manual fallback. Use a fake model in automated tests.

Run all backend, agent, spec, codegen and diff checks. Mark Phase 10 complete only when every
agent outcome is explainable, deterministic checks remain authoritative, every write has a
human identity, and failure leaves the manual workflow usable. Do not start reports merely
to make the demo look complete.
```

---

## Phase 11 — Reports, history and production hardening

```text
Work in the CareLanka repository and complete only Phase 11, “Reports, history and
production hardening,” from docs/build/emergency.md. Verify Phases 0–10 honestly rather than
papering over incomplete exit criteria. Read CLAUDE.md, CONTEXT.md, the complete Emergency
plan/spec, assignment rubric, audit/notification infrastructure, all Emergency tests, and
the existing reporting UI conventions. Preserve unrelated changes.

Implement the published response-time, fleet-utilisation and agent-performance reports from
authoritative persisted timestamps/statuses. Add ambulance dispatch history and current/
historical crew-assignment views. Complete retry/reconciliation for notifications and
pre-admissions, audit coverage for every high-impact operation, patient call rate limits and
abuse controls, seed/demo fixtures, accessibility, poor-network, permission-denied, and
resume/recovery behaviour.

Do not fabricate metrics that the stored model cannot support. Define each calculation once,
test boundary dates/time zones and empty data, and make report filters/pagination match the
contract. Use generated clients and existing query/state conventions in React/Flutter.

Run the complete .NET suite, all web checks/build, Flutter analyze/tests, OpenAPI validation
and lint, generated-client drift gates, migration-from-clean-database test, seed scripts,
and git diff --check. Exercise documented failure demonstrations for Maps, AI, push and
Patient Management. Review authorization and privacy across every Emergency endpoint.

Mark Phase 11 complete only when reports reconcile with underlying dispatch history and the
full demo remains operable under each planned dependency failure. Finish with a submission
handoff listing requirements covered, commands and results, remaining external limitations,
and exact demo accounts/steps. Do not claim completion for skipped or manually assumed gates.
```
