# Emergency crew flow — from "new run" to handover

**Owner:** Emergency (Nasrulla Unais) · **Status:** planned 2026-09-30 · Fixes 1–6 built on `feat/emergency-crew-flow`

## The problem

A crew can finish a normal run, but the app falls apart the moment real life happens:

- The ambulance stops sending its location as soon as the crew opens Google Maps.
- The crew drives to a call without knowing what happened or how to reach the caller.
- A run that is cancelled or given to another ambulance just disappears from the screen.
- A patient who is treated at the scene, refuses to go, or is not there cannot be recorded.
- What the crew writes at handover is saved, but no screen shows it to anyone.

This plan fixes those, one fix at a time, in the order below.

```
Today:  accept → drive (location freezes) → arrive → hospital only → notes go nowhere
After:  accept → drive (location live) → arrive → hospital OR finish at scene
                → notes on the duty manager's desk and the incoming admission
```

---

## 0. Before starting

**Two people besides us are involved.** Ask them first, so nothing waits later.

| Ask | Who | Why |
| :--- | :--- | :--- |
| Add `IAdmissionService.FindByDispatchIdAsync(string dispatchId)` | Lochana (Patient) | Emergency must cancel a pre-admission when no patient is coming (Fix 4). **Added in Fix 4 by the Emergency owner, with Patient's permission.** |
| Add `TreatedAtScene`, `DiedAtScene`, `CallCancelled` to `CancelReason` | Lochana (Patient) | Patient's cancel reasons have no honest match for these three (Fix 4). **Added in Fix 4, same way.** |
| Show the crew handover on the emergency admission's detail | Lochana (Patient) | That is the hospital's screen, not ours (Fix 5) |
| Edit `AndroidManifest.xml` and `Info.plist` | Group | These phone settings files are shared (Fix 1, Fix 2) |

Both Patient additions were made in Fix 4 itself, so no stub was needed.

**Every contract change moves together.** For each fix that changes the API, update these in
the same commit:
- `specs/emergency-spec.yaml`
- `specs/emergency-management-plan.md`
- `specs/integration_of_functions.md`
- `docs/entity_diagram.md` (only where a column changes)

Then regenerate both clients.

**New names, all checked against the five specs — none are taken:**

| Kind | Name |
| :--- | :--- |
| Operations | `getMyDispatch`, `endMyDispatchAtScene` |
| Schemas | `EndAtSceneRequest`, `SceneOutcome`, `AmbulanceHandover` |
| Notification types | `DispatchCancelled`, `DispatchReassigned` |

**Order, and why:**

| # | Fix | Why this order |
| :--- | :--- | :--- |
| 1 | Location keeps flowing during the run | Everything the patient and desk see depends on it |
| 2 | The crew knows what they are driving to | Small, and Fix 3 reuses the new fields |
| 3 | A run never vanishes silently | Adds `getMyDispatch`, which Fixes 4 and 6 reuse |
| 4 | Finish at the scene without transport | The only state machine change; needs Fix 3's end-of-run panel |
| 5 | The handover reaches people | Reads what Fixes 3 and 4 record |
| 6 | Safer step buttons and smarter navigation | Pure phone-app work on top of the above |
| 7 | Card polish | Last, because earlier fixes change the card |
| 8 | Real-phone check | Only a phone proves Fixes 1 and 3 |

---

## Fix 1 — Location keeps flowing during the run

**Problem:** the app stops sharing the ambulance's location whenever it is not on screen.
That happens as soon as the crew opens Google Maps, which is the whole drive.

```
crew_location_lifecycle.dart:33   any state but "resumed" → reporter.pause() → stop()
```

So the patient's tracking screen and the desk's fleet map show a frozen ambulance. And
`appsettings.json` sets `LocationMaxAgeMinutes` to 30, so nobody is even warned for half an hour.

### What changes for the crew

- While they have a live run, a small ongoing notification shows: **"Sharing AMB-3's location
  — run in progress"**.
  - It cannot be swiped away.
  - It goes away on its own when the run ends.
- With no live run, nothing changes from today: location is shared only while the app is on
  screen.

### Phone app (Flutter)

**Switch from "ask every 12 s" to a location stream, only during a live run.**
- `DeviceLocation.fixes()` already exists.
- Give it a `ForegroundNotificationConfig` on Android (built into `geolocator` 14):
  - title "Sharing ambulance location"
  - text "Run in progress for {registration}"
  - `setOngoing: true`
  - `enableWakeLock: true`
- On iPhone, use `AppleSettings`:
  - `allowBackgroundLocationUpdates: true`
  - `showBackgroundLocationIndicator: true`
  - `pauseLocationUpdatesAutomatically: false`

**Why a stream and a foreground service, not a timer:** Android freezes a background app's
timers. A foreground service is Android's own way of saying "this app is legitimately working
while you can't see it", and it only needs the *while in use* permission if it starts while the
app is open. The crew never has to grant "Allow all the time".

**`CrewLocationReporter` gets two modes:**

| Mode | When | How |
| :--- | :--- | :--- |
| `foreground` | No live run | Today's behaviour: stops when the app is not on screen |
| `run` | Live run | Stream + foreground service; keeps going when the app is not on screen |

- `MyRunController` tells the reporter which mode to use whenever the run appears or ends.
- `CrewLocationLifecycle` no longer calls `pause()` in `run` mode.

**Don't send every GPS point:**
- `distanceFilter: 25` metres.
- Send at most once every 10 s.
- Always send the first point straight away.
- One upload at a time. A new point that arrives while one is uploading replaces any point
  still waiting.
- **Heartbeat:** a phone that is not moving sends no stream updates, which would make a parked
  ambulance look stale. After 30 s of silence the reporter asks for a fresh fix and sends it.
- The ambulance id is looked up once and only again after a failed upload, not on every send.

**Stop cleanly:**
- The run ends (any way — Fix 3 detects every way): stop the stream, and the notification goes.
- The crew logs out: stop the stream first.
- Location permission is taken away mid-run: the stream errors, so show the existing
  permission banner and keep retrying.

### Shared platform files (ask the group first)

- `AndroidManifest.xml`:
  - `FOREGROUND_SERVICE`
  - `FOREGROUND_SERVICE_LOCATION` (Android 14 refuses the service without it)
  - `WAKE_LOCK`
- `Info.plist`:
  - `UIBackgroundModes` → `location`
  - the existing when-in-use text, updated to mention the notification

### Backend

- New `Emergency:TrackingLocationMaxAgeSeconds` (default 90).
- `TrackMineAsync` uses it instead of `LocationMaxAgeMinutes`, so "not updated recently" means
  something again.
- **Keep `LocationMaxAgeMinutes` for eligibility.** Which ambulances *can* be sent is a
  different question from "is the dot on the map fresh".
- Validate it in `Program.cs` next to the other Emergency option checks.

### Edge cases

| Case | What happens |
| :--- | :--- |
| Android 13+ and notifications refused | The service still runs; Android just hides the notification. No change to sharing. |
| Precise location off | Existing "approximate only" banner. Nothing is shared: dispatch needs a precise position, so a rough dot would mislead more than it helps. |
| GPS switched off mid-run | The stream errors → "Location is turned off" banner with "Turn on location". It looks again every 30 s and resumes by itself when GPS is back. |
| No signal for 5 min | Points are dropped, not queued — an old position is useless. The first point after reconnecting goes out at once. |
| Android kills the app anyway | The run is on the server. The next launch opens My run and the stream restarts. |
| Two crew phones on one ambulance | Both report. The server keeps the latest. That is fine and needs no change. |
| The demo fleet worker | It already skips ambulances on a live run, so it never overwrites a real phone. |

### Tests

- `crew_location_reporter_test.dart`:
  - switching modes
  - the 10 s and 25 m throttle
  - one upload at a time
  - the stream stops when the run ends
  - the permission error in `run` mode
- Backend: `TrackMineAsync` flags a location as stale after 90 s, and the fleet eligibility
  rule is unchanged.

**Done when:** on a real Android phone, the ambulance dot keeps moving on the patient's
tracking screen while the crew phone is in Google Maps with the screen off.

---

## Fix 2 — The crew knows what they are driving to

**Problem:** the run card shows priority, ambulance and street name only. The crew does not
know what happened, and cannot phone the caller when the street name is vague — common here,
because many roads have no clear name.

On top of that, the patient app never sends a caller name or phone
(`patient_emergency_service.dart`), so those fields are empty even on the desk.

### What changes for the crew

A **Scene** card above the step button:

```
┌ Scene ─────────────────────────────────────┐
│ "My father collapsed and is not waking up" │
│ 42 Galle Road, Dehiwala                    │
│ ⚠ Location is approximate (±300 m)         │  ← only when accuracy > 50 m
│                                            │
│ Caller: Nimal Perera (for someone else)    │
│ [ 📞 Call caller ]                          │  ← hidden when no number
└────────────────────────────────────────────┘
```

- The approximate-location warning suggests calling to confirm the exact spot.
- **"Call caller"** opens the phone's dialer with the number filled in. It does not call
  automatically, so a mistaken tap costs nothing.

### Backend

**Fill in caller details for app calls.** In `EmergencyCallService.CreateAsync`, when a patient
raises the call and sent no `caller_name` / `caller_phone`:
- Copy `FullName` and `Phone` from `IPatientService.FindByUserAccountIdAsync`.
- This is a copy taken at that moment, like the crew list on a dispatch: the number the caller
  had when they called.
- It reads through Patient's service, never the table.

**`DispatchDetail` gains:**

| Field | From |
| :--- | :--- |
| `scene_details` | `EmergencyCall.Details` |
| `scene_latitude`, `scene_longitude` | the call |
| `scene_location_accuracy_metres` | the call |
| `caller_name`, `caller_phone` | the call |
| `patient_is_caller` | the call |

**Caller contact only while the run is live, for the crew.**
- `ToDetail` takes the caller's role into account.
- A crew member reading a finished run (Fix 3's `getMyDispatch`) gets `caller_name`,
  `caller_phone` and `scene_details` as null.
- The duty manager always sees them.
- One rule in one place, not a check in every screen.

### Phone app (Flutter)

- New `SceneCard` widget in `widgets/`.
- `tel:` link through the existing `url_launcher`.
- Add a `<queries>` entry for the `tel` scheme to `AndroidManifest.xml` (a shared file — same
  group ask as Fix 1), or Android 11+ may say no app can place the call.
- If the dialer cannot open, show a snackbar with the number written out, so it can be typed by
  hand.

### Edge cases

| Case | What happens |
| :--- | :--- |
| No details typed | The details line is hidden, not shown as "Not set" |
| Caller has no phone on record | No button; the row reads "No phone number recorded" |
| Desk logged a phone call | Name and phone were typed by the desk; shown as-is |
| Street name still loading (the lookup runs in the background) | Show coordinates plus "Street name loading…". The 10 s refresh fills it in. |
| Caller corrects the location after dispatch | Existing `PATCH` moves the call; the next refresh shows the new point. The navigation link is fetched fresh on every tap. |

### Tests

- Backend:
  - an app call copies the caller's name and phone
  - a desk call keeps what was typed
  - the crew gets null caller fields on a finished run
  - the duty manager always gets them
- Flutter: `run_card_test.dart` covers the call button hidden or shown, the accuracy warning,
  and the missing street name.

---

## Fix 3 — A run never vanishes silently

**Problem:** when the duty manager cancels or reassigns a run, or the agent diverts the
ambulance, the next refresh just shows "No run right now". No push, no reason. A crew driving
with sirens on cannot tell a cancelled run from a broken app.

A diversion is worse: the card quietly swaps to a **different address**.

### What changes for the crew

A panel at the top of My run that stays until they press **OK**:

| What happened | Panel |
| :--- | :--- |
| Cancelled | **Run cancelled.** "The caller no longer needs an ambulance." You can stop driving. |
| Given to another ambulance | **Run given to another ambulance.** {reason} |
| Diverted | **You have been sent to a more urgent call.** Accept the new run below. |
| Handed over | **Handover recorded at 10:52.** AMB-3 is available for the next run. |
| Finished at scene (Fix 4) | **Run finished at the scene.** AMB-3 is available for the next run. |

- It is a panel, not a pop-up message that disappears on its own. A driver may not look at the
  phone for a minute.
- A **push** (loud, the same urgent channel as a new run) arrives with it, so a phone in a
  pocket buzzes.

### Backend

**New endpoint — `GET /api/me/dispatches/{id}`, operation `getMyDispatch`.**
- Returns a run this crew member was on, live or finished.
- Responses:
  - `200` `DispatchDetail`
  - `403` not your run
  - `404` does not exist

**`DispatchDetail` gains `superseded_by_dispatch_id`**, so the app can tell a diversion from a
plain reassignment.

**Two new notification types**, both sent to every crew member on the run, both on the
`Urgent` channel:

| Type | Raised in | Text |
| :--- | :--- | :--- |
| `DispatchCancelled` | `CancelAsync`, `CancelForApprovedCancellationRequestAsync` | "The run for {registration} was cancelled. Open CareLanka to see why." |
| `DispatchReassigned` | `ReassignAsync`, `ApplyDiversionAsync` | "The run for {registration} was given to another ambulance. Open CareLanka for details." |

The reason is **not** in the push text: a lock screen is visible to bystanders. The reason
shows inside the app.

- If the same ambulance is diverted, its crew gets `DispatchReassigned` for the old run *and*
  `DispatchAssigned` for the new one. That is correct: two things happened.

**Clear old "new run" alerts** with `INotifier.ResolveAsync(DispatchAssigned, subject)`:
- On **acknowledge**, so the second crew member's alert is marked handled.
- On **decline, cancel and reassign**, so nobody taps a dead alert later.

**Fix `CancelForApprovedCancellationRequestAsync`.** Today it never sets `CancellationReason`,
so the panel would have nothing to show. Set it to a fixed resource text,
`cl_emg_` "The caller no longer needs an ambulance". Do not use the patient's own words: they
wrote them for the duty manager, not the crew.

**Everything the notifications track needs, in one commit:**
- `NotificationType.cs`
- `NotificationTypeExtensions.cs` (the `Urgent` channel)
- `NotificationTexts.resx`
- migration `Emergency_NotifyCrewOfEndedRuns` (the `type` column has a check constraint that
  lists every value)
- `notification_route.dart` (both go to `_myRun`)
- `web-ui/src/types/notifications.ts`
- the type list in `docs/build/notifications.md`
- one test each in `EmergencyNotificationTests.cs`, following the existing
  one-test-per-type pattern

### Phone app (Flutter)

**`MyRunController` remembers the last live run's id.** On every load:

1. **Same run** → nothing new.
2. **Run gone, or a different id** → fetch `getMyDispatch(lastId)` and build a `RunEnding`
   from its status and reason.
   - **Diversion:** the old run is `reassigned` and its `superseded_by_dispatch_id` equals the
     new active id.
3. **The fetch fails** (no signal) → a plain panel: "This run is no longer assigned to you."
   Never make up a reason.

**`RunEnding` lives in the controller, not the screen**, so it survives going to Past runs
and back.

**Tapping OK** clears it.

**A newer run takes priority, and the ending notice is kept above it.**

### Edge cases

| Case | What happens |
| :--- | :--- |
| App closed when the run was cancelled | No last id is known, so no panel. The push and the inbox cover it. This is why both layers exist. |
| Cancelled while the crew is tapping "I have reached the scene" | The tap gets a 409 → reload → the panel shows "cancelled". The error banner is cleared, because the panel explains it. |
| Crew declines at the same moment the desk reassigns | Whichever lands first wins. The other gets a 409 and sees the real result in the panel. |
| Cancel after the crew reached the patient | Impossible — the server refuses it after `at_scene`. So the handover sheet can never be cut off. |
| Both crew phones open | Both detect it on their own next refresh; both get the push. |
| A run is handed over and a new one arrives within 10 s | The handover panel sits above the new run's Accept button. |

### Tests

- Backend:
  - `getMyDispatch`: your run, someone else's run, a missing run
  - each new notification type, including the diversion case with both types
  - the alert is cleared on acknowledge and on cancel
- Flutter (`my_run_controller_test.dart`):
  - every row of the panel table
  - a diversion
  - a failed fetch
  - the ending survives a refresh
  - OK clears it

---

## Fix 4 — Finish at the scene without going to hospital

**Problem:** after "I have reached the scene", the only way forward is the hospital
(`DispatchService.Move`). In real life many runs end at the scene: treated there, refused to
go, not found, a false alarm, or the patient has died.

The desk cannot help either: cancelling is refused after `at_scene`, on purpose. So today the
ambulance is **stuck on that run forever.**

The design already planned for this. `EmergencyCall.Transported` exists ("not every call ends
in a hospital trip", plan §3.1), but nothing ever writes it.

### What changes for the crew

At the scene, under the main "Patient on board" button, a second button:
**"Finish without going to hospital"**. It opens a sheet:

```
Why is nobody going to hospital?
  ( ) Treated at the scene
  ( ) Patient refused to go
  ( ) Patient not found at the location
  ( ) False alarm
  ( ) Patient died at the scene
Notes (optional)  [                      ]
                          [ Finish run ]
```

- **Finish run** stays disabled until a reason is picked.
- Afterwards, Fix 3's panel: "Run finished at the scene".

### Backend — state machine

```
at_scene ──► transporting_to_hospital ──► handed_over
    └──────► ended_at_scene            (new, final)
```

- `DispatchStatus.EndedAtScene` (sent as `ended_at_scene`). Legal only from `AtScene`.
- It is **not** "live" (`IsLive`, `LiveStatuses` unchanged), so:
  - the ambulance becomes available again
  - the agent sees it as free
  - the one-live-run-per-ambulance rule releases it
- Enums are stored as text (ADR 5), but the column has a check constraint listing every value,
  so the new value **needs a migration** (`Emergency_CallSceneOutcome`, below).

**New endpoint — `POST /api/me/dispatches/{id}/end-at-scene`, operation `endMyDispatchAtScene`.**
- Body: `EndAtSceneRequest`
  - `outcome: SceneOutcome` (required)
  - `notes: string?` (max 1000)
- Checked by its own `EndAtSceneRequestValidator` (FluentValidation, not attributes).
- Responses: the same as `recordHandover` — `200`, `400`, `401`, `403`, `404`, `409`.

**`SceneOutcome`:**
- `TreatedAtScene`
- `PatientRefused`
- `PatientNotFound`
- `FalseAlarm`
- `PatientDeceased`

**`DispatchService.EndAtSceneAsync`** (one transaction):
- dispatch → `EndedAtScene`, set `CompletedAt`
- ambulance → `Available`
- call → `Completed`, `Transported = false`, `SceneOutcome` and `SceneOutcomeNotes` set
- the pre-admission is withdrawn (below)

**`HandoverAsync` now also sets `Transported = true`**, so every finished call says which way
it ended.

**Data change — migration `Emergency_CallSceneOutcome`:**
- The unused text column `EmergencyCall.Outcome` is renamed `SceneOutcomeNotes` (max 1000), and
  a new `SceneOutcome? SceneOutcome` column is added (stored as text with a check constraint).
- Safe: nothing has ever written `Outcome`, so every row is empty.
- The migration also carries the check constraints for the new `DispatchStatus`,
  `PreAdmissionStatus` and Patient `CancelReason` values, the new `withdrawal_reason` column and
  a row version on `pre_admission_notices`, and the widened "due" index.
- `entity_diagram.md` and the `EmergencyCall` schema in the spec are updated.
- The spec's old, never-built `POST /emergency-calls/{id}/outcome` is removed: this fix
  replaces it.

### Backend — tell the hospital nobody is coming

**Problem inside the problem:** the ambulance's dispatch already created a waiting admission
in Patient Management (`PreAdmissionNotice` → `PreAdmitAsync`). If nobody is transported, that
admission sits in the ward's list forever, maybe holding a bed.

**The same gap already exists today for a call cancelled after dispatch.**
`PreAdmissionProcessor` only skips *unsent* notices of cancelled calls. This fix closes both
cases through one path.

**Only an approved caller cancellation ends the call with nobody coming.** A desk cancel,
decline or reassign puts the call back to `received` for another ambulance, so its admission
must stay.

**New `PreAdmissionStatus` values:**

| Status | Meaning |
| :--- | :--- |
| `Withdrawn` | The admission is cancelled, or was never created |
| `Withdrawing` | Sent already; a withdrawal is waiting to go out |
| `WithdrawalFailed` | Patient refused the cancel, e.g. the patient was already admitted |

**Inside `EndAtSceneAsync`, and inside the approved-cancellation path**
(`IPreAdmissionWithdrawals.RequestAsync`, saved with the caller's own change):
- Notice `Queued` **or** `Sent` → set `Withdrawing`, store the reason, restart the attempt count.
  A queued notice is not shortcut to `Withdrawn`: the worker might be sending it at that very
  moment, and the admission it creates must still be cancelled.
- `PreAdmissionNotice` has a row version, so a send that was already in flight cannot overwrite
  the withdrawal. The worker's save fails, the next pass sees `Withdrawing` and cancels the
  admission the send just made.

**`PreAdmissionProcessor` also processes `Withdrawing`.** It calls a new
`IPreAdmissionGateway.WithdrawAsync(dispatchId, reason)`:
- `Withdrawn` on success, when no admission exists, or when it is already cancelled.
- Retry with the same growing gaps on `Unavailable`.
- `WithdrawalFailed` on a refusal. That is logged at `Warning`, not retried.

Because it runs in the background worker, **a Patient Management outage never blocks the crew
finishing a run** — the same rule Phase 9 set for pre-admit.

**Cancel reasons sent to Patient:**

| Why | Patient `CancelReason` |
| :--- | :--- |
| `PatientRefused` | `PatientRefused` |
| `FalseAlarm` | `FalseAlarm` |
| `PatientNotFound` | `NoShow` |
| `TreatedAtScene` | `TreatedAtScene` (new) |
| `PatientDeceased` | `DiedAtScene` (new) — `DiedEnRoute` would be recorded wrong |
| Call cancelled after dispatch | `CallCancelled` (new) |

**Patient's side (added here).** `IAdmissionService.FindByDispatchIdAsync`, and the three new
`CancelReason` values in the enum, the Patient spec, the entity diagram and the web label maps.
`PreAdmissionGateway.WithdrawAsync` finds the admission by dispatch and calls Patient's own
`CancelAsync`, so Patient's rules decide what can still be cancelled. No stub is involved.

### Patient's tracking screen

- `MyCallTracking` gains `transported`.
- A completed call with `transported = false` reads "The crew finished at the scene" instead
  of "Response completed".
- **The reason is not shown to the caller.** "Patient died" belongs to a person, not a status
  line.

### Web (duty manager)

`domain.ts` — `ended_at_scene` in `dispatchStatusLabels` ("Finished at scene") and
`dispatchStatusTones`. The call detail also gets a "How it ended" field (reason and notes)
whenever the call finished at the scene. The label maps are keyed by the generated enum, so forgetting it is a
compile error.

### Edge cases

| Case | What happens |
| :--- | :--- |
| Tapped at `transporting` by a stale screen | 409 illegal move → reload shows the real state |
| The partner taps "Patient on board" at the same moment | The row version check lets one win; the other gets 409 and the panel or new state |
| Patient already admitted before withdrawal (very late) | `WithdrawalFailed`, logged. The admission is real and stays. |
| Response-time report | Unaffected — it measures time to the scene |
| Fleet utilisation report | Uses `CompletedAt`, which is set |
| The dispatch agent | Sees an available ambulance with no live run — the normal case |

### Tests

- Backend:
  - legal only from `at_scene`
  - the ambulance becomes free
  - `Transported` is false, or true on handover
  - the validator rejects a missing outcome and notes over 1000 characters
  - the withdrawal path: queued, sent, patient refuses, Patient Management down then back
  - a call cancelled after the pre-admit was sent is now withdrawn too
  - a migration exists for the model change (the CI contract job checks this)
- Flutter: the sheet disables Finish until a reason is picked; the controller calls the new
  endpoint.
- Web: the new label renders.

---

## Fix 5 — The handover reaches people

**Problem:** `HandoverNotes` and `PatientCondition` are saved, but no screen shows them. The
duty manager's history shows only ambulance, status and crew count. The ward never sees them.

### What changes

**Duty manager's call detail** — each run card shows how it ended:

```
AMB-3 · Handed over · 10:52
  Condition on arrival: Conscious, breathing, BP low
  Notes: Fall from ladder, left leg splinted
```

Or it shows "Finished at scene — Treated at the scene", or the cancel, reassign or decline
reason.

**The crew can write notes on the way.**
- A **"Write handover notes"** button shows from `at_scene` onwards.
- Typing is saved as a draft in `MyRunController`, one draft per run.
- "Hand over at the hospital" opens the same sheet, pre-filled, with **Confirm handover**.
- One crew member types in the back while the other drives. Closing the sheet loses nothing.

### Backend

- `EmergencyCallDetail.dispatches` changes from `DispatchSummary[]` to a new `CallDispatch[]`.
  - `CallDispatch` extends `DispatchSummary`, so today's web code keeps compiling.
  - It adds the acknowledgement, the three reasons, the superseded link and the handover.
  - `DispatchDetail` (the crew's view) now extends `CallDispatch` and adds the scene and caller.
    Plan change: the first draft put `DispatchDetail` on the desk, which would have repeated the
    scene and caller once per run on a payload that already carries them.
  - One mapping (`DispatchMapping`) builds both, so the fields cannot drift apart.
  - Blank handover text is stored as nothing, so a screen never shows an empty value.
- **For Patient:**
  - New `IDispatchService.FindHandoverAsync(string dispatchId)` returns an `AmbulanceHandover`:
    registration, handed-over time, condition, notes.
  - It takes the **first** dispatch's id, because that is the one Patient was given (Phase 9:
    one pre-admission per call). It then finds whichever dispatch for that call was handed
    over.
  - Recorded in `integration_of_functions.md` as §4.4.
  - Plan change: nobody else was going to write the consumer, so it was done here.
    `AdmissionDetail.ambulance_handover` is filled from it, and the visit panel in
    `PatientsPage` shows condition and notes.

### Phone app (Flutter)

- `HandoverDraft` in the controller.
- The sheet takes an initial draft and hands back edits on close.
- The draft is kept in memory only. If Android closes the app, the draft is lost.
  - This is a stated limit, not a bug. `pubspec.yaml` has no local storage package, and adding
    one is a group decision.

### Edge cases

- A draft for a run that then ends another way (for example, finished at the scene) → thrown
  away with the run.
- A diverted run's draft does not carry over to the new run. The draft is tied to the run's id.
- An empty handover is still allowed. The fields are optional today, and a crew at the door
  must never be blocked by a form.

### Tests

- Web: `call-detail` renders each way a run can end. The ward panel in `PatientsPage` has no
  test setup, so the ward side is covered by the backend test on `AdmissionDetail`.
- Backend:
  - `FindHandoverAsync` after a reassignment still finds the handed-over dispatch
  - it returns null before a handover
- Flutter: the draft is kept across closing and reopening, and cleared when the run ends.

---

## Fix 6 — Safer step buttons, smarter navigation

**Problem:** one tap on a big button in a moving vehicle is permanent — there is no step back.
Navigation is a separate button that does not say where it goes.

### Changes

**Confirm by tapping twice, for the two steps that are easy to hit by accident.**
- "I have reached the scene" and "Patient on board" first turn amber: "Tap again to confirm".
- After 4 s it resets.
- It also resets when the status changes underneath.
- Screen readers get the same through the button's label.
- **Accept** stays one tap: accepting fast is the point, and declining has its own path.
- **Why not a pop-up or press-and-hold:** a pop-up needs a second target to aim at, and
  press-and-hold is hard in a bumping vehicle. Tapping twice in the same place is quick and
  deliberate.

**Navigation follows the run.**
- After **Start driving** succeeds, Google Maps opens to the scene automatically.
- After **Patient on board** succeeds, it opens to the hospital entrance.
- The manual button stays, labelled by where it goes:
  - "Navigate to scene" before the patient is on board
  - "Navigate to hospital" once transporting
  - This matches the server rule in `GetMyNavigationTargetAsync`.
- The phone mirrors the server's state machine, as the frontend rules require.

**Record where each step happened.**
- `advance()` sends the reporter's latest fix, if it is under 30 s old.
- `UpdateMyDispatchStatusRequest` already accepts latitude and longitude. The phone just never
  sent them.

**A tap whose answer got lost.** The request reached the server but the reply did not, so the
retry gets a 409.
- In `_act`, after a conflict, reload.
- If the run is now in the state the tap asked for, treat it as success and clear the error.
- The same for acknowledge, and for handover (the run is gone and `getMyDispatch` says
  `handed_over`).

**No signal.**
- A request with no reply shows "No connection — this step was not saved" with **Try again**.
- Try again repeats the same step.
- Never report a step as done unless the server said so.

**How it is built.**
- Each tap is tied to the run it was made on. A retry, or a later poll, can never move a
  different run, and can never take a further step than the one that failed.
- Google Maps opens when the run *reaches* "on the way to the scene" or "taking the patient to
  hospital", so **Try again** opens it too.
- The reporter keeps the newest fix (`recentPosition`), and forgets it when reporting stops.

### Tests

`run_card_test.dart` and `my_run_controller_test.dart`:
- the second tap is needed
- the 4 s reset
- the reset on a status change
- navigation opens after the two steps
- a lost reply becomes success
- Try again repeats the step

---

## Fix 7 — Card polish

| Now | After |
| :--- | :--- |
| `Priority: critical` (the raw value) | A coloured chip: **Critical** / High / Medium / Low |
| "Going to ward: Not set" — always, because nothing ever sets `DestinationWardId` | The row is removed. Emergency sends no ward (Phase 9). |
| "Crew on board: 2" | Removed — useless to the crew themselves |
| Nothing about time | "Sent 3 min ago" under the status, ticking every minute |
| Decline asks for typed text | Choices: Vehicle problem · Crew not complete · Already busy · Other. Typing only for Other. Stored as the same `reason` text, so no API change. |
| Idle: "No run right now" | "You're on **AMB-3**. Ready for the next run." — or "You're not on an ambulance crew right now. Ask the duty manager." when not assigned. Uses the existing `getMyAmbulanceAssignment`. |

Tests: `run_card_test.dart` for each row.

---

## Fix 8 — Prove it on a real phone

Tests cannot prove that a push arrives or that Android keeps the location service alive. On a
real Android phone, with the server's Firebase key in place (`notifications.md` §11):

1. **New run:** it rings loudly with the app closed, and tapping it opens My run.
2. **Background location:** accept, start driving (Maps opens), lock the screen. On the desk's
   fleet map and the patient's tracking screen, the dot keeps moving.
3. **Cancel from the desk while driving:** a push arrives, and the panel explains it.
4. **Diversion:** the panel shows it, and the new run waits for Accept.
5. **Finish at scene:** the ambulance becomes available, and the ward's admission is cancelled.
6. **Handover:** the notes show on the desk's call detail.
7. **Airplane mode on "I have reached the scene":** "not saved" + Try again. Then turn it off
   and retry: success, or a lost reply that is recognised as success.

Write down what happened in `RESUME.md` and update each fix's status here.

---

## Not in this plan

- **The patient tracking map and arrival time.** `EstimatedMinutesToArrival` is always null
  today. It is worth its own plan once Fix 1 makes the location trustworthy.
- **Keeping the screen awake during a run.** It needs a new package, which is a group
  decision.
- **Desk buttons to cancel or reassign a run.** The endpoints exist, but no React screen calls
  them. That is a desk plan, not a crew one.
