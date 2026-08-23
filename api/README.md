# Running the API locally

Everything below is a one-time setup, except step 4.

## 1. Install PostgreSQL

Any recent version. If you already have one running (some of us do, from other
projects), you don't need a second — just create a database and user for this
project inside it.

## 2. Create the database and user

Open `psql` (or pgAdmin, or whatever you use) and run:

```sql
CREATE USER carelanka WITH PASSWORD 'carelanka';
CREATE DATABASE carelanka OWNER carelanka;
```

`api/appsettings.Development.json` assumes this user, this password, and the
default port `5432`. **If your Postgres runs on a different port, or you'd
rather not use these exact credentials, don't edit that file — it's shared and
your change would show up in every diff.** Instead, override it locally:

```bash
cd api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:CareLanka" "Host=localhost;Port=5433;Database=carelanka;Username=carelanka;Password=carelanka"
```

User secrets live outside the repo (in a folder under your user profile), so this
never gets committed and never conflicts with anyone else's setup.

## 3. Create the tables

```bash
dotnet tool install --global dotnet-ef --version 8.0.11    # once per machine
cd api
dotnet ef database update
```

## 4. Load the test accounts

```bash
psql -U carelanka -d carelanka -f ../docs/seed_staff.sql
```

(Add `-h localhost -p <port>` if you're not on the default.)

Seven accounts, one per role. All of them use the password **`CareLanka#2026`**.

| Email | Role |
| :--- | :--- |
| `admin@carelanka.lk` | HospitalAdministrator |
| `duty@carelanka.lk` | DutyManager |
| `nurse@carelanka.lk` | WardNurse |
| `doctor@carelanka.lk` | Doctor |
| `crew@carelanka.lk` | AmbulanceCrew |
| `equipment@carelanka.lk` | EquipmentManager |
| `staff@carelanka.lk` | GeneralStaff |

## 5. Run it

```bash
cd api
dotnet run
```

Swagger is at `/swagger`. Call `POST /auth/login`, copy the `access_token` out of
the response, click **Authorize** at the top of Swagger and paste it in. Everything
marked `[Authorize]` works from then on.

---

# What the bootstrap already does for you

You do not need to wire any of this up in your own component. It happens automatically.

| You write | What happens |
| :--- | :--- |
| A class extending `Entity` | Gets a `Guid` PK and `created_at`, stamped on insert |
| A class extending `AuditedEntity` | Also gets `updated_at`, stamped on every save |
| A class extending `SoftDeletableEntity` | Also gets `is_active` / `deleted_at`, **and vanishes from every query** once `is_active` is false |
| Any `enum` property | Stored as readable text (`ward_nurse`), not an integer |
| Any class name / property name | Becomes `snake_case` in Postgres. `BedAssignment.WardId` → `bed_assignments.ward_id` |
| `: IAuditLogged` on an entity | Every insert, update and soft delete writes an `AuditLog` row with the staff id from the JWT |
| `throw new NotFoundException("Admission", id)` | Becomes a 404 `application/problem+json` with a machine-readable `code` |

## The rules that come with it

**Put your entity configuration in your own file.** `Data/Configurations/{Component}/`.
The DbContext picks it up automatically. **Do not add anything to `OnModelCreating`** —
four people editing one method means four merge conflicts on every migration.

**Add your `DbSet<T>` to your own group** in `CareLankaDbContext`, under your component's
comment header. That is the only line of a shared file you should ever touch.

**Never write a `try/catch` in a controller.** Throw an `ApiException` subclass from the
facade; `GlobalExceptionHandler` shapes it. A controller that catches an error produces a
response the generated clients cannot classify.

**Add your message codes to your own region** of `MessageCode`, with your prefix —
`cl_emg_`, `cl_stf_`, `cl_equ_`, `cl_pat_`. Codes are permanent; never renumber one.

**Every timestamp is `DateTimeOffset` in UTC.** Npgsql rejects anything else.

**Unique constraints on soft-deletable tables must be scoped `.HasFilter("is_active")`.**
`StaffMemberConfiguration` shows the pattern and says why. A plain `UNIQUE` is a live bug.

## Migrations

Name yours `{Component}_{What}` — `Patient_AddWard`, `Equipment_AddBed`.

```bash
cd api
dotnet ef migrations add Patient_AddWard --output-dir Data/Migrations
dotnet ef database update
```

**Pull `main` immediately before you generate one, and push promptly after.** Each
migration snapshots the whole model, so two people generating from the same snapshot
produces a conflict that is genuinely painful to unpick. If you hit one: delete your
migration, pull, regenerate. Never hand-merge `CareLankaDbContextModelSnapshot.cs`.
