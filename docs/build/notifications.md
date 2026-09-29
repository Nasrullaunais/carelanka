# Build Track — Notifications (common)

**Owner:** Nasrulla Unais, with the group's permission to add the triggers inside all four components.

**Status:** Phases 1 to 10 are built, 2026-09-30, except Phase 9's by-hand step: putting the Firebase key on the server (§11) and confirming a real push. Three catalogue types are reserved and not raised: `pharmacy_stock_low` and `lab_test_requested` (see §5), and `appointment_rescheduled` (there is no reschedule action).

**Contract:** `specs/common-spec.yaml` (the inbox endpoints are added there — notifications are common, not one member's).

**Before this track:** only one event notified anyone — a crew assigned to a run (`DispatchService.cs`). The hosted server has no Firebase key and phone builds have no `google-services.json`, so even that alert never reaches a phone.

---

## 1. What we are building

Patients and staff are told when something that concerns them happens, anywhere in the system.

| Where | How it arrives |
| :--- | :--- |
| Android phone (staff and patient) | Push pop-up, even when the app is closed |
| Phone app | Inbox behind a bell icon, read / unread |
| Website (staff) | Inbox behind a bell icon, **updated instantly** over a live connection, plus a toast |

Decisions settled on 2026-09-27:

| Question | Decision |
| :--- | :--- |
| Channels | Push on phones + inbox in both apps. No browser push. |
| Who builds | This track builds everything, including the triggers inside each component. |
| Web speed | Live connection (SignalR). Catches up after a reconnect by re-reading the inbox. |
| iPhone | Inbox only. No push until someone has an Apple developer account. |
| Turning types off | Not in this version. Android's own permission prompt is handled properly. |
| Email / SMS | No. The channel design leaves room for them. |
| Role-wide alerts | Staff on shift for that ward now; everyone in the role if nobody is. |
| Lock-screen text | Full detail. One setting switches to general text (see §6). |
| Inbox kept for | 90 days, then deleted automatically. |

**Out of scope:** per-user settings, iPhone push, email, SMS, browser push.

---

## 2. How it works, end to end

```text
A service changes something (e.g. approves an admission)
        ↓
It calls INotifier.Notify(...) — nothing is sent yet
        ↓
One SaveChanges writes the change AND the notification rows together
  (if the change fails, no notification exists; if it succeeds, one always does)
        ↓
    ┌───────────────────────────────┬──────────────────────────────────┐
    ↓                               ↓                                  ↓
Inbox row (the record)     Live signal to the website        Push delivery row
read by both apps          sent right after the save          sent by the background
                           (instant, best effort)             worker, retried if Firebase fails
```

Why this shape:

- **Saved together, sent later.** Sending to Firebase in the middle of a request would
  either slow the request or send an alert for a change that then failed to save. Writing
  the notification in the same save as the change, and sending it afterwards from a worker,
  means every saved change gets exactly one notification. This is the "outbox" pattern, and
  the existing push code already works this way — this track widens it.
- **The inbox is the truth; push and the live signal are nudges.** If a push is lost or the
  website was closed, the inbox still has it.

---

## 3. Data model

The current `notifications` table mixes two things: *what the person is told* and *one
attempt to send it to a phone*. It also only allows a staff member as recipient. It is
split into two tables.

### 3.1 `notifications` — the inbox

| Column | Notes |
| :--- | :--- |
| `id` | |
| `recipient_staff_member_id` | nullable FK → `staff_members` |
| `recipient_patient_account_id` | nullable FK → `patient_accounts` |
| `type` | `NotificationType` enum, snake_case (ADR 5) |
| `title`, `body` | Rendered text, stored, so old items still read correctly after wording changes |
| `entity_type`, `entity_id` | What tapping it opens |
| `dedupe_key` | Unique. Stops the same alert being created twice |
| `read_at` | null = unread |
| `created_at` | from `AuditedEntity` |

- **Check constraint: exactly one recipient column is set.** Two real foreign keys, not one
  loose "recipient id" column, so the database refuses a notification for someone who does
  not exist.
- Indexes: `(recipient_staff_member_id, created_at desc)`, `(recipient_patient_account_id, created_at desc)`,
  and a partial index on unread rows for the badge count.

### 3.2 `notification_deliveries` — one row per push to send

| Column | Notes |
| :--- | :--- |
| `id`, `notification_id` | FK → `notifications`, cascade delete |
| `channel` | `NotificationChannel` (only `push` today; room for `email`/`sms`) |
| `status` | `queued` → `sent` / `failed` |
| `attempt_count`, `next_attempt_at`, `sent_at`, `failure_reason` | Moved here from `notifications` |

The partial index `ix_notification_deliveries_due` (`WHERE status = 'queued'`) replaces
`ix_notifications_due`.

### 3.3 `device_tokens`

Add `patient_account_id` beside `staff_member_id`, both nullable, same exactly-one check.
When a phone signs in as a different person, its token moves to the new person and the old
registration is revoked — a shared ward phone must never show the last user's alerts.

### 3.4 `NotificationType`

One enum value per event in §5 (`appointment_booked`, `admission_awaiting_approval`, …).
Title and body text live in a resource file keyed by type, with `{0}` parameters — the same
approach as `ErrorMessages.resx`, so Sinhala/Tamil later is a resource change.

### 3.5 Migration

`Common_SplitNotificationDeliveries`. Existing rows are copied into the new shape inside the
migration (DDL plus a data move, no seed data). Pull `main` right before generating it.

---

## 4. Backend

### 4.1 One way in: `INotifier`

Replaces `IPushNotifications`. Services never build notification rows themselves.

```csharp
public interface INotifier
{
    void Notify(NotificationType type, Recipients to, NotificationSubject subject, params object[] args);
}
```

- **Stages only.** Adds rows to the `DbContext`; the caller's own `SaveChanges` commits them.
- `NotificationSubject` = `(entityType, entityId)`, used for the tap target and the dedupe key.
- Dedupe key = `type:entityId:recipient[:occurrence]`. A second call for the same thing is
  ignored, so a retried request cannot double-notify.
- Renders `title`/`body` from the resource file at staging time.

### 4.2 Who receives it: `Recipients`

```csharp
Recipients.Staff(staffMemberId)
Recipients.Patient(patientId)                 // looks up Patient.UserAccountId
Recipients.Role(StaffRole.DutyManager, wardId) // on shift for that ward, else whole role
Recipients.Role(StaffRole.DutyManager)         // whole role, hospital-wide events
```

Resolved inside `INotifier` by an `IRecipientResolver`:

- **Patient** → the patient's `UserAccountId`. A patient with no login gets no notification,
  and that is correct, not an error.
- **Role on a ward** → staff with an `Allocation` on a `Shift` for that ward whose time window
  covers now, excluding ended or cancelled allocations. Read through a new Staff method,
  `AllocationService.FindOnShiftAsync(wardId, role, at)` — Staff owns that table, so nobody
  else queries it directly (`integration_of_functions.md` §3).
- **Fallback** → nobody on shift → every active staff member with that role.
- The person who caused the event is never notified about their own action.

### 4.3 After the save: live signal

A `SaveChangesInterceptor` (`NotificationSavedInterceptor`) collects the recipients of
notifications added in this save, and **after** the save commits, tells the SignalR hub
"your inbox changed". If the save fails, nothing is announced.

### 4.4 SignalR hub

- `NotificationsHub` at `/api/hubs/notifications`, `[Authorize]`.
- Each connection joins a group for its own user (`staff:{id}` or `patient:{id}`), taken
  from the JWT — never from anything the client sends.
- The browser cannot put a header on a WebSocket, so the JWT is read from the
  `access_token` query string **for the hub path only** (`JwtBearerEvents.OnMessageReceived`).
- It sends one message, `inboxChanged`, **with no data in it**. The client re-reads the inbox
  through the generated REST client. So there is no hand-written model anywhere, and the
  "clients are generated" rule holds.
- One server today, so no backplane. Moving to several servers would need Redis — noted in
  the ADR, not built.

### 4.5 Push delivery

`PushDeliveryProcessor` reads `notification_deliveries` instead of `notifications` and looks
up tokens for whichever recipient column is set. Retry, back-off, token revocation and
give-up stay as they are. Two Android channels:

| Channel | Used for | Behaviour |
| :--- | :--- | :--- |
| `urgent_alerts` | Crew assignment, new emergency call, urgent care flag | High priority, sound |
| `general` | Everything else | Default priority |

`NotificationType` carries its channel, so the choice is made once.

### 4.6 Inbox API (`specs/common-spec.yaml`)

Every route acts on the **caller's own** inbox, staff or patient alike.

| Method | Route | operationId |
| :--- | :--- | :--- |
| GET | `/api/notifications?unreadOnly&page&pageSize` | `listMyNotifications` |
| GET | `/api/notifications/unread-count` | `getMyUnreadNotificationCount` |
| POST | `/api/notifications/{id}/read` | `markNotificationRead` |
| POST | `/api/notifications/read-all` | `markAllNotificationsRead` |

- Schema `InboxNotification` (not `Notification`, which is too generic to stay unique across specs).
- Someone else's notification id → **404**, not 403, so ids cannot be probed.
- Query model + FluentValidation validator; `[ProducesResponseType]` for every outcome.
- Run the collision sweep (`check:specs`) before committing.

### 4.7 Scheduled jobs

One `NotificationScheduleWorker` (a `BackgroundService`, same pattern as `PushDeliveryWorker`):

| Job | Runs | Does |
| :--- | :--- | :--- |
| Appointment reminders | every 15 min | Appointments starting in ~24 h that are still booked → reminder. The dedupe key makes repeat runs harmless |
| Maintenance due | hourly | Schedules due today → equipment managers |
| Inbox clean-up | daily | Deletes notifications older than 90 days (deliveries go with them) |

Times use Sri Lanka time for "tomorrow" (`HospitalTime`), stored as UTC.

### 4.8 Settings (`appsettings.json` → `Notifications`)

| Key | Default | Meaning |
| :--- | :--- | :--- |
| `LockScreenDetail` | `Full` | `Generic` swaps every push's text for "You have a new update from CareLanka" |
| `RetentionDays` | `90` | Inbox clean-up age |
| `ReminderLeadHours` | `24` | Appointment reminder timing |

Existing `Push:*` settings stay as they are.

---

## 5. Event catalogue

Each row is one `NotificationType` and one `Notify` call placed in the service method that
makes the change, **inside the same save**. Wording is final in the resource file.

### 5.1 To patients

| Type | Trigger (service) | Channel |
| :--- | :--- | :--- |
| `appointment_booked` / `_rescheduled` / `_cancelled` | `AppointmentService` | general |
| `appointment_reminder` | schedule job (§4.7) | general |
| `admission_approved` | `AdmissionService` | general |
| `bed_assigned` | `BedAssignmentService` | general |
| `discharge_ready` | `DischargeService` | general |
| `bill_raised` / `bill_settled` | `BillingService` | general |
| `care_reply_ready` | `CareRecommendationService` (on approve) | general |
| `prescription_ready` / `_delivered` | `PrescriptionService` | general |
| `lab_report_ready` | `LabReportService` | general |
| `ambulance_on_the_way` / `_arrived` | `DispatchService` | urgent |
| `cancellation_answered` | `EmergencyCallService` | general |

### 5.2 To staff

| Type | Trigger (service) | To | Channel |
| :--- | :--- | :--- | :--- |
| `emergency_call_received` | `EmergencyCallService` | Duty managers | urgent |
| `dispatch_assigned` *(exists)* | `DispatchService` | The crew | urgent |
| `cancellation_request_waiting` | `EmergencyCallService` | Duty managers | general |
| `dispatch_proposal_waiting` | `DispatchProposalExecutor`, diversion needing approval only | Duty managers | urgent |
| `dispatch_proposal_failed` | `DispatchProposalExecutor`, no ambulance recommended | Duty managers | urgent |
| `admission_awaiting_approval` | `AdmissionService` | Duty managers, that ward | general |
| `care_query_flagged` | care agent red-flag screen | Nurses + doctors, that ward | urgent |
| `care_reply_waiting` | `CareRecommendationService` | Nurses + doctors, that ward | general |
| `equipment_warning_raised` | `WarningService` / `WarningSweepWorker` | Equipment managers | general |
| `maintenance_due` | schedule job | Equipment managers | general |
| `pharmacy_stock_low` | **Reserved, not raised.** Low stock is already reported inside `equipment_warning_raised` by the warning sweep's low-stock rule | Equipment managers | general |
| `lab_test_requested` | **Reserved, not raised.** There is no way to request a lab test yet; it needs that feature first | Equipment managers (lab) | general |
| `leave_requested` | `LeaveRequestService` | Duty managers | general |
| `leave_approved` / `_rejected` | `LeaveRequestService` | The requester | general |
| `shift_changed` | `AllocationService` | The staff member | general |
| `roster_proposal_waiting` | `RosterProposalService` | Hospital administrators | general |

Confirm each "To" against `docs/CareLanka_Component_Plan.md` §roles while building — if a
role name there differs, the component plan wins.

---

## 6. Privacy note (lock screen)

The group chose full detail on the lock screen. **Say this plainly at the viva:** anyone near a
locked phone can read "Lab report ready: …". A real hospital would likely have to use
general text for anything about someone's health. Setting `Notifications:LockScreenDetail`
to `Generic` does that with no code change — the inbox, which needs a sign-in, keeps the detail.
Record the choice and the reason in the ADR (§9).

---

## 7. Web (`web-ui/`)

- **Bell in the app shell**: unread count badge (`getMyUnreadNotificationCount`) and a
  dropdown list (`listMyNotifications`), both through the generated TanStack Query options.
- **Inbox page** `/notifications`: full list, unread filter, mark all read.
- **Live connection** in `src/services/realtime/notifications.ts`, using `@microsoft/signalr`:
  - Connects after sign-in, token from the session, disconnects on sign-out.
  - Built-in automatic reconnect; **on every reconnect, invalidate the inbox queries**, so
    anything missed while offline shows up.
  - On `inboxChanged`: invalidate inbox queries, then toast the newest unread item's title.
  - Must not import from `runtime.ts`'s chain back into the generated client (startup-cycle rule).
- **Clicking an item** marks it read, then routes by `entity_type` → page, through one map in
  `src/types/notifications.ts` keyed by the generated `NotificationType` enum, so a new type
  without a route is a compile error.
- Vite proxy: add `ws: true` to the `/api` entry. Caddy already passes WebSockets through.

---

## 8. Mobile (`mobile-ui/`)

Shared code goes in `lib/core/notifications/` (group permission given for `core/` and `pubspec.yaml`).

- **Permission**: ask on first sign-in for **staff and patients** (Android 13+ requires it).
  If refused: the app works normally, the inbox still fills, and a small banner on the inbox
  offers "Turn on notifications", which opens system settings. Never ask again on every launch.
- **Registration**: `PushRegistration` covers patients too (drop the `isStaff` condition).
  Unregister on sign-out, as today.
- **Android channels**: create `urgent_alerts` and `general` at start-up (§4.5), replacing `dispatch_alerts`.
- **Message open in foreground**: listen to `FirebaseMessaging.onMessage`; show an in-app
  banner and refresh the inbox instead of a system pop-up.
- **Tap handling**: cold start (`getInitialMessage`) and background (`onMessageOpenedApp`)
  both route by `entity_type` / `entity_id` through one route map, then mark read.
- **Inbox screen** with a bell + badge in the app bar, pull to refresh, mark read. Uses the
  generated `api_client` only.
- The app refreshes the inbox on resume — the phone does not need the live connection;
  push already makes it instant.

---

## 9. Documents that change with this

In the same commits as the code, per the repo rules:

| Document | Change |
| :--- | :--- |
| `specs/common-spec.yaml` | Inbox endpoints, `InboxNotification`, `NotificationType`, `RegisterDeviceRequest` covers patients |
| `docs/entity_diagram.md` | `notifications`, `notification_deliveries`, `device_tokens` |
| `specs/integration_of_functions.md` | `INotifier` as a common service every component calls; `AllocationService.FindOnShiftAsync` as a Staff read |
| `docs/ADR.md` | **ADR 9 — Notifications**: outbox + inbox split, SignalR over polling, single-server no-backplane, lock-screen choice |
| `docs/build/common.md` | Point to this file |
| `deploy/README.md`, `deploy/compose.yaml` | Firebase setup (§11) |
| Emergency/Patient/Equipment/Staff build docs | One line each: which events their component raises |

---

## 10. Build order

Each phase ends with everything green: `dotnet test`, `check:specs`, `check:codegen`,
`typecheck`, `flutter analyze`, `flutter test`.

| # | Phase | Done when |
| :--- | :--- | :--- |
| 1 | **Data model + migration** (§3) | Existing dispatch alert still works on the new tables; check constraints tested |
| 2 | **`INotifier` + recipients** (§4.1–4.2), move `DispatchService` onto it, delete `IPushNotifications` | Tests: dedupe, patient without login, on-shift vs fallback, actor excluded, rolled-back save leaves no notification |
| 3 | **Inbox API** (§4.6) + regenerate both clients | Integration tests: own inbox only, other's id → 404, invalid query → 400, unread count |
| 4 | **Web inbox** — bell, list, page (§7, without live) | Works by refreshing |
| 5 | **Live connection** — hub, interceptor, web client (§4.3–4.4, §7) | Two browsers: approve in one, bell updates in the other with no refresh; reconnect catches up |
| 6 | **Mobile** — patient registration, permission flow, channels, inbox, taps (§8) | On a real Android phone: foreground, background and cold-start taps all land on the right screen |
| 7 | **Triggers** — the §5 catalogue, one component at a time: Emergency → Patient → Equipment → Staff | One test per type: the action creates exactly one notification for the right people |
| 8 | **Scheduled jobs** (§4.7) | Tests with a fake clock: reminder once, clean-up at 90 days |
| 9 | **Firebase + deploy** (§11) | A real push reaches a real phone from the hosted server |
| 10 | **Docs** (§9) | ADR 9 written; spec sweep 0 collisions. **Done 2026-09-30** |

Phases 4–6 can run in parallel once 3 is merged. Phase 7 per component can start after 2.

---

## 11. Firebase setup (one-time, by hand)

1. In the Firebase console, create project **CareLanka**, add an **Android app** with
   package name `lk.carelanka.carelanka_mobile`.
2. Download `google-services.json` → `mobile-ui/android/app/`. **Never commit it** — share it
   with the group privately. Without it the app builds with push off.
3. Project settings → Service accounts → **Generate new private key** → a JSON file. This is
   the server's key. **Never commit it.**
4. On the server, put that file at `/opt/carelanka/secrets/firebase.json` (read-only to the
   container user), mount it in `deploy/compose.yaml`, and set
   `Push__CredentialsPath=/run/secrets/firebase.json` on the `api` service.
5. Restart the API. The log line "Push:CredentialsPath is not set" must no longer appear.
6. Install a release build that has `google-services.json`, sign in, trigger an event, and
   check the row in `notification_deliveries` reaches `sent`.

---

## 12. Risks

| Risk | What we do |
| :--- | :--- |
| Alert storm (e.g. warning sweep raises 50 warnings at once) | Dedupe key per warning; sweep uses one notification per sweep run listing the count |
| Wrong person on a shared phone | Token moves on sign-in, revoked on sign-out (§3.3) |
| Live connection drops silently | Reconnect re-reads the inbox; the inbox is always the truth |
| Notification text leaks health data | §6 — documented choice, one setting to change |
| Migration clashes with a teammate's | Pull `main` right before generating; regenerate on conflict, never hand-merge the snapshot |
