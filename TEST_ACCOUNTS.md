# Test accounts

Every account in a freshly seeded CareLanka database. **Required by assignment §15.**

They come from `docs/seed/001_identity.sql`. Nothing here is hard-coded in the
application — if you have not run that script, none of these exist. See `api/README.md`.

Demo passwords on a demo database. **Change every one before this points at anything real.**

---

## Staff

Sign in with an email: `POST /api/auth/login`

| Role | Email | Password |
| :--- | :--- | :--- |
| Reception (general staff) | `staff.jayasuriya@carelanka.lk` | `CareLanka#2026` |
| Ward nurse | `nurse.perera@carelanka.lk` | `CareLanka#2026` |
| Doctor | `dr.silva@carelanka.lk` | `CareLanka#2026` |
| Duty manager | `duty.rajapaksa@carelanka.lk` | `CareLanka#2026` |
| Hospital administrator | `admin.wickrama@carelanka.lk` | `CareLanka#2026` |
| Equipment manager | `equip.bandara@carelanka.lk` | `CareLanka#2026` |
| Ambulance crew | `crew.fernando@carelanka.lk` | `CareLanka#2026` |
| **Deactivated** | `former.gunasekara@carelanka.lk` | `CareLanka#2026` |

The last one exists to fail. The password is right and login still returns 401, the same
answer as a wrong password and an unknown email — so nobody can learn which addresses are
real by trying a list.

## Patients

Sign in with a username: `POST /api/auth/patient/login`

| Username | Password |
| :--- | :--- |
| `chathura.w` | `Patient#2026` |

This account has no `patients` row behind it, which is the ordinary state for a fresh
sign-up. `GET /api/auth/me` returns `patient_id: null` and every `/api/me/*` route except
`pre-register` answers 404 with `cl_pat_033` until the patient fills in their details.

Registration is open — `POST /api/auth/patient/register` with a username and a password
makes a new one and signs you straight in.

---

## What each role can do

### Reception (general staff)
Registers walk-in patients and edits their details · assigns a bed · marks a patient
arrived · raises, itemises and settles a bill · confirms a discharge · reads the bookings
list, wards and bed capacity.
**Cannot:** check a booking in, tick clinical clearance, or set prices.

### Ward nurse
Everything reception does, plus: admits a patient and moves the admission through its
states · checks a booked patient in · cancels a booking · completes missing patient
details · ticks the non-clinical discharge items · reads lab reports.
**Cannot:** tick clinical clearance, or set prices.

### Doctor
Ticks **clinical clearance** — the one item nobody else can tick, and no discharge happens
without it · reads patient details and lab reports · reads the discharge board.
**Cannot:** register, admit, bed, bill, or confirm a discharge.

### Duty manager
Everything reception and the ward nurse can do, plus the two things that need authority:
**assigning a bed that does not match the assessed care level** (either direction), and
**checking a patient in at ICU or HDU level**. Also handles emergency calls and ambulances,
and can start an agent workflow.

### Hospital administrator
**Sets prices** — the admission fee per care level and every ward's rates. Nobody else can.
**Creates and retires wards.** Reads patient details, the bookings list and the discharge
board. **Confirms new equipment** in the mobile app or on the web Equipment page — an item the
equipment manager registers only reaches the web register once the administrator confirms it.
Both ask for the confirmation code first: `equipment2026`. **Confirms maintenance done** the same way, in the
mobile app or on the web Maintenance unit page — every reported fault and scheduled job is listed
there, and confirming it puts the item back into service. **Runs the maintenance unit** on the web:
books maintenance, sees the open jobs, and retires a machine beyond repair.
**Cannot:** register, admit, bed, or discharge anyone.

### Equipment manager
Beds, equipment items, pharmacy and maintenance. **Files lab reports** — the only role that
can. Reads patient details and lab reports.
**Runs the pharmacy's prescription queue** on the web Pharmacy page: marks one ready (which issues
the patient's token), delivered, or can't fill.
**Cannot:** confirm an item they registered, or run the maintenance unit (booking, confirming or
retiring) — the hospital administrator does those. Reports a fault from the Equipment page.

### Ambulance crew
Emergency calls and ambulances.
**Cannot:** register or admit a patient — that is reception's job, not the crew's.

### Patient (mobile app only)
Their own record and nothing else: saves their details, books and cancels one visit at a
time, follows their current stay, and reads their discharge instructions afterwards. **Sends a
prescription** to the pharmacy from the Prescriptions tab and sees its collection token once it is
ready.
No `/api/me/*` route takes a patient id — every one resolves the record from the token, so
being handed somebody else's is impossible rather than merely checked.

---

## Walking the patient journey

It takes four people on purpose. One account will not do the whole thing.

| Step | Sign in as |
| :--- | :--- |
| Register and admit | Reception |
| Assign the bed | Reception, nurse or duty manager. Duty manager only if the ward does not match the care level |
| Mark them arrived | Reception, nurse or duty manager |
| Check in a booked visit | Nurse or duty manager. Duty manager for ICU or HDU |
| Tick clinical clearance | **Doctor** |
| Raise and settle the bill | Reception |
| Confirm the discharge | Reception, nurse or duty manager |

A children's ward only takes patients under 18, and a patient with no recorded date of
birth counts as an adult.

---

## Using a token

Sign in, copy `access_token`, send it as `Authorization: Bearer <token>`. In Swagger, click
**Authorize** and paste the token **without** typing `Bearer` in front — Swagger adds that
itself, and pasting it twice is the usual reason a token "does not work".

An access token lasts **15 minutes**. `POST /api/auth/refresh` with your `refresh_token`
gives you a fresh pair. Each refresh token works **once**; reusing an old one deliberately
ends every session that account has.

| What you see | What it usually is |
| :--- | :--- |
| `401` on a password you are sure about | The seed script has not been run |
| `401` on everything, suddenly | The access token is over 15 minutes old. Refresh it |
| `403`, not `401` | You are signed in fine. That role is not allowed to do that |
| `429` | Too many attempts. Wait a minute |
| `500` on anything that reads data | PostgreSQL is not reachable. `/api/health` says so outright |
