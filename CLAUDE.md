# CLAUDE.md

Repo-wide conventions for Claude Code. Kept short on purpose — it loads on every
turn of every session. Detail lives in the documents below; this file holds only
what you would get wrong before thinking to go and look it up.

## How to explain things to us

**Write for a student who is good at this course but not fluent in the jargon.**
We are all learning this stack while building on it. An explanation nobody
understands is not an explanation, however correct it is.

- **Lead with the problem in one plain sentence**, before any detail.
- **Say what actually happens, not the name for it.**
- **Show the actual thing.** Two clashing values side by side beats a paragraph
  describing that they clash.
- **Short lines, not paragraphs.** A block of prose gets skimmed and missed.
- **An everyday comparison earns its place** when the idea is unfamiliar.
- **End with what it means for the reader** — what changes, what is left to do.

```
Not:  "The route collides, throwing AmbiguousMatchException at startup."

But:  "Two people gave their page the same web address.
       One app can't have two pages at the same address — it won't start at all.
       Like two shops on one street both numbered 12: the postman can't deliver.
       You don't close a shop, you renumber one."
```

Use the exact term when it *is* the thing — a class name, an endpoint, a config
key, something being looked up or searched for. Everywhere else, plain words.

**If someone says they don't understand, that is not a request for more detail.**
Say the same thing again with simpler words and fewer of them.

## Current state

**Design and scaffolding only — no features yet.** `api/` is an empty ASP.NET
project with the component folder tree stubbed out (`.gitkeep` files, no classes,
no `DbContext`). `mobile-ui/` is a Flutter skeleton with the same per-member tree
and no `android/`/`ios/` yet. `web-ui/` is empty.

So this file is prescriptive. **Where a committed contract in `specs/` already
answers a question, that contract wins over anything written here.**

## Which document answers what

Read the one that owns the question before deciding. If two disagree, the order
here is the order of authority.

| Document | What it settles | Owner |
| :--- | :--- | :--- |
| `docs/2026-S1-SE3090-Assignment_1_Specification.md` | The assignment brief. Beats every other file, this one included. | Module |
| `docs/ADR.md` | **The decisions that are settled and why.** Agent framework, model provider, agent workflow tables, no facade, enum storage, React and Flutter state. Required by assignment §14.2 and examinable at the viva. | Group |
| `specs/common-spec.yaml` | **Auth and the shared agent workflow surface.** Everything that is not one member's component. Read before assuming something is unowned. | Common (Nasrullah) |
| `docs/CareLanka_Component_Plan.md` | The four components, who owns which, the seven roles, why React and Flutter differ. Read first. | Group |
| `docs/BUILD_PLAN.md` | **The build index.** Build order, the five tracks, who is waiting on whom, integration checkpoints. Read before writing code. | Group |
| `docs/build/{common,emergency,staff,equipment,patient}.md` | **What your member builds, step by step.** Read your own in full; read `build/common.md` §7 whoever you are — four things about auth that change how you build. | That track's owner |
| `STUBS.md` | **Every fake standing in for someone else's unbuilt work.** Read the rows where `Owner` is your member — somebody is already depending on those. | Everyone, constantly |
| `docs/entity_diagram.md` | Every table, field and enum, with the reasoning. | Group; each member edits only their own entities |
| `specs/integration_of_functions.md` | Component boundaries: who owns which table, who calls whose service, open cross-component items. Read before touching anything you do not own. | Group |
| `specs/*-spec.yaml` | The OpenAPI contract per component — actual request and response shapes. | That component's member |
| `specs/{patient,equipment,emergency}-management-plan.md` | That component's design doc; its `*-spec.yaml` follows from it. Staff has not written theirs. | That component's member |
| `specs/ai-orchestration-workflow.md` | How the agents chain into one workflow. Its two open questions — shared workflow state, and the fifth Coordinator Agent — are settled in ADR 3 and ADR 1. Does not block building an individual agent. | Group |
| `mobile-ui/README.md` | Flutter layout, and that each member works only inside `lib/features/<component>/`. Read before writing Dart. | Group |

(Each member may also keep their own gitignored `IMPORTANT.md`/`RESUME.md` for
their personal Claude sessions — nothing to read here, not part of this table.
If you keep one, update `RESUME.md` as you go with what you discover, fix and
build, so a fresh chat can pick up where you left off.)

Four rules that fall out of it:

- **A design doc and its spec move together** — changing one means changing all
  three (plan, spec, integration doc) in the same commit.
- **Where `entity_diagram.md` and a member's own committed `*-spec.yaml`
  disagree, the spec wins** — for whichever member owns that entity. A
  disagreement about someone *else's* entity is an Open Decision, not an edit.
- **Cross-document references are bare filenames in prose**, not relative links.
- **Stub anything you don't own, and record it in `STUBS.md` in the same
  commit.** Never wait for a teammate's code, and never write it for them — see
  below.

## Common vs. yours

**If it does not belong to a specific member, it is common — and common is built once,
by Nasrullah, not four times.** *(settled 2026-09-07)*

Common covers: auth and the JWT, the `DbContext` and base entity classes, the exception
handler, the audit interceptor, `AgentWorkflow` / `AgentProposedChange`, the Coordinator
Agent, and CI. The contract is `specs/common-spec.yaml`.

Before building anything that is not in your component's row of
`integration_of_functions.md` §3, check whether it is common. Building a common thing
twice is worse than stubbing it, because both copies look right.

## Stubs

Four people build four components that depend on each other, so you will
regularly need something nobody has built yet. Fake it and keep moving. **A stub
nobody wrote down is the problem** — it looks like working code, passes your
tests, and quietly returns invented data.

- **Stub at the service interface**, not in a controller, so swapping in the real
  one is a DI registration change and nothing else moves.
- **Mark it `// STUB`** with the `STUBS.md` row number.
- **Match the owner's published contract exactly.** Invent a different shape and
  the real service will break your code when it arrives.
- **Read `STUBS.md` rows where `Owner` is your member** before you start work —
  those are things other people are already depending on you for.
- Stub notes go in `STUBS.md`, **never in this file.** This one loads every turn
  and is for standing conventions; that one is current state and changes daily.

## Stack

ASP.NET Core Web API (.NET 8) · PostgreSQL via EF Core · React + Vite (plain
React, not Next.js) · Flutter · shared JWT with roles in claims · GitHub Actions.

**Package manager: `bun`.** Others use `npm`, which is fine — keep `package.json`
scripts runner-agnostic (no `bun` inside a script body).

---

# API clients are generated, never hand-written

The ASP.NET API is the source of truth; both frontends generate typed clients
from its OpenAPI document. **Never write `fetch()`, `axios`, `http.get()` or a
hand-rolled model for a CareLanka endpoint** — generate it, then import it.

```
api/ → /swagger/v1/swagger.json
         ├─ web-ui/    @hey-api/openapi-ts → TS client + types + query options
         └─ mobile-ui/ swagger_parser      → Dart client + models
```

## Publish a truthful spec

- `<Nullable>enable</Nullable>`, or required vs. optional in the spec means nothing.
- `[ProducesResponseType]` for **every** outcome, errors included. An endpoint
  declaring only its 200 generates a client that cannot type its failures.
- Wire format is **`snake_case` bodies** (`full_name`) with **`camelCase` query
  params** (`sortDir`) — what `specs/*.yaml` already publish. Use
  `JsonNamingPolicy.SnakeCaseLower` for bodies.

## The hand-written specs are temporary

`specs/*.yaml` were written ahead of the code and are the only record of the API
design, so **do not delete them yet**. Per component, once its controllers exist:
build them to match the hand-written spec, download the generated document over
the top, and diff. Every difference is either a spec the code failed to honour or
a decision made in a controller and never written down — resolve each one
deliberately rather than accepting the generated side by default. From then on
that file is generated output. Once all four are generated they collapse into one
`carelanka.json` — one app publishes one document.

## Rules

1. **Generated directories are disposable.** Anything written inside one is lost
   on the next run. Fixes go in the ASP.NET code that publishes the spec.
2. **Generated types are the single source of truth for domain shapes.** Import
   `Admission` from the generated types. `src/types/*` holds UI-only concerns —
   permission unions, column definitions, presentation maps — keyed off the
   generated enums so a contract change becomes a compile error.
3. **Regenerate in the same commit as the backend change.**
4. **A drift gate, not discipline.** `check:codegen` regenerates and fails on any
   `git status --porcelain` difference in the generated directory. CI runs it
   **before** `typecheck` — a stale client makes every type error a red herring.

## Two things that are easy to get wrong

- **web-ui generates with `baseUrl: false`**, dropping the spec's `servers`
  entry, which names whichever host published it. The base URL is the relative
  path `/api`, set in `src/services/api/runtime.ts`, with dev proxying `/api` to
  the backend — relative in dev and production alike. **No base-URL environment
  variable**: it buys a CORS surface and an environment contract for nothing.
  `runtime.ts` must import nothing leading back to the generated client — the
  client calls `createClientConfig` while still initialising, and a cycle there
  is a startup crash. All other HTTP concerns live in `transport.ts`.
- **mobile-ui contradicts `mobile-ui/README.md`**, which gives every feature a
  `models/` and a `services/` for hand-written JSON classes and HTTP calls — the
  thing this section forbids. Until the group settles it: domain shapes come from
  the generated `api_client`, a feature's `services/` wraps that client instead of
  calling `http`/`dio`, and `core/network/` owns the JWT header and error handling
  once for everyone. **Open:** is `lib/services/api_client/` committed or built in CI?

---

# One app, one API surface

The four specs describe **one** ASP.NET application, so routes, `operationId`s
and schema names are global, not per-component. A duplicate route throws at
startup; a duplicate `operationId` or schema name silently collides in the
generated clients.

- **Namespace by component** where a resource is not genuinely shared:
  `/reports/staff/agent-performance`, not a third claim on
  `/reports/agent-performance`.
- **`operationId` is unique across all four specs** — it becomes the generated
  function name.
- **A schema name shared between specs is byte-identical.** Where two components
  need different views of one thing, give them different names (`EquipmentBed` /
  `AdmissionBed`), not one name and two shapes. Same for enums: a
  component-specific vocabulary gets a component-specific name.
- **Genuinely shared types** (`ProblemDetails`, `PagedResult`, the workflow
  types) have one definition, group-owned: changed in all five specs in the same
  commit, or not at all.

**All five specs validate and the collision count is zero** (re-swept 2026-09-07, after
`common-spec.yaml` was added: 5/5 valid, 0 duplicate routes, 0 duplicate `operationId`s,
0 conflicting schema names). `integration_of_functions.md` §11.6 records what was renamed and
why, so the same names are not reintroduced. Check it before adding a route,
`operationId` or schema name.

**Validate in CI:** `bun run check:specs`, which loads all five with
`@apidevtools/swagger-parser` and then sweeps for duplicate routes, duplicate
`operationId`s and same-name-different-shape schemas. Note that
`npx @apidevtools/swagger-parser` does **not** work — the package ships a library, not a
CLI — so this has to be a small script, not a one-liner. That sweep is what stops zero
silently becoming one again. **Quote any inline description containing a
comma** — inside a YAML flow mapping an unquoted comma splits the description
into phantom keys, which no one catches by eye and the validator catches every
time. That exact mistake in `staff-spec.yaml` kept it invalid for weeks and
blocked client generation for both frontends.

---

# Exception handling

`specs/*.yaml` commit to `application/problem+json` with `ProblemDetails` /
`ValidationProblemDetails` — ASP.NET Core's native shape, and what the clients
generate against. Do not wrap responses in a custom envelope.

**Typed exceptions.** One `ApiException` base carrying status, a machine-readable
`MessageCode` and message params; one subclass per status — `NotFoundException`
(404), `BadRequestException` (400), `ForbiddenException` (403),
`ConflictException` (409), and `IllegalTransitionException : ConflictException`.
That last one is its own type because the specs define explicit state machines
and document illegal moves as a distinct response.

**`Find*` returns, `Get*` throws.** `FindByIdAsync` returns `T?`; `GetByIdAsync` turns
`null` into `NotFoundException`. Both live in the service (ADR 4). Otherwise "not found"
means two different things at two layers.

**One central handler** — a single `IExceptionHandler` via `AddExceptionHandler`
+ `UseExceptionHandler`. `ApiException` → `ProblemDetails` at its status;
validation failures → `ValidationProblemDetails`; everything else → 500 with a
generic message and **no internal detail**. Code goes in `extensions["code"]`,
trace id in `extensions["traceId"]` and a header. Log `Error` for 5xx,
`Information` for 4xx — a 404 is not an incident. **No `try/catch` in
controllers**: a controller that reshapes an error produces a response the client
cannot classify.

**Message codes** are a stable enum, with human text in a resource file keyed by
code and parameterised with `{0}` (`cl_adm_003  Admission {0} cannot move from
{1} to {2}`). Prefixes: `cl_err_` shared, then `cl_emg_`, `cl_stf_`, `cl_equ_`,
`cl_pat_`. Clients branch on the code, so text stays translatable and
Sinhala/Tamil is a resource-file change rather than a code change.

## Client side

`transport.ts` owns a response interceptor that toasts every failure. **A page
never handles an HTTP status code.**

- **401** — session gone, re-authenticate.
- **403** — toast the server's message. It means the UI offered an action the
  user's permissions forbid: a UI bug, not a normal path.
- **4xx/5xx** — toast the server's message **as-is**; never re-word it in a page.
- **Queries do not retry.** The toast already fired. Lists render "Try again"
  instead of an empty table.
- **Successes are the page's job** — there is no interceptor signal for them.
- **A rejected fetch has no `Response`** and never reaches the interceptor.
  Handle it explicitly, or a page that swallows it leaves the user believing a
  write succeeded.

---

# Backend architecture

**Controller → Service → Entity**, DTOs separate from EF entities. *(ADR 4 — the facade
layer was dropped on 2026-09-07.)* The
tree committed in `api/`, and the one `integration_of_functions.md` §1 publishes
to the group:

```
Controllers/{Component}/   thin — bind, delegate, return. No business logic.
Services/{Component}/      business logic + data access, returns T?
DTOs/{Component}/          no `Dto` suffix: `Admission`, not `AdmissionDto`
Data/                      one DbContext, all entities
Agents/                    the AI agents
```

`Controllers/`, `Services/` and `DTOs/` each have a `Common/` folder alongside
the four component folders. **There is no repository layer** — EF Core's `DbSet`
is the repository.

**No facade layer** *(ADR 4)*. The transaction boundary and the throwing both live in
the **service**. `FindByIdAsync` returns `T?` for internal lookups; `GetByIdAsync` throws
`NotFoundException`. The `Find*` / `Get*` naming is what tells you which you are calling —
get it wrong and "not found" means two different things at two layers again.

- **One writer per table; everyone else reads through the owner's service.**
  Ownership is `integration_of_functions.md` §3 — it names the exact methods.
  `Bed` is Equipment's, `BedAssignment` is Patient's, and neither writes the
  other's. That split is decided, not open.
- **AI agents propose, never write.** Every agent output becomes an
  `AgentProposedChange` awaiting human approval, and no agent sets a patient's
  care category.
- **Each component's agent plans and acts internally** — gather → filter → rank →
  decide → propose → deterministic validation → pause for approval.
  `patient-management-plan.md` §8.7 is the template. Chaining the four together
  is `ai-orchestration-workflow.md`, and comes after.

## Data conventions

Base classes are `Entity` → `AuditedEntity` → `SoftDeletableEntity`, defined in
`docs/entity_diagram.md`, which is the authority on every field. **There are no
`CreatedBy` / `UpdatedBy` columns** — who did what lives in the separate
`AuditLog` table.

- **Soft delete via EF global query filters** — `HasQueryFilter(e => e.IsActive)`.
- **Unique constraints on soft-deletable tables are scoped `WHERE is_active`.**
  A plain `UNIQUE` is a live bug: deactivate ward `ICU-1` and you can never create
  another, and because the global filter hides the conflicting row the
  service-layer duplicate check passes and `SaveChanges` throws.
- **Auditing is automatic** — an `ISaveChangesInterceptor` writes the `AuditLog`
  row, taking the staff id from the JWT. Never write one by hand. A soft delete
  logs as `Operation = Delete`, not `Update`.
- **`DateTimeOffset` in UTC** everywhere — Npgsql requires a UTC offset.
- **Migrations are DDL only.** Seed data lives in `docs/seed/*.sql` as parameterised,
  idempotent scripts, so environment-specific ids never reach production.

## One DbContext, four people

**Nobody edits `OnModelCreating`.** Four people adding entity configuration to
one method means all four of us get a merge conflict on every migration.

Each member writes their own configuration classes under
`Data/Configurations/{Component}/`, and the context picks them all up in one
line:

```csharp
protected override void OnModelCreating(ModelBuilder b)
    => b.ApplyConfigurationsFromAssembly(typeof(CareLankaDbContext).Assembly);
```

You own your files; two people adding entities on the same day touch zero shared
lines. `DbSet<T>` properties are the one shared surface — keep them grouped by
component and add yours at the end of your group.

**Migrations conflict badly**, because each one snapshots the whole model:

- Name them `{Component}_{What}` — `Patient_AddAdmission`, `Equipment_AddBed`.
- **Pull `main` immediately before generating one**, and push promptly after.
- On a snapshot conflict: delete your migration, pull, regenerate. Never
  hand-merge the snapshot file.

---

# Frontend conventions

- **Server state lives in TanStack Query**, wired through the generator plugin.
  **Do not fetch in `useEffect`.** Every mutation invalidates the queries it
  changed — a detail mutation invalidates the list too.
- **State machines belong to the backend.** Admission status has an explicit
  transition matrix in `specs/patient-spec.yaml`. Mirror it and offer only legal
  moves. A transition with side effects has its own endpoint and the picker must
  not offer it — PATCHing straight to `admitted` skips the approval and the
  approver stamp.
- **Approval gating is the product.** Gate approve/reject on the specific
  permission, **hide** controls the user cannot use rather than disabling them,
  and show the state badge on the detail page.
- **React decides, Flutter does.** Reviewing an AI recommendation belongs in
  React; work done walking a ward, in an ambulance or in a bed belongs in Flutter.
- **In Flutter, work only inside your own `lib/features/<component>/`.** `core/`
  and `pubspec.yaml` are shared — ask the group before changing either.
- **Bulk writes are a loop over one endpoint.** Report *n* of *m* landed; never
  claim the batch succeeded because the first one did.
- **Blocks with no backend**: keep the UI, disable the write path, mark it
  visibly. A mock control that still looks live is worse than a missing one.
- **The generated type is the field list.** A mock field with no column behind it
  is deleted, not faked.

---

# CI

On every PR into `main`: `dotnet build` + `dotnet test`; `bun install
--frozen-lockfile` → `check:codegen` → `typecheck`; `flutter analyze` +
`flutter test`; spec validation and uniqueness checks.
