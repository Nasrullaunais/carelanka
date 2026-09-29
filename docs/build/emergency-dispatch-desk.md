# Emergency dispatch desk — one screen from call to Send

**Owner:** Emergency (Nasrulla Unais) · **Status:** built 2026-09-28 on `feat/emergency-dispatch-desk`

## What changed while building

- **The group agreed to `Withdrawn`** (§1), so it is on both `AgentWorkflowStatus` and
  `DispatchProposalStatus`.
- **Three more moments open a recommendation**, because each puts a call back to waiting:
  a crew declining, a dispatch being cancelled (both exclude that ambulance), and a diversion
  taking the call's ambulance away.
- **`open_proposal_id` was never filled in by the server.** It is replaced by `latest_proposal`,
  which reuses the existing `DispatchProposalSummary` shape — no new schema name.
- **A late agent answer is stopped by a row version** on the proposal, not a check at the start
  of the run. The check alone would miss a withdrawal that lands while the agent is thinking.
- **Approving a patient's cancellation needs no withdrawal**: that call already has a live
  dispatch, so it cannot have an open recommendation.
- **"No ambulance" gets its own bell**, `dispatch_proposal_failed`, instead of reusing
  `dispatch_proposal_waiting`, which now means only "a diversion needs approval".
- **Every path that opens a recommendation or dispatches a call locks the call row first**
  (`IDispatchProposalLifecycle.LockCallAsync`), then reads. Without it, a priority change and a
  manual dispatch landing together left a dispatched call holding an open recommendation.
- **Send and Approve mark the recommendation executed inside the dispatch's own transaction.**
  Before, a withdrawal between the two saves reported a 409 after the ambulance had gone.
- **Each recommendation rings its own bell.** The bell still opens the call, but the duplicate
  check uses the recommendation id (`NotificationSubject.OccurrenceId`), so a second failure on
  the same call rings again.
- **Calls already waiting when this ships get no recommendation until someone presses
  Re-check.** Migrations are table changes only, so nothing back-fills them.
- **Not checked in a browser yet.** Everything below is covered by tests, not by eye.

## The problem

Sending an ambulance takes five clicks across two screens, and the agent only starts when
someone asks it to.

```
Today:  Calls tab → open call → "Ask the agent" → close → Proposals tab → find card → Send
After:  open call → Send
```

- Every step between "call arrives" and "ambulance moves" is time the patient waits.
- The Proposals card has no map and no caller report, so the Duty Manager approves an
  ambulance without the facts that should drive the choice.
- With several calls waiting, matching a card back to its call is done from memory.

## Decisions already made

| Question | Decision |
| :--- | :--- |
| Layout | Side by side: call list on the left, open call on the right. No pop-up |
| Call changes while a recommendation waits | Priority or scene change → old one withdrawn, agent runs again |
| After a reject | Depends on the reason (table in §3.4) |
| Proposals tab | Removed. History stays in each call and in Reports |
| When the agent runs | Automatically, for every new call, staff-logged or from the patient app |

The rule "AI agents propose, never write" is unchanged: nothing moves until a human presses
Send or Approve.

## 1. Open decision — needs the group

**A recommendation can now be closed by the system, not a person.** Examples: the call was
cancelled, someone dispatched by hand, the priority changed.

Today the only closed states are `rejected` (a person said no) and `failed` (the agent
broke). Recording a system closure as either one is false:

- As `rejected`, it counts against the agent in the Reports tab —
  `EmergencyReportService.GetAgentPerformanceAsync` treats every `Rejected` as a human
  review, so `ConfirmedWithoutChangeRate` drops for reasons that have nothing to do with
  the agent.
- As `failed`, it claims the agent broke when it did not.

**Proposal:** add `Withdrawn` to `DispatchProposalStatus` (Emergency's own enum) and to
`AgentWorkflowStatus` (common, group-owned — `docs/entity_diagram.md` §AgentWorkflowStatus,
`specs/common-spec.yaml`). Add a `DispatchWithdrawalReason` enum:
`call_changed`, `dispatched_manually`, `call_closed`.

`AgentWorkflowStatus` is shared, so adding a value is a group change: agreed first, then
changed in every spec that publishes it in the same commit. **Parts 3–5 wait on this.** If
the group says no, the fallback is to keep `AgentWorkflowStatus` untouched and hold the
withdrawal only on `DispatchProposal` — decided explicitly, not worked around.

## 2. Things found in the code that this plan also fixes

1. **Manual dispatch leaves the recommendation waiting.** `DispatchService.DispatchAsync`
   never touches `DispatchProposals`, so a call sent by hand keeps a "pending confirmation"
   recommendation forever.
2. **Cancelling a call does the same** (`CancelMineAsync`, `ApproveCancellationRequestAsync`).
3. **A late agent answer is saved anyway.** `DispatchProposalExecutor.ExecuteAsync` checks
   the proposal is `Pending` but not that the call is still waiting for an ambulance.
4. **Two requests at once throw a 500.** The unique index "one open proposal per call"
   (`DispatchProposalConfiguration`, filter on `pending`/`pending_confirmation`/
   `pending_approval`) is the real guard, but `StartAsync` only checks with a read first. Two
   clicks at the same moment both pass the read, and the second save fails as an unhandled
   database error.
5. **Bell notifications open `/emergency`, not the call.** `web-ui/src/types/notifications.ts`
   ignores the id for every emergency type.

## 3. Backend

### 3.1 One place that opens and withdraws recommendations

Three services need to open or close a recommendation: `EmergencyCallService`
(new call, call changed, call cancelled), `DispatchService` (manual dispatch) and
`DispatchProposalService` (re-check, re-run after reject).

`DispatchProposalService` already depends on `IDispatchService`, so `DispatchService`
cannot depend back on it — that is a loop the container refuses to build. Instead, move the
open/withdraw logic into one small class all three use:

```
Services/Emergency/DispatchProposalLifecycle.cs   (+ IDispatchProposalLifecycle)
  OpenAsync(call, allowDiversion, excludeAmbulanceIds, ct)   stages workflow + proposal
  WithdrawOpenAsync(callId, DispatchWithdrawalReason, ct)    stages the withdrawal
```

- Both **stage** changes and do not save. The calling service owns the save and the
  transaction, so the call and its recommendation land together or not at all.
- It depends only on `CareLankaDbContext` and `TimeProvider` — no service, so no loop.
- `StartAsync` keeps its checks and calls `OpenAsync`; the entity-building code moves, it is
  not copied.

**Withdraw and open in the same request must be two saves inside one transaction.** The
partial unique index is checked per statement, and EF Core does not promise to run the
`UPDATE` (withdraw) before the `INSERT` (open). Save the withdrawal, then stage and save the
new one, then commit.

**Wake the worker only after commit.** `IDispatchRunQueue.Enqueue` runs after
`CommitAsync`. If the process dies between the two, `DispatchProposalWorker.RecoverAsync`
picks the row up within 30 s — that loop is already the safety net, not a new one.

### 3.2 Start the agent when a call is created

`EmergencyCallService.CreateAsync`: inside a transaction, save the call, `OpenAsync` with
`allowDiversion: true`, save, commit, then enqueue. The idempotency path (same key sent
twice) returns the existing call and opens nothing.

### 3.3 Withdraw when the recommendation no longer applies

| Where | Reason |
| :--- | :--- |
| `DispatchService.DispatchAsync` (manual dispatch) | `dispatched_manually` |
| `EmergencyCallService.CancelMineAsync`, `ApproveCancellationRequestAsync` | `call_closed` |
| `EmergencyCallService.UpdateAsync` — priority or location changed | `call_changed`, then `OpenAsync` again |
| `DispatchProposalExecutor.ExecuteAsync` — call no longer `Received` or has a live dispatch | `call_closed`; the agent's answer is dropped |

`UpdateAsync` re-runs only when priority or coordinates **actually change** — saving the same
priority again does nothing.

### 3.4 After a reject, act on the reason

`DispatchProposalService.RejectAsync`, in the same transaction as the rejection:

| Reason | Next step |
| :--- | :--- |
| `ambulance_unsuitable` | `OpenAsync` again, adding the rejected ambulance to the previous exclusions |
| `unsafe_diversion`, `source_call_too_urgent_to_divert` | `OpenAsync` again with `allowDiversion: false`, same exclusions |
| `handled_another_way`, `no_longer_needed`, `other` | Nothing. The Duty Manager picks by hand or presses Re-check |

The rejection itself stays a human review with `ReviewedByStaffMemberId` set, and still
counts in the Reports tab.

### 3.5 Concurrency — a conflict, not a crash

Where a recommendation is opened, catch the unique violation on the open-proposal index and
throw `ConflictException(MessageCode.DispatchProposalConflict)`. This is the same pattern
`CreateAsync` already uses for the idempotency key. Give the index a named constant in
`DispatchProposalConfiguration` so the catch does not match on a string written twice.

### 3.6 The call list shows the recommendation

Add to `EmergencyCallSummary` a nullable `Recommendation` of a new type
`CallRecommendationSummary`:

```
status                      DispatchProposalStatus
proposed_ambulance_registration   string?
estimated_minutes_to_scene        int?
is_diversion                      bool
```

- It holds only the **open** proposal, or the latest `failed` one if the call is still
  waiting — that is what the list needs to say "Checking…", "AMB-03 · 6 min" or "Pick by
  hand".
- Built with a single projection in `ListAsync`, not one query per call.
- `CallRecommendationSummary` is a new name. Check `integration_of_functions.md` §11.6
  before settling it.
- `OpenProposalId` stays; the detail panel uses it to load the full proposal.

### 3.7 Notifications

- **`dispatch_proposal_waiting` changes subject to the call** (`emergency_call`, call id).
  The Duty Manager acts on the call; the recommendation is part of it. This lets the bell
  open the exact call.
- **Only send it when the recommendation needs attention beyond the normal flow:**
  `PendingApproval` (a diversion) or `Failed` (no ambulance found). Every call now gets a
  recommendation automatically, and "call received" already rang for it — two bells per
  call teaches people to ignore the bell.
- Rename the type if the meaning changes enough that the old name misleads — decide while
  building, and record it in `docs/build/notifications.md`.

### 3.8 Things that do not change

- `POST /dispatch-proposals` stays. It is now the **Re-check** button.
- Confirm, approve and reject endpoints keep their shapes.
- The agent, its validator and its tools are not touched.

## 4. Contract and documents — same commit as §3

The design doc, the spec and the integration doc change together.

- `specs/emergency-spec.yaml`:
  - `CallRecommendationSummary` and the new field on `EmergencyCallSummary`
  - `withdrawn` on `DispatchProposalStatus`, and the `DispatchWithdrawalReason` enum
  - `POST /emergency-calls` says it starts a recommendation
  - `PATCH /emergency-calls/{id}` says a priority or location change re-runs it
  - reject says which reasons re-run
- `specs/common-spec.yaml` and every other spec publishing `AgentWorkflowStatus` — only if
  the group agrees (§1).
- `docs/entity_diagram.md`: the new status, the new reason, the transition.
- `specs/emergency-management-plan.md`: the new flow replaces "Ask the agent".
- `specs/integration_of_functions.md`: check §11.6 for the new schema name; nothing else
  crosses a component boundary.
- `docs/build/emergency.md`: link this file from Phase 10.
- Run `bun run check:specs`. Regenerate `web-ui` and `mobile-ui` clients in the same commit.
- If `OpenApiContractTests` covers emergency, it must pass against the new spec.

## 5. Web screen

### 5.1 Selected call lives in the URL

`/emergency/calls/:callId` instead of `?call=`.

- The back button, a refresh and a bell click all land on the same call.
- `routeForNotification` for `emergency_call_received` and `dispatch_proposal_waiting`
  returns `/emergency/calls/${entity_id}`.

### 5.2 Side-by-side layout

`EmergencyDesk` renders the call list and the open call next to each other; `ActionDialog`
goes.

- On narrow screens, the list alone; opening a call shows it full width with a back link.
- The list keeps its current filter, paging and "Log emergency call" button.

### 5.3 The recommendation panel

A new `components/call-recommendation.tsx`, built from `ProposalCard` in
`proposal-queue.tsx` (moved, not copied), shown in `call-detail.tsx` above the ambulance
list.

| State | Shows |
| :--- | :--- |
| `pending` | "Checking available ambulances…" |
| `pending_confirmation` | Ambulance, minutes away, crew readiness, reason · **Send** · Reject |
| `pending_approval` | Same, plus the "impact on the other call" box · **Approve diversion** · Reject |
| `failed` / none | "No recommendation — pick an ambulance below" · Re-check |

- The ambulance list for picking by hand stays below, collapsed while a recommendation
  is ready and open when there is none.
- Status changes are announced to screen readers (`role="status"`).
- Reject keeps `ReasonDialog`. After a reason that re-runs, the panel goes back to
  "Checking…" by itself — it is reading the call's new open proposal.

### 5.4 The list shows each call's recommendation

`call-board.tsx` adds one line per call from `recommendation`: "Checking…", "AMB-03 · 6 min",
"Diversion — needs approval", or "Pick by hand".

### 5.5 After Send, open the next call

On a successful Send or Approve, go to the next call that is still `received` with no live
dispatch — highest priority first, then longest waiting. If none, clear the panel.

### 5.6 Remove the Proposals tab

Delete `proposal-queue.tsx` and its test once §5.3 covers their cases, the `proposals` route
and tab, and the pending-count badge. `useActionableProposals` goes if nothing else uses it.

### 5.7 Faster refresh

Polling every 5 s stays. When an emergency notification arrives over the SignalR connection
from the notifications work, call `invalidateEmergencyQueries` so the screen updates
straight away instead of on the next poll.

### 5.8 Server state stays in TanStack Query

The panel reads the proposal with the generated `getDispatchProposalOptions`. Every mutation
calls `invalidateEmergencyQueries`. No fetch in `useEffect`, no hand-written types.

## 6. Tests

**Backend** — `tests/CareLanka.Api.Tests/`:

- A new call, from staff and from a patient, gets exactly one open recommendation.
- Sending the same idempotency key twice opens one recommendation, not two.
- Manual dispatch withdraws the open one with `dispatched_manually`.
- Cancelling (both paths) withdraws with `call_closed`.
- A priority change withdraws and opens a new one; saving the same priority does not.
- Each reject reason leads to the step in §3.4, with exclusions carried forward.
- The executor drops its answer when the call was dispatched while it ran.
- Two opens at once: one succeeds, one gets 409 — never 500.
- The call list returns `recommendation` for open, failed and absent cases.
- Withdrawn proposals do not count as rejections in the agent performance report.
- The waiting notification fires only for `pending_approval` and `failed`.

**Web** — Vitest:

- Each of the four panel states in §5.3.
- Send moves to the next call in the right order.
- A bell click opens `/emergency/calls/:id` with that call selected.
- The list shows the recommendation line.
- Existing `proposal-queue.test.tsx` cases move to the new panel test before the file goes.

Run before each commit: `dotnet build`, `dotnet test`, `bun run check:specs`,
`bun run check:codegen`, `bun run typecheck`, `bun run test` in `web-ui`.

## 7. Order of work

1. **Group decision on §1.** Nothing else in §3 starts until it is settled.
2. **Backend + contract + docs + regenerated clients** — one commit. The migration, if the
   status check constraint changes, is named `Emergency_WithdrawDispatchProposals`; pull
   `main` right before generating it.
3. **Web screen** — its own commit.
4. Update `RESUME.md` with what was built and anything left unverified.

Branch from `feat/notification-system-plan`, not `main` — §3.7 and §5.7 build on the
notification code that is not merged yet.
