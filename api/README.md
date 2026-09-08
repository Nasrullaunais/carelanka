# CareLanka API — running it locally

Four people need this to start on four machines. Three commands, then it runs.

---

## What you need

- **.NET 8 SDK**
- **PostgreSQL 14 or newer**, running locally
- **`dotnet-ef`**, once per machine:

  ```
  dotnet tool install --global dotnet-ef --version 8.*
  ```

To run the integration tests, Docker must also be running. `dotnet test` starts a
disposable PostgreSQL 16 container, applies the real migrations, and removes the
container afterward; no test database password is stored in the repository.

---

## 1. Make the database

```
createdb -U postgres carelanka
```

Or from `psql`: `CREATE DATABASE carelanka;`

The repository contains no database credential. Store your local connection
string in .NET user-secrets so nobody else's checkout changes and a password
cannot be committed accidentally:

`api/appsettings.Development.example.json` shows the two required settings with
placeholders only; the application does not load that example file.

```
dotnet user-secrets set "ConnectionStrings:CareLanka" "Host=localhost;Port=5432;Database=carelanka;Username=postgres;Password=YOUR_PASSWORD" --project api
```

For a deployed environment, use `ConnectionStrings__CareLanka` (two
underscores) instead.

## 2. Set the JWT signing key

**The API will not start without this.** That is deliberate — the alternative is
a default key in `appsettings.json` that ships to production and that anyone who
has read the repository can forge tokens with. Assignment §18.2 and §19 both
call out committed credentials specifically.

```
dotnet user-secrets set "Jwt:SigningKey" "any-random-string-of-at-least-32-characters" --project api
```

User secrets live outside the repository, in your Windows user profile, so there
is nothing to accidentally commit.

For a deployed environment it is an environment variable instead:
`Jwt__SigningKey` (two underscores — that is how .NET spells a nested key).

## 3. Create the tables and seed the accounts

```
dotnet ef database update --project api
psql -U postgres -d carelanka -f docs/seed/001_identity.sql
```

## 4. Run it

**From the repo root:**

```
dotnet run --project api
```

**Or from inside `api/`:**

```
cd api
dotnet run
```

Both do exactly the same thing — `--project api` just saves you the `cd`. Use
whichever you prefer; the rest of this file assumes you are at the repo root,
because that is where the `psql` and `dotnet ef` commands expect to be.

It comes up on **`http://localhost:5231`** and opens
**`http://localhost:5231/swagger`** in your browser by itself. That port and the
browser-opening both come from `api/Properties/launchSettings.json` — change it
there if you want something else.

Stop it with **Ctrl+C**.

Want HTTPS as well? `dotnet run --project api --launch-profile https` adds
`https://localhost:7109`. You do not need it for local work.

**A code change needs a restart.** Ctrl+C and run it again, or use
`dotnet watch --project api` and it restarts itself when you save.

---

## Test accounts

**Full list, with how to actually use a token: `TEST_ACCOUNTS.md`.**

Seeded by `docs/seed/001_identity.sql`. All seven staff passwords are
`CareLanka#2026`.

| Email | Role |
| :--- | :--- |
| `nurse.perera@carelanka.lk` | `ward_nurse` |
| `dr.silva@carelanka.lk` | `doctor` |
| `crew.fernando@carelanka.lk` | `ambulance_crew` |
| `staff.jayasuriya@carelanka.lk` | `general_staff` |
| `duty.rajapaksa@carelanka.lk` | `duty_manager` |
| `admin.wickrama@carelanka.lk` | `hospital_administrator` |
| `equip.bandara@carelanka.lk` | `equipment_manager` |

Plus two accounts that exist to be tested against, not to be used:

| Account | What it is for |
| :--- | :--- |
| `former.gunasekara@carelanka.lk` | Deactivated. Logging in must give the **same** 401 as a wrong password |
| `+94771234567` / `Patient#2026` | A patient account with no medical record linked — `patient_id` comes back `null`, and that is normal |

---

## Using a token in Swagger

1. `POST /api/auth/login` with one of the emails above.
2. Copy `access_token` out of the response.
3. Click **Authorize**, top right, and paste it. **No `Bearer ` prefix** —
   Swagger adds that.
4. Every `[Authorize]` endpoint now works from the UI.

Access tokens last 15 minutes. When one expires, `POST /api/auth/refresh` with
the `refresh_token` gives you a new pair.

---

## Things that will bite you

**"Jwt:SigningKey is not configured"** — step 2. The message names the exact
command.

**A 500 on every endpoint that touches data** — the database is not reachable.
`GET /api/health` tells you directly: it reports `database: down` rather than
pretending to be fine.

**`Npgsql.PostgresException: relation "staff_members" does not exist"`** — the
migrations have not been applied. Step 3.

**`Cannot write DateTimeOffset with Offset != 0`** — somebody used
`DateTimeOffset.Now` instead of `.UtcNow`. Everything in this database is UTC,
and Npgsql refuses anything else at write time rather than at compile time.

---

## Before you generate a migration

```
git pull                                   # immediately before, not this morning
dotnet ef migrations add Yours_WhatItDoes --project api --output-dir Data/Migrations
git push                                   # promptly after
```

Every migration snapshots the whole model, so two people generating one on the
same day conflict in `CareLankaDbContextModelSnapshot.cs`. If that happens:
**delete your migration, pull, regenerate.** Never hand-merge the snapshot file.

Name migrations `{Component}_{What}` — `Patient_AddAdmission`,
`Equipment_AddBed`.

**Migrations are DDL only.** Seed data goes in `docs/seed/*.sql`, so no
environment-specific id ever reaches production.
