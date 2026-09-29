# Patient app password reset — test log (source notes for the SE3110 report)

Step 21 in `docs/build/patient.md`: the hospital gives a patient who forgot their app password a
temporary one, and the app makes them choose their own before anything else. Same columns as
`patient-management-e2e-test-log.md`.

## Test environment

| Item | Value |
| :-- | :-- |
| Date | 2026-09-28 |
| Code | branch `feat/patient-password-reset` off `main` at `bd654a3`, uncommitted |
| API | ASP.NET Core 8, `http://localhost:5231`, local PostgreSQL dev database, migration `Common_AddPatientAccountMustChangePassword` applied |
| Staff web app | React + Vite, `http://localhost:5174` (its `/api` proxy was used for the API walk-through) |
| Patient app | Flutter web, `http://localhost:5173` |
| Browser | Chrome, driven through Claude in Chrome; screenshots taken at each step |
| Accounts | Seeded staff from `TEST_ACCOUNTS.md`; a test patient made for this pass (below) |

## Test Execution Summary

| | Count |
| :-- | :-- |
| Automated server tests (new) | 14 in `PatientAppAccountEndpointTests` + 3 contract checks — all pass |
| Full server suite | **1361 / 1361** pass (xUnit + Testcontainers Postgres) |
| Web (Vitest) | new `PatientAppAccountsPage.test.tsx` 3/3; full suite **210 / 210**; `typecheck` and `build` pass |
| Mobile (flutter_test) | new `forced_password_change_test.dart` 5/5; full suite **166 / 166**; `flutter analyze` clean |
| Manual API walk-through on the dev stack | **12** cases, all pass (TC-PWR-01 … 12) |
| Manual click-through, web + phone | **10** cases, all pass (TC-PWR-20 … 29) |
| Defects found | **4** (R1–R4); R1 and R4 fixed, R2 documented, R3 not this change |

## Test Case Document

### TC-PWR-01 to 12 — Reset through the running API (dev database, via the web app's `/api` proxy)

| ID | Scenario | Steps | Expected | Actual | Result |
| :-- | :-- | :-- | :-- | :-- | :-- |
| TC-PWR-01 | Ward nurse and doctor cannot open the list | `GET /patient-accounts` as each | 403 | 403, 403 | Pass |
| TC-PWR-02 | Find by name | desk, `search=Nirosha` | the one row, five fields | Nirosha Walkthrough, NIC, 1992-02-12, P9H8SXF5, reset.walkthrough | Pass |
| TC-PWR-03 | Find by NIC | `search=199254300123` | same row | same row | Pass |
| TC-PWR-04 | Find by username, any case | `search=RESET.WALK` | same row | same row | Pass |
| TC-PWR-05 | Find by patient code | `search=P9H8SXF5` | 1 match | 1 | Pass |
| TC-PWR-06 | Generate a new password | desk, `POST /patient-accounts/{id}/reset-password` | 200, `no-store`, `AAAA-9999` with no I/L/O | 200, `no-store`, shape matches | Pass |
| TC-PWR-07 | Forgotten password stops working | patient login with the old password | 401 | 401 `cl_err_401` | Pass |
| TC-PWR-08 | Sessions held before the reset end | refresh with a token issued before | 401 | 401 | Pass |
| TC-PWR-09 | Temporary password signs in, flagged | patient login with it | 200, `must_change_password: true` | as expected | Pass |
| TC-PWR-10 | Flag enforced by the server | flagged token: `GET /me/profile`, `GET /auth/me` | 403 `cl_err_004`; 200 | 403 "Choose a new password before continuing."; 200 | Pass |
| TC-PWR-11 | Choosing their own password | `POST /auth/password` with the temporary one as current | 204; temporary one then refused | 204; 401 | Pass |
| TC-PWR-12 | Back to normal | login with the new password, `GET /me/profile` | flag false, 200 | false, 200 | Pass |

Also checked: the temporary password appears nowhere in the API log (0 lines).

### TC-PWR-20 to 29 — Click-through in the real apps

| ID | Scenario | Steps | Expected | Actual | Result |
| :-- | :-- | :-- | :-- | :-- | :-- |
| TC-PWR-20 | Reset password in Manage account | Profile → Manage account → Reset password | sheet with hospital number and email | shown (after R4 fix, sized to its content) | Pass |
| TC-PWR-21 | Forgot password on sign-in | Sign in → Forgot password? | same sheet | same sheet | Pass |
| TC-PWR-22 | Page in the menu for reception | sign in as reception | **Patient app accounts** under Patient care | there | Pass |
| TC-PWR-23 | Search as you type | type `reset.walk` | list narrows to the one patient, five columns | Nirosha Walkthrough, NIC, 1992-02-12, P9H8SXF5, reset.walkthrough | Pass |
| TC-PWR-24 | Confirm first | Generate new password | warning naming the patient, NIC and date of birth | "Generate a new password for Nirosha Walkthrough? … Check their NIC and date of birth first." | Pass |
| TC-PWR-25 | Password shown once | confirm | username and temporary password large, Copy, "shown once" | as expected | Pass |
| TC-PWR-26 | Nothing kept after closing | Done, then open the same row again | fresh confirmation, no old password | fresh confirmation | Pass |
| TC-PWR-27 | Temporary password forces the change | phone: sign in with it | **Choose a new password**, no back arrow, Sign out | as expected | Pass |
| TC-PWR-28 | Cannot get round it | type `#/me` in the address bar (full reload) | back on the forced screen | back on the forced screen | Pass |
| TC-PWR-29 | Choose own, sign in again | current = temporary, new = own → Change password → sign in with the new one | "Password changed…", signed out, then home | as expected, home shows "Nirosha" | Pass |

### Automated cases (plan section 6)

`tests/CareLanka.Api.Tests/PatientAppAccountEndpointTests.cs` — list shows only linked patients
with the five fields and finds each by name, NIC, code and username; reset replaces and flags;
every earlier refresh token revoked; flagged token limited to `/auth/me` and `/auth/password`;
flag gone after the change; 409 `cl_pat_053` with no app account, 404 for an unknown patient;
nurse and doctor 403 on both routes; patient token 403, no token 401; a patient locked out by
five wrong guesses can sign in with the temporary password; shape `^[A-HJKMNP-Z]{4}-\d{4}$` and
two resets differ; `Cache-Control: no-store`; `page=0` and `pageSize=101` are 400 without the
service being called. Contract: both operationIds and their declared failures; the two new
schemas' required members; `CurrentPrincipal.must_change_password` required.

`web-ui/src/pages/PatientAppAccountsPage.test.tsx` — the table shows the five fields; the
password appears only after confirming; hidden from ward nurse and doctor.

`mobile-ui/test/core/routing/forced_password_change_test.dart` — a flagged patient is sent to
**Choose a new password** and cannot leave it for another route; the forced screen has no back
arrow and offers Sign out; without the flag `/change-password` goes home; **Forgot password?**
and **Reset password** both open the sheet with the hospital number.

## Defect / Bug Report

| ID | Found by | What went wrong | Severity | Fix | Retest |
| :-- | :-- | :-- | :-- | :-- | :-- |
| R1 | Building §5.1 | The plan moves the call-the-hospital box into `core/`, but its look came from `NoticeBanner` in `features/patient/`, which `core/` may not import | Medium (blocked the plan as written) | `NoticeBanner` moved to `core/widgets/notice_banner.dart` (decided by Lochana); six patient files updated | Pass — `flutter analyze` clean, 166/166 |
| R2 | API walk-through | `demo.emergency` is not linked to a record in this dev database (seed 006 not run), so it does not appear on the new page | Low (setup) | `TEST_ACCOUNTS.md` now says it is listed once seed 006 has run | — |
| R3 | Full web suite | Three Emergency tests hit the 5 s Vitest timeout while other builds were running; different ones on each run | Low (flaky under load, not this change) | None | Pass — 24/24 alone, full suite 210/210 once the machine was quiet |
| R4 | Click-through | The reset-password sheet stretched its help box down into empty space instead of fitting the text | Low | Sheet opened the way the app's other sheets are (`isScrollControlled`, content sized to fit) | Pass — TC-PWR-21 |

## Test data created by this pass (dev database)

App login `reset.walkthrough` (password now `Walkthrough#2027`), linked to patient `P9H8SXF5`
Nirosha Walkthrough (NIC `199254300123`, born 1992-02-12).
