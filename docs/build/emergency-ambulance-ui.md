# Emergency / Ambulance UI — Phase-by-Phase Implementation Plan

**Scope decided with the user:** web-ui (Duty Manager desk, rebuilt on HeroUI v3) + Flutter crew-screen polish + backend report endpoints (Phase 11 slice) + interactive Leaflet/OSM fleet map.

**Current state:** phases 0–8 are implemented. The web app has routed Duty Manager screens for calls, proposals and diversions, fleet map, ambulance and crew management, cancellations, and reports. Flutter supports both the crew run and the caller-scoped patient report, tracking, and cancellation paths. Deterministic demo fixtures and the clean-checkout rehearsal are documented in `docs/demo/emergency.md`.

---

## Cross-cutting standards (apply to every phase)

**Types — strict, generated, no hand-written HTTP:**
- All request/response types come from `web-ui/src/services/api/generated` (`bun run codegen`, never hand-edit). No `any`, no re-declared API shapes in components.
- Exhaustive label maps as `Record<EnumType, string>` (existing pattern in `EmergencyPage.tsx`), never inline ternaries. A missing enum case must be a compile error.
- Domain unions/labels live in `src/features/emergency/domain.ts`.

**Components — reusable, no duplication:**
- Shared primitives in `src/components/ui/`: built once, consumed by every screen.
- Screen-specific compositions in `src/features/emergency/components/`.
- If two screens need the same thing, it moves to `ui/` — never copy-paste.

**Error handling — industry standard:**
- `src/services/api/errors.ts`: strictly-typed helpers — `isConflict(error)`, `problemMessage(error, fallback)` (moved out of `EmergencyPage.tsx`, typed against the generated error shapes instead of `unknown` sniffing).
- Queries: `QueryState` wrapper renders loading skeleton / error-with-retry / empty state. No ad-hoc `isLoading &&` checks per screen.
- Mutations: button disabled while pending; success → `sonner` toast + targeted `queryClient.invalidateQueries`; `409` → conflict toast + invalidate the affected boards (existing behavior, standardized); validation problems from the API (`DispatchProposalError[]`) render inline, not as toasts.
- Keep the existing `refreshOperationalBoards` invalidation-predicate pattern.

**Comments:** rare — only for non-obvious invariants (why `at_scene` is divertible-proof, why staff input is a UUID). Max 2 lines, only where a future dev would actually be confused.

**Polling:** live boards use TanStack Query `refetchInterval` (5s) — calls, proposals, fleet. No new polling mechanisms.

**Per-phase verification gates (from `docs/build/emergency.md` §7):**
```
web-ui:  bun run typecheck && bun run test && bun run build
          (+ bun run check:codegen when the API changed)
api:      dotnet test
flutter:  flutter analyze && flutter test
manual:   seeded demo steps listed per phase (login: duty.rajapaksa@carelanka.lk / CareLanka#2026)
```

---

## Phase 0 — Foundation: React 19 + Tailwind v4 + HeroUI v3

**Why first:** HeroUI v3 requires React 19+ and Tailwind v4; the app is on React 18.3 with no Tailwind. Nothing else can be built until this lands.

1. Upgrade: `react@19`, `react-dom@19`, `@types/react@19`, `@types/react-dom@19`. Confirm peer compat of `@tanstack/react-query@5`, `react-router-dom@7`, `sonner`, `@testing-library/react@16.3` (all support 19).
2. Add: `tailwindcss@^4`, `@tailwindcss/vite`, `@heroui/react@^3`, `@heroui/styles`.
3. `web-ui/vite.config.ts`: register the `tailwindcss()` plugin.
4. `web-ui/src/index.css` — top of file:
   ```css
   @import "tailwindcss/theme.css";
   @import "tailwindcss/utilities.css";
   @import "@heroui/styles";
   ```
   Deliberately **skip Tailwind preflight** so the ~17 legacy pages' hand-rolled CSS stays pixel-identical. If a HeroUI component visually depends on preflight, switch to the full `@import "tailwindcss"` and re-eyeball legacy pages (fallback path, verification covers it).
5. Restyle `src/components/AppShell.tsx` with HeroUI (`Button` for sign-out, keep the existing nav/`.shell-header` CSS for layout) — proves the library works in the shell without touching legacy pages.
6. `src/main.tsx`: ensure `Toaster` from `sonner` is mounted app-wide (it's already a dependency; standardize on it — the `notice` string pattern dies in Phase 2).

**Verify:** clean `bun install`; `bun run typecheck`, `bun run test`, `bun run build` all green; dev server — login as duty manager, click through every legacy page: visually unchanged; HeroUI button renders and animates in the shell.

---

## Phase 1 — Emergency feature restructure + shared primitives

**Goal:** break the `EmergencyPage.tsx` monolith into a feature module and build the reusable base the later phases compose. Fix bad patterns here, not in each screen.

1. New structure:
   ```
   src/features/emergency/
     emergency-routes.tsx     (route entries for App.tsx)
     components/              (screen compositions)
     domain.ts                (labels, status colors, formatters)
     hooks/                   (thin wrappers around generated query options)
   src/components/ui/         (app-wide primitives)
   src/services/api/errors.ts (typed conflict/problem helpers, moved from EmergencyPage.tsx)
   ```
2. Build the shared primitives (HeroUI-backed):
   - `DataTable` — HeroUI Table with columns, loading skeleton, error/empty states, optional pagination. One table component for call board, fleet, register, cancellation queue, reports.
   - `QueryState` — wraps any query: loading / `ErrorState` (retry) / empty / children.
   - `StatusChip` — maps any domain enum value to a semantic HeroUI Chip color via an exhaustive record.
   - `ConfirmDialog` — HeroUI Modal for irreversible actions (retire, approve diversion).
   - `ReasonDialog` — Modal with a required reason select/textarea (used by reject-diversion, reject-cancellation; required-ness enforced client-side too).
   - `DetailField`, `SummaryCard` (reports), relative-time/distance formatters in `domain.ts`.
3. `domain.ts`: move `priorityLabels`, `blockReasonLabels` here; add `callStatusLabels`, `dispatchStatusLabels`, `ambulanceStatusLabels`, `proposalStatusLabels`, `rejectionReasonLabels`, `cancellationStatusLabels` — all exhaustive.
4. Emergency routes skeleton: `/emergency` (calls, default), `/emergency/proposals`, `/emergency/fleet`, `/emergency/register`, `/emergency/cancellations`, `/emergency/reports`. AppShell keeps a single "Emergency" link; inside, a HeroUI Tabs bar bound to the sub-route. Replace the single route in `src/App.tsx`.
5. Migrate `EmergencyPage.test.tsx` mocks into a shared test helper (the `vi.mock('../services/api/generated/@tanstack/react-query.gen')` pattern, extended per phase).

**Verify:** gates green; `/emergency` renders the existing functionality (call board, dispatch, fleet, crew — still old visuals inside the new shell) using existing data; no duplicate route or dead code; the old monolith is the only remaining consumer of `problemMessage`/`isConflict` (deleted next phase).

---

## Phase 2 — Live call board + call detail + front-desk intake + manual dispatch (HeroUI rebuild)

**Goal:** full parity with today's page in HeroUI, plus the two missing basics: front-desk call logging and sub-route structure.

1. `CallBoard` (`features/emergency/components/call-board.tsx`):
   - DataTable, default sort priority → waiting minutes; status filter (open calls / all); priority `StatusChip`; 5s poll via `listEmergencyCallsOptions` (keep the `nearTo` params on the ambulances query as today).
2. `CallDetail`:
   - Caller info, `details`, `address_label` (reverse geocoded — currently unused by the UI), scene coordinates, waiting time.
   - Priority select (Duty Manager only) via `updateEmergencyCallMutation` — replaces the string-`notice` flow with a toast.
   - Dispatch history including `acknowledgement_overdue` warning (`AcknowledgementStatus` component, alert styling).
3. `LogCallDialog` — the front-desk path (spec §10: a phone caller has no app):
   - `caller_name`, `caller_phone`, `patient_is_caller`, `details`, numeric lat/long + `location_accuracy_metres` inputs (upgraded to a map picker in Phase 6).
   - `idempotency_key` = `crypto.randomUUID()` per form session; posts `createEmergencyCallMutation`.
4. `EligibleAmbulanceList` — manual dispatch: crew counts, ETA/drive-minutes or straight-line fallback label, block reasons from `blockReasonLabels`, one-tap dispatch button, 409 conflict handling.
5. Delete `src/pages/EmergencyPage.tsx` + `EmergencyPage.test.tsx` once parity is confirmed; port their three tests to the new components.

**Verify:** gates green; manual flow against seeded DB: log a front-desk call → appears on board → select → dispatch the eligible ambulance → acknowledgement warning appears; `409` (dispatch same ambulance twice) shows the conflict toast and refreshes the boards.

---

## Phase 3 — Dispatch proposal queue + diversion review (the two human gates)

**Goal:** the screen the track doc says doesn't exist: "A React screen for the dispatch-proposal queue (the API exists; nothing calls it yet)." This is the demo centerpiece — the AI gate.

1. "Ask the agent" action in `CallDetail` (`createDispatchProposalMutation`) with pending state; the call row shows a "recommendation pending" indicator.
2. `ProposalQueue` (`/emergency/proposals`):
   - `listDispatchProposalsOptions` filtered to `pending_confirmation` / `pending_approval`, 5s poll.
   - **Routine gate** — one-tap: recommendation card with ambulance, crew readiness, ETA, one-line reasoning; `Send` → `confirmDispatchProposalMutation`; `Reject` → `ReasonDialog` (`DispatchRejectionReason` select + free text, both required).
   - **Diversion gate** — review card: full `DiversionImpact` block (which call loses its ambulance + its priority, how long they've waited, **extra minutes imposed**, replacement ambulance or explicit "none free", ETA gain for the critical call); `Approve` → `approveDispatchProposalMutation`; `Reject` → `ReasonDialog`.
   - `failed` / `no_ambulance_available` proposals render as an honest failed state with `errors` (type `DispatchProposalError[]`) shown inline — never silently retried or hidden.
   - All three mutations: pending-disabled buttons, success toast + invalidate proposals/calls/fleet; validation failure (re-check failed at confirm time) shows returned errors inline and re-polls.
3. Wire the nav badge: proposal count chip on the Emergency tab.

**Verify:** gates green; seeded data (§3.4 seed: one `pending_confirmation`, one `pending_approval`): confirm the routine one → dispatch appears on call + fleet; reject the diversion with a reason → disappears with rejection recorded; force a validation failure (confirm after the ambulance goes busy) → inline error, no partial state; tests cover all three gates + failure display.

---

## Phase 4 — Ambulance register + crew management

1. `FleetBoard` + `AmbulanceRegister` (`/emergency/register`, `/emergency/fleet`):
   - Register: DataTable with status filter; `Add ambulance` / `Edit` dialog (`createAmbulanceMutation`, `updateAmbulanceMutation` — registration number required, `out_of_service_reason` required when status is `out_of_service`); `Retire` / `Reinstate` behind `ConfirmDialog` (`retireAmbulanceMutation`, `reinstateAmbulanceMutation`).
   - Fleet board: status `StatusChip`, crew `n/required`, `location_updated_at` (rendered "stale" when old — honest timestamps, existing spec language), active dispatch inline (`AmbulanceSummary.active_dispatch`), `is_divertible` indicator.
2. `CrewManagement` panel (per ambulance, both fleet and register link to it):
   - Current crew list; assign via `ReasonDialog`-style form. **Keep UUID input** — Staff Management's `/staff/lookup` is still a documented stub (`StubStaffLookupService.cs`), so build the seam, not the picker: a `StaffPickerField` component that takes an optional `lookup` hook; when M2 ships, only the hook body changes.
   - Unassign (`unassignCurrentAmbulanceCrewMutation`); blocked-during-live-run `409` → conflict toast explaining crew is locked (invariant §2.6).

**Verify:** gates green; add + edit + retire + reinstate an ambulance; assign/unassign crew; attempt crew change on a seeded live-run ambulance → clear blocked message, no UI dead-end.

---

## Phase 5 — Cancellation review

1. `CancellationQueue` (`/emergency/cancellations`):
   - `listEmergencyCancellationRequestsOptions`; pending first.
   - Card per request: caller's reason, call summary (priority, waiting, status, assigned ambulance).
   - `Approve` → `approveEmergencyCancellationRequestMutation` (behind `ConfirmDialog` — this recalls the ambulance); `Reject` → `ReasonDialog` with required review notes.
   - After decision: toast + invalidate calls, fleet, and the queue.

**Verify:** gates green; create a request via the patient path (`requestMyEmergencyCallCancellation` as seeded patient `chathura.w` or via API on a dispatched seeded call) → appears in queue → reject with notes (patient sees review result) → create another → approve (dispatch cancelled, ambulance freed on fleet board); tests cover both decisions.

---

## Phase 6 — Fleet map + LocationPicker (Leaflet + OSM)

1. Deps: `react-leaflet`, `leaflet` (+ `@types/leaflet`). Tiles: OpenStreetMap — free, no API key, consistent with the backend's OSRM/Nominatim choices.
2. `FleetMap` component (`features/emergency/components/fleet-map.tsx`):
   - Ambulance markers colored by status (same color record as `StatusChip`); open-call scene pins; popup with registration, crew, status, location age.
   - Legend; marker click selects the ambulance (same selection state as the board).
   - `React.lazy` the map (code-split; keeps jsdom tests free of canvas).
3. `LocationPicker`: draggable-pin Leaflet map used by (a) `LogCallDialog` (replacing raw lat/lng inputs, keeping manual coordinate entry as fallback for poor-network desks) and (b) call detail scene display.
4. "Open in Google Maps" external links on ambulance/call detail (`https://www.google.com/maps?q=lat,lng` — display aid only; navigation remains the crew app's backend-provided launch target).

**Verify:** gates green (map excluded from jsdom via lazy/mock — its selection logic tested with a mocked `react-leaflet`); markers match board statuses; drag the pin in `LogCallDialog` → submitted call carries the pinned coordinates; build output shows the map in its own chunk.

---

## Phase 7 — Reports: backend endpoints + UI (Phase 11 slice)

**Goal:** the three report screens the spec defines but the backend hasn't built. The spec (`specs/emergency-spec.yaml` L1668–1768) already settles routes, roles (DutyManager), `from`/`to` params, and schemas (`ResponseTimeReport`, `FleetUtilisationReport`, `EmergencyAgentPerformanceReport`) — implement to the contract, no spec changes needed.

1. **Backend** (`api/`):
   - Stamp `RouteLog.departed_at` / `arrived_at` on the `en_route_to_scene` / `at_scene` transitions (`DispatchService` — closes the "not filled yet" gap from track Phase 7), so the "dispatched → arrival" clock exists.
   - `Controllers/Emergency/EmergencyReportsController.cs`: three GETs — response times by priority (received→dispatched, dispatched→arrival), fleet utilisation (runs, committed hours, idle share per vehicle), agent performance (confirmed / rejected / failed rates, `validation_failure_rate`, `confirmed_without_change_rate` from `DispatchProposal` rows).
   - `Services/Emergency/ReportService.cs` with the queries; `[Authorize(Policy = Policies.DutyManager)]`; date-range validation.
   - Endpoint tests in `CareLanka.Api.Tests` (disposable-PostgreSQL pattern) — report numbers must agree with seeded dispatch history (that's the exit criterion in the track doc).
2. **Regen clients:** `web-ui` `bun run codegen` + `check:codegen`; regenerate `mobile-ui` models (swagger_parser) so both clients stay drift-free in the same commit.
3. **UI** (`/emergency/reports`):
   - Date-range inputs (`from`/`to`), default = last 30 days.
   - `SummaryCard` row (median decision time, median drive time, runs completed, agent agreement rate) + three DataTabs: Response times by priority, Fleet utilisation per vehicle, Agent performance. Tables, not charts — no chart library needed.

**Verify:** `dotnet test`; `bun run check:specs` (unchanged spec must still validate); web gates green; cross-check the response-time table against two seeded completed dispatches by hand; agent-performance numbers against seeded proposal outcomes.

---

## Phase 8 — Flutter polish + end-to-end rehearsal + docs

1. **Flutter polish** (`mobile-ui/lib/features/emergency/`): run `flutter analyze` + `flutter test`; regen API models; small improvements only — e.g. show the reverse-geocoded scene address on My Run if the detail exposes `address_label`, keep error banners and status-button pattern as-is (they're solid). No structural rewrite.
2. **End-to-end rehearsal** — BUILD_PLAN checkpoint 4, the assessed workflow:
   front-desk call (web) → proposal (agent) → Duty Manager confirm (web) → crew acknowledge + progress + navigate (phone) → patient tracking window sane → handover → pre-admission notice queued → cancellation-review path exercised → reports reflect the run.
3. **Docs:** update `docs/build/emergency.md` status line (Phase 11 slice complete, UI screens complete), `BUILD_PLAN.md` §7 row 7 state; graft `graft build` after the changes.

**Verify:** flutter gates green; full manual rehearsal passes with zero Swagger/direct-DB steps; every gate from the standards section green on a clean checkout.

**Verification status (2026-09-23):** Flutter analysis is clean and all 131 tests pass. My Run
shows the server-provided reverse-geocoded scene address. Patients can report an emergency,
recover recent requests, track the caller-safe status/ETA, cancel before dispatch, and request
Duty Manager review after dispatch. The fresh-database seed sequence and an immediate replay of
`006_emergency_demo_data.sql` were executed successfully; the fixture produces one ready
ambulance, two assigned crew, five status-history intervals, and two completed dispatches.

The executable rehearsal is in `docs/demo/emergency.md`. Background Firebase delivery still
requires configured credentials and a physical Android device, so that external-device check is
listed separately rather than being inferred from polling tests. The plan's front-desk call and
patient tracking step cannot be the same record: an unauthenticated phone call has no patient
principal that may read a caller-scoped `/me` endpoint. The runbook exercises both UI paths as
separate calls and preserves that privacy boundary.

---

## Flagged for later (useful, not in this plan)

- **`GET /dispatches` board** (spec has it, backend doesn't): live-runs board for all active dispatches. The fleet board's inline `active_dispatch` covers the demo; worth building in the next backend slice.
- **`link-patient` + call `outcome` + call `cancel`** (spec'd, backend missing): lets the desk link an unidentified patient after the scene and close out a false alarm pre-dispatch. Small backend slices; strong demo value with the seeded unidentified-patient call.
- **Pre-admission failure visibility**: Phase 9 notes "nothing shows the dispatcher that a pre-admission failed except the row and the log" — a small badge on call detail once M4's real endpoint lands.
- **Staff picker upgrade**: when Staff Management ships `/staff/lookup`, swap the `StaffPickerField` hook body (seam built in Phase 4) — a one-file change.

## Key files

- **Web:** `web-ui/package.json`, `vite.config.ts`, `src/index.css`, `src/main.tsx`, `src/App.tsx`, `src/components/AppShell.tsx`, `src/pages/EmergencyPage.tsx` (deleted Phase 2), `src/features/emergency/**` (new), `src/components/ui/**` (new), `src/services/api/errors.ts` (new), generated client via `bun run codegen`.
- **API:** `api/Controllers/Emergency/EmergencyReportsController.cs` (new), `api/Services/Emergency/ReportService.cs` (new), `DispatchService.cs` (RouteLog timestamps), `CareLanka.Api.Tests` (report tests).
- **Specs/docs:** `specs/emergency-spec.yaml` (reference only — no edits), `docs/build/emergency.md`, `BUILD_PLAN.md`.
- **Flutter:** `mobile-ui/lib/features/emergency/**` (polish only).
