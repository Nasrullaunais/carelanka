# Emergency & Ambulance — full flow review

*Reviewed 2026-10-03 on branch `fix/emergency-real-staff-lookup`, including its uncommitted changes.*

> **Status:** every item below is addressed on branch `fix/emergency-flow-gaps`.
> What changed is summarised in `emergency.md` Phase 12. This file is kept as the record of
> what was wrong.

**In one sentence:** the happy path works end to end, but several real-life paths leave
someone stuck, and a few screens show raw code values to users.

What was checked:

- Every Emergency endpoint, service, validator and the dispatch agent in `api/`
- Every duty manager screen in `web-ui/src/features/emergency/`
- Every patient and crew screen in `mobile-ui/lib/features/emergency/`
- Builds and tests: `dotnet build` is clean, web tests 28/28 pass, Flutter tests 27/27 pass.
  **Passing tests do not mean the flow is complete** — none of the problems below has a test.

How to read the labels:

| Label | Meaning |
| :--- | :--- |
| 🔴 **Blocker** | Someone gets stuck, or the wrong thing happens in a real emergency |
| 🟠 **Bug** | Something is visibly wrong, or data ends up wrong |
| 🟡 **Gap** | A piece of the flow is missing |
| ⚪ **Polish** | Small, but a marker would notice it |

---

## 1. The top 8, in the order to fix them

| # | Who is hit | What goes wrong | Label |
| :-- | :--- | :--- | :--- |
| 1 | Crew | A run that reaches the scene can only end by driving to hospital. "Patient refused", "nobody there" and "treated at the scene" have no way out, and the ambulance stays locked | 🔴 C1 |
| 2 | Duty manager | No button to cancel or swap an ambulance once it is sent. The screen even says "Dispatcher attention required" and then offers nothing to do | 🔴 D1 |
| 3 | Duty manager | No way to close a hoax, duplicate or false-alarm call. It stays in "Awaiting dispatch" forever | 🔴 D2 |
| 4 | Duty manager | The **Retire** button always fails | 🔴 D3 |
| 5 | Patient | After a crew declines, the patient can no longer cancel at all | 🔴 P1 |
| 6 | Crew | When a run is cancelled, it just vanishes from the crew's phone. No message, no alert | 🔴 C2 |
| 7 | Everyone | The crew phone stops sending its position the moment they open Google Maps to drive | 🟠 C3 |
| 8 | Duty manager | Every call from the patient app arrives with no name and no phone number | 🟠 P3 |

---

## 2. Patient (Flutter app)

The flow: Home → **Request an ambulance** → pick "For me / For someone else" → describe →
wait for location → send → tracking screen → optionally cancel.

### P1 🔴 Patient is stuck after a crew declines

The steps:

```
1. Patient sends a request          → call is "received"
2. Duty manager sends ambulance A   → call is "dispatched"
3. Crew of A taps "I cannot take this run"
                                    → call goes back to "received"
4. Patient taps "Cancel request"
```

What happens at step 4:

- The app sees "received", so it tries a direct cancel.
- The server refuses because the call has *any* dispatch in its past
  (`api/Services/Emergency/EmergencyCallService.cs:348` — `call.Dispatches.Any()`).
- The other route, "ask the duty manager to cancel", also refuses: it needs a *live* dispatch
  (`EmergencyCallService.cs:360`).

Both doors are locked. The same thing happens after the duty manager cancels or reassigns.

**Fix:** at line 348, only refuse when there is a *live* dispatch (`Dispatches.Any(d => d.Status.IsLive())`).

### P2 🟠 The patient sees raw code values

| Screen | What the patient sees | Where |
| :--- | :--- | :--- |
| Tracking | `Cancellation: CancellationRequestStatus.pending` | `emergency_tracking_screen.dart:110` |
| Recent requests | `enRoute`, `received` | `report_emergency_screen.dart:134` |
| Recent requests | `2026-10-03 14:22:11.123456` | `report_emergency_screen.dart:138` |

**Fix:** a small label map, like `_statusLabel` already does at the bottom of the tracking
screen, and a formatted date.

### P3 🟠 The duty manager never gets the patient's name or phone

The app sends no `caller_name` or `caller_phone`, and the server does not fill them in.
So every app call shows on the desk as:

```
Caller details unavailable
No phone number recorded
```

The duty manager cannot ring the patient back.
The server already knows both: `Patient.FullName` and `Patient.Phone`.

**Fix:** in `EmergencyCallService.CreateAsync`, when the caller is a patient account, copy the
account holder's name and phone onto the call. This applies to "for someone else" too, because
the account holder is still the one calling.

### P4 🟡 Tracking shows no ambulance on a map and no arrival time

- The server already returns `ambulance_latitude` / `ambulance_longitude`. The screen ignores them.
- `estimated_minutes_to_arrival` is always empty — it is hard-coded to `null`
  (`EmergencyCallService.cs:337`).

So all the patient sees is a line of text: "An ambulance is on the way".

### P5 🟡 "For someone else" callers get no updates on their phone

The "on the way", "arrived" and "cancellation answered" alerts are sent to `call.PatientId`
(`DispatchService.cs:247`, `EmergencyCallService.cs:422`).
For "someone else" calls that field is always empty — the request checker forces it empty.
So the person who made the call hears nothing unless they keep the screen open.

**Fix:** send these alerts to whoever made the call (`CallerUserId` → their patient record).

### P6 🟡 Smaller cancel problems

- After asking for a cancellation, the **Cancel request** button stays. A second tap gets a
  "409 Conflict" error ("already requested").
- Tapping **Continue** with an empty reason quietly does nothing (`emergency_tracking_screen.dart:66`).
  It should say "Please give a reason".
- When the duty manager rejects a request, their notes never reach the patient. But the web
  dialog tells the duty manager "the patient can see this review result"
  (`cancellation-queue.tsx:185`). One of the two must change.

### P7 🟡 A patient can send a second request while the first is still open

There is no "you already have an active request — open it?" check, so a panicking user can put
three calls on the desk.

### P8 ⚪ Wording

- "An ambulance is on the way" shows as soon as one is *assigned*, before the crew has accepted.
- If the crew then declines, the screen flips back to "Request received" with no explanation.

---

## 3. Duty manager (React web, `/emergency`)

Tabs: **Calls · Fleet · Register · Cancellations · Reports**.

### D1 🔴 No way to cancel or swap an ambulance once it is sent

The server has both: `POST /dispatches/{id}/cancel` and `POST /dispatches/{id}/reassign`.
Nothing in `web-ui` calls them (searched: zero uses outside the generated client).

What the duty manager sees on a call whose crew has not answered:

```
Crew acknowledgement is overdue. Dispatcher attention required.
A response is already active ... another ambulance cannot be assigned to this call.
```

…and no button. The only way out is for the crew to act.

**Fix:** on each live dispatch card in `call-detail.tsx`, add **Cancel this ambulance** and
**Send a different ambulance** (both need a reason). Show them only while the run is still
before pickup, because the server only allows these then.

### D2 🔴 No way to close a call

A hoax, a duplicate, or "they got a taxi" has nowhere to go. The call stays "received":

- it sits in "Awaiting dispatch" forever,
- the AI keeps re-checking it every time anything changes,
- the response-time report counts it as never answered.

`docs/build/emergency-ambulance-ui.md` already lists `cancel`, `outcome` and `link-patient` on
calls as "in the spec, not built". This is the most important one of the three.

### D3 🔴 The Retire button always fails

```
web sends:    { "reason": null }              ambulance-register.tsx:100
server wants: reason must not be empty        RetireAmbulanceRequestValidator.cs
result:       400 every time
```

The confirm dialog never asks for a reason. **Fix:** use the existing `ReasonDialog` instead of
`ConfirmDialog` for Retire.

### D4 🟠 An ambulance's status can be overwritten in the middle of a run

The run steps (dispatched → en route → at scene → transporting) set the ambulance status on
their own. Two other doors can overwrite it:

| Door | What it allows | Where |
| :--- | :--- | :--- |
| Edit dialog → Status → **Available** | Accepted while the ambulance is on a run. Only "Out of service" is blocked | `AmbulanceService.cs:239-262` |
| **Reinstate** | Works on an ambulance that was never retired, and sets it to Available mid-run | `AmbulanceService.cs:294` |
| `PATCH /ambulances/{id}` as **crew** | The route lets crew in, so a crew member mid-run can change the status or even the registration number | `AmbulancesController.cs:63` |

The double-dispatch guard still holds (it checks for a live run, not the status). But the fleet
board, the map and the fleet-utilisation report will all show the wrong thing.

**Fix:** refuse any status change while a live run exists. Allow Reinstate only on a retired
ambulance. Make PATCH duty-manager only.

### D5 🟠 Closed calls show a waiting time that keeps growing

`waiting_minutes` = "now minus when the call came in" for **any** call without a live ambulance
(`EmergencyCallService.cs:616`). That includes completed and cancelled ones.

Under **All calls**, yesterday's finished call reads `1440 min`. Because the board sorts by
waiting time, it also jumps to the top.

**Fix:** waiting is `0` unless the status is `received`.

### D6 🟡 What the crew writes is never shown to anyone

The crew records a handover note, the patient's condition, and decline reasons. The duty manager
records cancel and reassign reasons.

- `DispatchSummary`, which the call page uses, has none of these fields.
- There is no `GET /dispatches/{id}` for the duty manager.

So this data is saved and then never seen. **Fix:** add the reasons and notes to the call
page's dispatch history.

### D7 🟡 Only the priority can be edited on a call

The server accepts changes to the scene location, the caller's name and phone, and the details.
The screen offers only priority, so a wrong GPS fix cannot be corrected.

### D8 🟡 The "Log emergency call" form

- **No priority choice.** Every phoned-in call starts as High. If it is really Critical, the
  duty manager must change it afterwards, which throws away the first recommendation and starts
  again.
- **"Accuracy (m)" is required** (`log-call-dialog.tsx:109`). A dispatcher taking a phone call
  has no idea what to put there. Fill it in automatically when the spot is picked on the map.
- **No address search.** The dispatcher must find the street on the map by hand.
- Typing coordinates moves the pin, but the map does not follow it, so the pin can sit off-screen.

### D9 🟡 The Cancellations tab offers Approve when it cannot work

Approve only works while the ambulance is still on its way. If the crew is at the scene or
driving to hospital, Approve gives a 409 error. The card shows only the *call* status
("En route"), not the run stage, so the duty manager cannot tell in advance.

Also: if the run ends (handed over, declined) while a request is pending, the request stays
"pending" forever. Approve fails, so the only way to clear it is Reject.

### D10 🟡 The call page doesn't show which patient it is

`patient_id` is on the call, but there is no name and no link to the patient record.

### D11 ⚪ Polish

- The Fleet board's "Active dispatch" column prints `critical · en_route_to_scene`
  (`fleet-board.tsx:28`). The label maps already exist in `domain.ts`.
- The Fleet board loads the call list every 5 seconds even when the map is hidden (`fleet-board.tsx:21`).
- "Runs today" and every report use UTC days. Sri Lanka is UTC+5:30, so a run at 03:00
  Colombo time lands on the previous day (`AmbulanceService.cs:180`, `EmergencyReportService.cs:157`).
- The cancellations list uses `Status`, `Page`, `PageSize` as query names. Every other list uses
  `status`, `page`, `pageSize` (the repo's camelCase rule).
- Unused code in `domain.ts`: `cancelReasonLabels` (it is Patient Management's enum),
  `proposalStatusLabels`, `proposalStatusTones`.

---

## 4. Ambulance crew (Flutter app, `/crew/run`)

The flow: alert → **Accept this run** → **Start driving** → **I have reached the scene** →
**Patient on board** → **Hand over** (condition + notes). Before accepting: **I cannot take this run**.

### C1 🔴 A run cannot end at the scene

The only legal move from "At the scene" is "Transporting to hospital"
(`DispatchService.cs:431-441`). And the duty manager's cancel only works *before* the scene
(`IsPrePickup`).

| Real situation | What the crew can do today |
| :--- | :--- |
| Patient refuses to go | Pretend to drive to hospital and hand over |
| Nobody at the address | Same |
| Treated at the scene | Same |
| Patient died | Same |

Otherwise the ambulance stays "At scene", busy and not dispatchable, forever.

The database already has the right fields: `EmergencyCall.Outcome` and `EmergencyCall.Transported`.
**Nothing ever writes them.**

**Fix:** a second button at the scene, **End without transport**, with an outcome picker. It
closes the run, frees the ambulance, and records the outcome. This changes the dispatch state
machine, so the plan, the spec and the integration doc must move together in one commit.

### C2 🔴 A cancelled run vanishes without a word

When the duty manager cancels, reassigns, or approves a patient's cancellation:

- the server sends the crew **no notification** (`CancelAsync`, `ReassignAsync` and
  `CancelForApprovedCancellationRequestAsync` in `DispatchService.cs` never call the notifier);
- the app's next refresh finds no live run and shows "No run right now"
  (`my_run_controller.dart:120`).

A crew halfway to the scene may not notice for minutes.
It is like a taxi app quietly deleting your ride while you're driving to the pickup.

**Fix:** notify the crew on all three. In the app, remember the last run and show
"This run was cancelled by the duty manager — you can stop" instead of the empty screen.

### C3 🟠 Location stops when the crew leaves the app

Location is sent every 12 seconds, but only while the **My run** screen is open and on top
(`crew_location_lifecycle.dart:39`; the banner says so: "while this screen is open").

When it stops:

- **Open in Google Maps** — the very button the crew uses to drive
- the screen locks
- they answer a phone call

What follows:

- After 5 minutes, the patient and the duty manager see "location has not updated recently".
- An idle ambulance whose crew phone is locked becomes **"Location is stale" → cannot be
  dispatched**. In the demo this is hidden by `DemoFleetLocationProcessor`, which moves the demo
  ambulances by itself. With real phones it would not be hidden.

This is also the main limit on the fleet map — see section 7.

### C4 🟡 The crew never sees what the emergency is

The run screen shows: status, priority, ambulance, scene address, crew count.
It does **not** show:

- what happened ("chest pain, conscious")
- the caller's phone, to ring for directions
- the patient's name

`DispatchDetail` simply has no such fields. **Fix:** add details and caller phone to the crew's
own run, and only to their own run (see X2).

### C5 🟠 "Going to ward: Not set" — always

`DestinationWardName` is never filled anywhere in the server (searched: zero writers).
So every run shows `Going to ward  Not set` (`run_card.dart:51`).
Either fill it from the pre-admission, or remove the row.

### C6 ⚪ Polish

- `Priority: critical` in lowercase code form (`run_card.dart:35`), and the same in Past runs.
- One tap on "I have reached the scene" or "Patient on board" cannot be undone. A short confirm
  is worth considering, given gloves and a moving vehicle.
- Two crew members on one ambulance can both tap Accept. The second gets a 409 error. The app
  already refreshes after a 409, so this is fine, but the banner shows a technical message.

---

## 5. The AI recommender (behind the duty manager's "Recommended" box)

Overall it is solid. It validates the model's pick, falls back to "fastest first" when Gemini is
down or picks badly, writes everything to the shared `AgentWorkflow`, and never writes a dispatch
itself.

### A1 🟡 Diversions are chosen without looking at distance

When no ambulance is free, the agent picks the **least urgent, oldest** run that hasn't reached
its patient yet (`DispatchAgent.cs:115-121`). It never checks how far that ambulance is from the
new call. It could be 40 km away.

The duty manager's "Impact" box then shows:

```
Extra wait imposed       Unknown — no replacement assigned
Time saved for this call Not estimated
```

because both are hard-coded empty (`DispatchAgent.cs:164-167`).
So the duty manager approves a diversion without knowing whether it helps.

**Fix:** rank the possible diversions by drive time to the new call (the road-time tool already
exists) and fill in "time saved".

### A2 🟡 A stale recommendation stays "Ready to send"

If the recommended ambulance becomes unavailable before **Send** is pressed, Send fails
correctly. But the recommendation stays "Ready to send" on the board until someone rejects it by
hand. A failed Send could re-check automatically.

### A3 ⚪ "Already waiting" in the diversion box means the wrong thing

It is time since *that ambulance was sent*, not since *that call came in* (`DispatchAgent.cs:135`).

---

## 6. Across all roles

### X1 🟠 Retiring an ambulance leaves its crew attached

`RetireAsync` does not unassign the crew. So:

- those crew members cannot be put on another ambulance until someone unassigns them by hand;
- their phone keeps trying to report location for the retired ambulance and gets refused every
  12 seconds.

Crew can also be *assigned* to a retired ambulance (`AmbulanceCrewService.AssignAsync` only
checks that it exists).

### X2 🟠 Privacy: any crew member can read any call

`GET /emergency-calls/{id}` lets every ambulance crew account read every call: caller name,
phone and description (`EmergencyCallService.cs:237`). It should be only calls their own
ambulance is on, like `GET /dispatches/{id}/route` already does.

### X3 🟡 "Out of service" without a reason is accepted by the server

The web form requires a reason. The server does not
(`UpdateAmbulanceRequestValidator` has no such rule). The rule belongs on the server.

### X4 ⚪ Clean code

- `DateTime.UtcNow` / `DateTimeOffset.UtcNow` is used instead of the injected `TimeProvider` in
  `AmbulanceService.cs:180,290`, `DispatchAgent.cs`, and `DispatchProposalExecutor.cs`. Tests
  cannot control the clock there.
- Searching for `%` or `_` in the call or ambulance search acts as a wildcard (not escaped).
- Otherwise the code follows the repo rules well: thin controllers, FluentValidation, typed
  errors, generated clients, row-level locks on calls, version checks on dispatches and
  proposals, idempotent call creation.

---

## 7. Fleet map — can we do it?

**Short answer: yes. Most of it already exists.**

Go to **Emergency → Fleet → Map**. `web-ui/src/features/emergency/components/fleet-map.tsx`
was built on 2026-09-23 (commit `b21e221`). It has:

- a Leaflet map with OpenStreetMap tiles (no API key, already installed)
- one coloured dot per ambulance, by status, with a popup
- open calls as red dots
- a legend, and a test

So the real question is not "can we" but **"what stops it being good"**.

### What is wrong with the current map

| # | Problem | Why it matters |
| :-- | :--- | :--- |
| 1 | It shows **one page of 25 ambulances**, with page buttons under the map (`fleet-board.tsx:20`) | Defeats "see all ambulances at once" |
| 2 | Calls come from the **first 25 calls of any status**, then closed ones are dropped (`fleet-board.tsx:21,45`) | With many closed calls, open ones go missing from the map |
| 3 | **Out-of-service ambulance red (`#dc2626`) and open-call red (`#e11d48`) look the same** | A broken ambulance looks like an emergency |
| 4 | En route, At scene and Transporting are all the same blue | The legend shows three identical dots |
| 5 | Clicking an ambulance opens the **Manage crew** pop-up on top of the map | You wanted information, you got a form |
| 6 | Stale positions look exactly like live ones | A dot 20 minutes old looks trustworthy |
| 7 | No line from an ambulance to the call it is going to; calls are not coloured by priority; clicking a call does not open it | The dispatcher can't read the situation at a glance |
| 8 | The map centres on the first ambulance instead of fitting all dots | Some dots start off-screen |

### What it needs

**Server (small):** one read-only endpoint built for the map, for example
`GET /api/emergency/live-map`. It returns every active ambulance (no paging) plus every open call
(received, dispatched, en route), with the link between them. The list endpoints cap a page at
100 and the call list filters on only one status, so reusing them means several requests and
gluing them together in the browser. A new route needs a unique name across all five specs —
check `integration_of_functions.md` §11.6 first.

**Web (most of the work):**

- Use the new endpoint and drop the paging.
- Use clear, different colours and shapes: ambulances as a vehicle icon, calls as a pin coloured
  by priority.
- Fade dots older than 5 minutes and add "updated 7 min ago" to the popup.
- Draw a line from each ambulance to its call.
- Clicking a call opens it on the Calls tab; clicking an ambulance shows a side panel, not the
  crew form.
- Fit the view to all dots on first load.

**Refresh:** keep polling every 5 seconds. A hospital fleet is tens of ambulances, so this is
cheap. Live push (SignalR is already in the project for notifications) is a nice-to-have, not
needed.

### The real risk: are the dots true?

A map is only as good as its positions, and today positions only arrive while the crew has the
app open and on top (C3). Without fixing that, a real map would mostly show faded, stale dots.
The demo hides it because the demo ambulances are moved by the server.

Fixing it means sending location from the background:

- `geolocator` (already in `pubspec.yaml`) supports this. On Android it runs a "foreground
  service" — the small permanent "CareLanka is sharing location" notification.
- It needs new permissions in `android/app/src/main/AndroidManifest.xml`
  (`ACCESS_BACKGROUND_LOCATION`, `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION`) and, on
  iOS, the `location` background mode in `Info.plist`. **None are there today.**
- These files are shared across the whole app, so **ask the group first** (the `mobile-ui/README.md` rule).

### Map tiles

`tile.openstreetmap.org` is free and fine for a university demo. Its usage policy forbids heavy
or commercial use. A real deployment would switch the one `TileLayer` URL to a paid provider or
a self-hosted server. Nothing else changes.

### Verdict

| Piece | Effort | Risk |
| :--- | :--- | :--- |
| Map endpoint + test | ~half a day | Low |
| Map screen fixes 1–8 above | ~1–1.5 days | Low |
| Background crew location | ~1 day + device testing | **Medium** — permissions, battery, and it needs group agreement |

**Feasible and worth doing.** Build the endpoint and the screen first: they make the demo much
stronger on their own. Treat background location as a separate piece of work, because it is what
makes the map trustworthy outside a demo.

---

## 8. What this means for you

- **Before the demo:** fix the four 🔴 items you would hit live — D1 (cancel/reassign buttons),
  D3 (Retire), P1 (patient stuck), C2 (silent cancel). Each is small.
- **C1 (ending a run at the scene) and D2 (closing a call)** are bigger. They change the state
  machine and the spec, so do each as one commit covering plan + spec + integration doc.
- **The map:** do section 7's endpoint and screen fixes. Raise background location with the group.
- **Add a test with each fix.** None of these paths has a test today, which is why 55 passing
  tests did not catch them.
