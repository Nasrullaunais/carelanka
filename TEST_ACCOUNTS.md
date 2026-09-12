# Test accounts and how to sign in

Every account that exists in a freshly seeded CareLanka database, what it is for,
and how to actually use one.

**Assignment §15 requires test accounts in the submission.** This is that list.

These are demo accounts on a demo database. **Change every password before this
is pointed at anything real**, and never reuse them anywhere else.

They come from `docs/seed/001_identity.sql`. Nothing here is hard-coded in the
application — if you have not run that script, none of these exist yet. See
`api/README.md`.

---

## Staff — sign in with an email

`POST /api/auth/login`

**Password for all seven: `CareLanka#2026`**

| Email | Role | Name it shows as |
| :--- | :--- | :--- |
| `nurse.perera@carelanka.lk` | `ward_nurse` | Amara Perera |
| `dr.silva@carelanka.lk` | `doctor` | Nimal Silva |
| `crew.fernando@carelanka.lk` | `ambulance_crew` | Kasun Fernando |
| `staff.jayasuriya@carelanka.lk` | `general_staff` | Ishara Jayasuriya |
| `duty.rajapaksa@carelanka.lk` | `duty_manager` | Sanduni Rajapaksa |
| `admin.wickrama@carelanka.lk` | `hospital_administrator` | Tharindu Wickramasinghe |
| `equip.bandara@carelanka.lk` | `equipment_manager` | Ruwan Bandara |

**Who to sign in as for the patient journey**, because it takes four different
people on purpose and one account will not walk the whole thing:

| Step | Account | Why not somebody else |
| :--- | :--- | :--- |
| Register and admit | `staff.jayasuriya` (reception) | The front desk does the paperwork. `crew.fernando` is refused — changed 2026-09-11 |
| Assign the bed | `staff.jayasuriya` or `nurse.perera` | Reception can bed a walk-in standing at the desk — changed 2026-09-12. Only for a bed matching the care level |
| Mark them arrived | `nurse.perera` | **The nurse and nobody else**, not even the duty manager: she is the one who can see the patient is in the bed. So a bed reception assigns stays *held* until she confirms it |
| Tick `clinical_clearance` | `dr.silva` | **A doctor and nobody else.** This is the wall |
| Tick medication, follow-up, transport | `nurse.perera` | |
| Prepare and settle the bill | `staff.jayasuriya` | Reception takes money. This is also the only way `billing_settled` is ever ticked |
| Confirm the discharge | `nurse.perera` | `duty.rajapaksa` instead if the patient is ICU or HDU |

An ICU or HDU bed needs `duty.rajapaksa` to assign it, and the same patient's
discharge needs them to confirm it. So does any bed that does not match the care
level — a downgrade, or a ward more acute than assessed. Those show as **amber**
buttons in the bed picker rather than the ordinary green.

**A children's ward only takes patients under 18**, and a patient with no recorded
date of birth counts as an adult. So give a test patient a real date of birth if you
want to see the pediatric ward offered.

```json
POST /api/auth/login
{
  "email": "duty.rajapaksa@carelanka.lk",
  "password": "CareLanka#2026"
}
```

One per role, because the demo has to switch between four different roles inside
ten minutes and creating accounts on stage is how demos die.

---

## Patient — sign in with a phone number

`POST /api/auth/patient/login`

| Phone number | Password | Name |
| :--- | :--- | :--- |
| `+94771234567` | `Patient#2026` | Chathura Wijesinghe |

```json
POST /api/auth/patient/login
{
  "phone_number": "+94771234567",
  "password": "Patient#2026"
}
```

**A patient signs in with a phone number, not an email.** That is not a
different spelling of the same thing — staff and patients are two separate
tables, with two separate login endpoints, on purpose.

---

## The account that exists to fail

| Email | Password | State |
| :--- | :--- | :--- |
| `former.gunasekara@carelanka.lk` | `CareLanka#2026` | **Deactivated** |

The password is correct and **login still returns 401**. That is the intended
behaviour, not a bug.

Sign in with a wrong password, an email that was never registered, and this
account, and you get three responses that are exactly the same, character for
character:

```json
{
  "title": "Unauthorized",
  "status": 401,
  "detail": "Those sign-in details are not correct.",
  "code": "cl_err_401"
}
```

If the three answers differed at all, anyone could feed the endpoint a list of
addresses and learn which ones belong to real hospital staff — without ever
guessing a password. Same answer to all three, and there is nothing to learn.

---

## Making a new patient account

Registration is open — no token needed.

```json
POST /api/auth/patient/register
{
  "phone_number": "+94770001234",
  "password": "Something#2026",
  "full_name": "Your Name"
}
```

You are signed in immediately: the response is a full token pair, not just
"created".

Try the same phone number twice and the second one is a `409` — one active
account per number.

---

## What comes back when you sign in

Every one of the four sign-in endpoints returns the same shape:

```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIs...",
  "token_type": "Bearer",
  "expires_in": 900,
  "refresh_token": "tF6OUlraLuOHF+OicwCTra62BUdMG51Vm59E/VLgTZ4=",
  "principal": {
    "id": "50d77772-fba4-4a7a-b64c-3a04f3aaead7",
    "principal_type": "staff",
    "role": "duty_manager",
    "display_name": "Sanduni Rajapaksa",
    "email": "duty.rajapaksa@carelanka.lk",
    "phone_number": null,
    "patient_id": null
  }
}
```

**Two tokens, two different jobs.**

- `access_token` goes on every request. It lasts **15 minutes** and then stops
  working.
- `refresh_token` gets you a new pair when the first one dies. Guard it the way
  you would a password — it is the thing that keeps someone signed in.

**`patient_id` is `null`, and that is normal.** It is null for every staff
member, and null for a patient who has an account but has never been treated
here — which is every patient until Patient Management ships the screen where
staff link a login to a medical record. A Flutter screen that assumes it is
filled in will crash on the very first real user.

---

## Using a token in Swagger

1. Run `POST /api/auth/login` and copy `access_token` out of the response.
2. Click **Authorize**, top right of the Swagger page.
3. Paste the token. **Do not type `Bearer ` in front of it** — Swagger adds that
   itself, and pasting it twice is the usual reason a token "does not work".
4. Every locked endpoint now works from the page.

When it stops working after 15 minutes, `POST /api/auth/refresh` with your
`refresh_token` gives you a fresh pair. Paste the new access token in again.

---

## Using a token from the command line

```bash
# sign in and keep the token
TOKEN=$(curl -s -X POST http://localhost:5231/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"duty.rajapaksa@carelanka.lk","password":"CareLanka#2026"}' \
  | jq -r .access_token)

# use it
curl http://localhost:5231/api/auth/me -H "Authorization: Bearer $TOKEN"
```

PowerShell:

```powershell
$r = Invoke-RestMethod -Uri http://localhost:5231/api/auth/login -Method Post `
  -ContentType application/json `
  -Body '{"email":"duty.rajapaksa@carelanka.lk","password":"CareLanka#2026"}'

Invoke-RestMethod -Uri http://localhost:5231/api/auth/me `
  -Headers @{ Authorization = "Bearer $($r.access_token)" }
```

---

## When sign-in is not working

| What you see | What it usually is |
| :--- | :--- |
| `401` on a password you are sure about | The seed script has not been run. Nothing exists yet |
| `401` on **everything**, suddenly | The access token is over 15 minutes old. Refresh it |
| `401` right after a refresh worked | Each refresh token works **once**. You reused an old one — and doing that deliberately ends every session that account has |
| `403`, not `401` | You are signed in fine. That role just is not allowed to do that |
| `429` | Too many attempts. Wait a minute |
| `500` on any endpoint that reads data | PostgreSQL is not reachable. Open `/api/health` — it says `database: down` outright |
| App will not start at all | `Jwt:SigningKey` is not set. `api/README.md`, step 2 |
