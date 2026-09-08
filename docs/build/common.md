# Build Track 0 — Common

**Owner: Common (group-owned)** · **Contract:** `specs/common-spec.yaml` · **Decisions:** `docs/ADR.md`
**Index:** `docs/BUILD_PLAN.md`

Everything that is not a specific member's component. Built **once**, not four times.

**The other three tracks cannot start until §2 is done.** Not "should not" — cannot. Every
endpoint in every other spec sits behind `[Authorize]`, and until something issues a token
there is nothing to authorize. This is why this track goes first and why it is one
person's.

Read §7 if you are *not* the owner of this track — it is the short version of what auth
means for your component.

---

## Built so far — 2026-09-08

Steps 1 to 4 below are implemented in PR #11, plus the exception handler from §4.1,
`GET /health` and the auth integration test foundation from §6. Local setup — connection
string, signing key, migrations, seed — is `api/README.md`.

Once PR #11 is merged, the other three tracks are unblocked. Endpoints can then be written
behind `[Authorize(Policy = Policies.DutyManager)]`; §7 is the short version of what that
means for you.

| Section | State |
| :--- | :--- |
| §1 Foundations — base entities, `DbContext`, snake_case wire, enum storage, `Common_AddIdentity` | Done |
| §2.1–2.2 Password hashing, `StaffMember`, seed (`docs/seed/001_identity.sql`) | Done |
| §2.3–2.5 JWT issuing, `POST /auth/login`, policies | Done |
| §2.6 Refresh rotation, reuse detection, logout | Done |
| §2.7–2.8 `PatientAccount`, register, patient login, `GET /auth/me` | Done |
| §4.1 Exception handler + `MessageCode` | Done |
| §6 Auth integration and generated-contract tests | Done |
| §4.2 Audit interceptor | **Not built** |
| §5 `AgentWorkflow` tables and the `/workflows` surface | **Not built** |
| §6 CI, `web-ui/` scaffold, Flutter auth plumbing | **Not built** |

Two things worth knowing before you build on it:

- **`RefreshToken` changed shape.** It could only hold staff sessions; it now carries a
  `PrincipalType` and one nullable FK per identity. `entity_diagram.md` Rev 2.7 records why.
- **The API will not start without `Jwt:SigningKey`.** That is deliberate. `api/README.md`
  gives the one command.

---

## 0. Order

Steps 1–5 are the critical path. Everything after §5 can slip a day without blocking
anybody else; nothing before it can.

```
1  base classes + DbContext + first migration
2  ─┬ password hashing
   ├ StaffMember + seed
   ├ JWT issuing
   ├ POST /auth/login
   └ [Authorize] + role policies          ◄── THREE PEOPLE UNBLOCKED HERE
3  refresh + logout + /auth/me
4  PatientAccount + register + patient login
5  exception handler + audit interceptor
   ─────────────── others are now building ───────────────
6  AgentWorkflow tables + /workflows surface
7  health, CI, web-ui scaffold, Flutter auth plumbing
```

---

## 1. Foundations

### 1.1 Base entity classes

Exactly as `docs/entity_diagram.md` defines them — `Entity` → `AuditedEntity` →
`SoftDeletableEntity`. Do not invent fields.

**There are no `CreatedBy` / `UpdatedBy` columns.** Who did what lives in `AuditLog`
(§5.2). Adding them here means two answers to the same question.

`DateTimeOffset` in UTC everywhere. Npgsql rejects a `DateTime` with an unspecified kind,
and it fails at write time, not at compile time.

### 1.2 DbContext

One `CareLankaDbContext`. **Nobody edits `OnModelCreating`** — it is one line forever:

```csharp
protected override void OnModelCreating(ModelBuilder b)
    => b.ApplyConfigurationsFromAssembly(typeof(CareLankaDbContext).Assembly);
```

Four people adding configuration to one method means all four get a merge conflict on
every migration. Each member writes `Data/Configurations/{Component}/*Configuration.cs`
instead and touches zero shared lines.

`DbSet<T>` properties are the one shared surface. Group them by component with a comment
header so a merge conflict there is at least readable.

### 1.3 Conventions to set now, not later

| Setting | Value | Why it cannot wait |
| :--- | :--- | :--- |
| `JsonNamingPolicy.SnakeCaseLower` | bodies | The specs publish `full_name`. Set this late and every client regenerates |
| `<Nullable>enable</Nullable>` | already on | Without it, required-vs-optional in the generated spec is meaningless |
| Enum storage | `HasConversion<string>()` + CHECK | ADR 5. Affects every migration |
| Route/query casing | `camelCase` query params | `sortDir`, not `sort_dir`. What the specs already publish |

### 1.4 First migration

`Common_AddIdentity` — base classes, `StaffMember`, `PatientAccount`, `RefreshToken`.

Migration naming is `{Component}_{What}` and the rule for everyone is: pull `main`
immediately before generating one, push promptly after. On a snapshot conflict, delete
yours, pull, regenerate. **Never hand-merge the snapshot file.**

---

## 2. Auth — the critical path

This is the section that was missing. It is written as a sequence because the order
matters: each step is testable before the next one exists.

### 2.1 Password hashing

Use ASP.NET Core's `PasswordHasher<T>`. It is PBKDF2 with a per-password salt and an
embedded format version, so upgrading the work factor later does not invalidate stored
hashes.

**Do not write your own.** Not a style preference — §16.1 grades security, and hand-rolled
password crypto is the single most common way to lose those marks.

Never log a password, never put one in a `ProblemDetails`, never return one from any
endpoint. `PasswordHash` is not on any DTO.

### 2.2 `StaffMember` + seed

`StaffMember` extends `SoftDeletableEntity`: email (unique), password hash, first/last
name, phone, department, `Role`.

**The unique index on email must be scoped `WHERE is_active`.** A plain `UNIQUE` is a live
bug on a soft-deletable table: deactivate a staff member and their email can never be
reused, and because the global query filter hides the conflicting row, the service-layer
duplicate check passes and `SaveChanges` throws a constraint violation nobody can explain.

```
CREATE UNIQUE INDEX ix_staff_members_email
    ON staff_members (email) WHERE is_active;
```

Seed one account per role — seven rows. §15 requires **test accounts** in the submission,
and the demo needs to log in as four different roles in ten minutes.

Seed data goes in `docs/seed/*.sql` as parameterised idempotent scripts, **not** in a
migration. Migrations are DDL only, so environment-specific ids never reach production.

### 2.3 JWT issuing

| Claim | Value | Why |
| :--- | :--- | :--- |
| `sub` | `StaffMember.Id` or `PatientAccount.Id` | Who |
| `role` | one `PrincipalRole` value | What they may do |
| `typ` | `staff` or `patient` | **Which table `sub` is in** |
| `jti` | token id | Traceability |
| `exp` | short — 15 min | Server-side revocation is the refresh token's job |

**`typ` is the one that is easy to leave out and expensive to add back.** `sub` alone is
ambiguous: staff ids and patient-account ids are both GUIDs from different tables. An
endpoint that trusts `sub` without checking `typ` looks a patient id up in `staff_members`,
finds nothing, and either 500s or — worse — silently treats the request as unauthenticated
staff.

The signing key comes from configuration. **Locally use `dotnet user-secrets`; in
deployment use an environment variable.** It is never in `appsettings.json` and never in
Git. §18.2 and §19 both call out committed credentials specifically.

### 2.4 `POST /auth/login`

Look up by email among active staff, verify the hash, issue the pair.

**All three failures return the same 401**: wrong password, unknown email, deactivated
account. Different responses turn the endpoint into a way to discover which emails exist
at the hospital.

Rate-limit it, per IP and per account. ASP.NET Core has built-in rate limiting; a fixed
window is enough.

**Test before moving on.** Correct password → 200 with a token. Wrong password → 401. A
deactivated account → 401, not 403.

### 2.5 `[Authorize]` + role policies — *the unblocking step*

Register the policies **once, here**, so no member writes a role string in a controller:

```csharp
options.AddPolicy("WardNurse",   p => p.RequireRole("ward_nurse"));
options.AddPolicy("DutyManager", p => p.RequireRole("duty_manager"));
// ... one per PrincipalRole value, plus the combinations the specs actually use
```

A member then writes `[Authorize(Policy = "DutyManager")]` and nothing else. A typo in a
policy name fails at startup; a typo in a magic role string fails silently at 3am on demo
day by letting the wrong person in.

**Tell the group the moment this is pushed.** This is the step that unblocks three people.

### 2.6 Refresh, rotation, logout

`RefreshToken` stores a **hash** of the token, not the token. If the database leaks, the
rows are not usable credentials.

**Rotating and single-use.** Each refresh revokes the presented token and issues a new one.

**Reuse detection:** presenting an already-revoked token revokes the entire chain for that
principal and returns 401. It means two clients hold the same token and one of them is not
the owner. This is a handful of lines and it is the reason `RefreshToken` is a table
rather than just a signed string.

`POST /auth/logout` revokes the row, so a stolen access token dies at the next refresh
instead of living out its full window. Idempotent — logging out twice is 204, not an error.

### 2.7 `PatientAccount` — register and login

Second identity, deliberately separate. Phone number is the login identifier, unique
`WHERE is_active`.

**Registration creates a login, not a medical record.** No `Patient` row, no link. Staff
link the two later through `POST /patients/{id}/link-account` after checking identity —
never inferred from a matching phone number, because two people share a phone far more
often than a hospital would like.

The consequence a client must handle: `CurrentPrincipal.patient_id` is **null** for a
patient who has signed up but never been treated here. That is the ordinary state for a
new account, not an error. A Flutter screen that assumes it is non-null crashes on the
first real user.

### 2.8 `GET /auth/me`

Resolved entirely from the `sub` claim. **No id parameter, ever.**

The same rule applies to every `/me/*` endpoint in the other three specs. A route that
takes an id and checks it against the JWT is one forgotten check away from letting any
patient read any other patient's admission. A route that takes no id cannot have that bug.

Both clients call this on startup to decide which navigation to render.

---

## 3. What "done" looks like for auth

Do not move on until all of these pass:

- [x] Seeded staff member logs in, gets a token
- [x] Wrong password, unknown email and deactivated account all return the **same** 401
- [x] A protected endpoint returns 401 with no token, 403 with the wrong role, 200 with the right one
- [x] Refresh returns a new pair; the old refresh token is dead
- [x] Reusing a revoked refresh token 401s **and** kills the chain
- [x] Logout, then refresh → 401
- [x] Patient registers, logs in, and `/auth/me` returns `principal_type: patient`, `patient_id: null`
- [x] Swagger publishes bearer authorization and a token passes the protected policy endpoint
- [x] No password or signing key appears in any captured log line or response body

These gates run in `CareLanka.Api.Tests` against a disposable PostgreSQL database. The
registration test sends two requests concurrently so the unique-index race is part of the
acceptance suite, not only a sequential duplicate check.

The generated document attaches bearer security to each `[Authorize]` operation rather
than declaring it globally. Swashbuckle 6.6 omits an empty operation security array when
serializing, so a global requirement would incorrectly make `[AllowAnonymous]` login,
registration and refresh operations require the token they exist to issue.

That last one is worth checking by actually reading the console output during a failed
login, not by assuming.

---

## 4. Exception handling and audit

### 4.1 One handler

One `IExceptionHandler` via `AddExceptionHandler` + `UseExceptionHandler`. `ApiException`
→ `ProblemDetails` at its status; validation failures → `ValidationProblemDetails`;
everything else → 500 with a generic message and **no internal detail**.

`extensions["code"]` carries the `MessageCode`; `extensions["traceId"]` carries the trace
id, also as a header. Log `Error` for 5xx and `Information` for 4xx — a 404 is not an
incident, and treating it as one buries the real ones.

**No `try/catch` in controllers.** A controller that reshapes an error produces a response
the generated client cannot classify.

Message codes are a stable enum with human text in a resource file keyed by code:
`cl_err_` shared, then `cl_emg_`, `cl_stf_`, `cl_equ_`, `cl_pat_`. Clients branch on the
code, so Sinhala or Tamil later is a resource-file change rather than a code change.

Publish the `MessageCode` enum and the four component prefixes to the group as soon as it
exists — each member adds their own codes and they must not collide.

### 4.2 Audit interceptor

An `ISaveChangesInterceptor` writes the `AuditLog` row, taking the staff id from the JWT.
**Nobody ever writes an audit row by hand.**

A soft delete logs as `Operation = Delete`, not `Update`. It looks like an update to EF —
`IsActive` goes false — so this is an explicit case in the interceptor, and it is the one
people get wrong.

---

## 5. The shared agent workflow surface

ADR 3. `AgentWorkflow` + `AgentProposedChange`, one generic pair for all five agents, and
the endpoints in `specs/common-spec.yaml`:

```
GET  /workflows                              filter by correlationId
GET  /workflows/{workflowId}                 the §9.1 auditable trace
POST /workflows/{workflowId}/approve         the one high-impact gate
POST /workflows/{workflowId}/reject          recorded safe failure
POST /workflows/{workflowId}/request-revision
```

Three things that are load-bearing:

**Persist the plan before running anything.** A crash mid-run must leave an auditable row,
not nothing. §9.1 asks for "an auditable result **or** a safe, clearly recorded failure" —
a workflow that vanishes is neither.

**Approve re-validates under a row lock, in one transaction.** The world moves while a
proposal sits on a screen: a bed that was free when the agent ranked it may be taken by the
time a human clicks approve. A hard rule that fails the re-check → 409 and **nothing is
written**, not a partial apply.

**Raw model reasoning is never persisted.** §6 forbids it. `rationale` fields hold a short
human-readable summary written for the approver.

The Coordinator Agent itself comes later, after the four domain agents exist — it has
nothing to delegate to until then. The tables and the read surface come now because four
people link to them by `workflow_id`.

---

## 6. Scaffolds and CI

Not glamorous, all graded.

| # | Thing | Notes |
| :-- | :--- | :--- |
| 1 | `GET /health` | §14 requires a working health URL. Report database connectivity, not just "up" |
| 2 | Swagger | `[ProducesResponseType]` for **every** outcome including errors. An endpoint declaring only its 200 generates a client that cannot type its failures |
| 3 | CORS | React's origin only, not `*` |
| 4 | `.github/workflows/ci.yml` | §13 grades it. `dotnet build` + `dotnet test`; `bun install --frozen-lockfile` → `check:codegen` → `typecheck`; `flutter analyze` + `flutter test`; `bun run check:specs` |
| 5 | Test project | `CareLanka.sln` has **one** project and no test project. Testing/CI/Git is 8 individual marks |
| 6 | `web-ui/` scaffold | React + Vite + TanStack Query + `@hey-api/openapi-ts`. Everyone builds screens on top, so it blocks all React work |
| 7 | `appsettings` + local Postgres setup | Documented well enough that four machines can all run it |

**Order `check:codegen` before `typecheck` in CI.** A stale generated client makes every
downstream type error a red herring, and someone will spend an afternoon on it.

### 6.1 Flutter auth plumbing

Shared, so it is common — `mobile-ui/lib/core/`, which members do not edit.

- `flutter_secure_storage` for both tokens. §8 requires secure token storage;
  `SharedPreferences` is not that
- One interceptor: attach the bearer header, refresh once on a 401, log out if the refresh
  fails
- Login, register and logout screens
- The route guard that sends an unauthenticated user to login

`flutter create .` still has not been run and needs a machine with the SDK. Do that first
or none of this compiles.

---

## 7. If you are **not** the owner of this track

The short version. Four things affect how you build your component:

**1. Your endpoints are gated by policy, not by a role string.**

```csharp
[Authorize(Policy = Policies.DutyManager)]   // yes — a typo will not compile
[Authorize(Policy = "DutyManager")]          // works, but a typo fails at startup
[Authorize(Roles = "duty_manager")]          // no — a typo here fails silently
```

`CareLanka.Api.Common.Auth.Policies` holds every policy name as a constant, and they are
registered centrally (§2.5). One per staff role, plus `AnyStaff`, `PatientOnly`,
`WorkflowReader` and `WorkflowStarter`. If you need a combination that is not there, ask —
do not invent a role string.

**2. Anything under `/me/*` is scoped by the `sub` claim, never by a parameter.** Your spec
already publishes it that way. A route that takes an id and checks it against the JWT is
one forgotten check away from letting any user read anyone else's data, and that is graded
under §16.1 security.

**3. Do not stub auth.** It is the one dependency being built first specifically so nobody
has to. If it is not pushed yet, build your entities and services — they do not need a
token. Wire up controllers after.

**4. To test a protected endpoint,** log in as a seeded account and paste the token into
Swagger's Authorize button. The seven seeded accounts are in `docs/seed/`.
