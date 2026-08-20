# CLAUDE.md

Guidance for Claude Code (claude.ai/code) when working in this repository.

## Current state

**Documentation only — no code yet.** `docs/` holds the design, `specs/` holds
the OpenAPI contracts, `api/` `web-ui/` `mobile-ui/` are empty placeholders. So
this file is prescriptive: conventions the code must follow when it lands. Where
a committed contract in `specs/` already answers a question, that contract wins.

`MY_NOTES.md` is gitignored and never pushed.

## Stack

| Layer | Technology |
| :--- | :--- |
| API | ASP.NET Core Web API (.NET 8) |
| Database | PostgreSQL via Entity Framework Core |
| Web | React + Vite (plain React, not Next.js) |
| Mobile | Flutter (Dart) |
| Auth | Shared JWT, `bearerAuth`, roles in claims |
| CI | GitHub Actions |

**Package manager: `bun`.** Other members use `npm`, which is fine — keep
`package.json` scripts runner-agnostic (no `bun` inside a script body).

---

# API clients are generated, never hand-written

The ASP.NET API is the source of truth. Both frontends generate typed clients
from its OpenAPI document. **Never write `fetch()`, `axios`, `http.get()` or a
hand-rolled model for a CareLanka endpoint.**

```
api/ → /swagger/v1/swagger.json
         ├─ web-ui/    @hey-api/openapi-ts → TS client + types + query options
         └─ mobile-ui/ swagger_parser      → Dart client + models
```

## Backend: publish a truthful spec

- `<Nullable>enable</Nullable>`, or required vs. optional in the spec is meaningless.
- `[ProducesResponseType]` for **every** outcome, error ones included. An endpoint
  declaring only its 200 generates a client that cannot type its failures.
- Wire format is **`snake_case` bodies** (`full_name`) with **`camelCase` query
  params** (`sortDir`) — what `specs/*.yaml` already publish. Use
  `JsonNamingPolicy.SnakeCaseLower` for bodies.
## The hand-written specs are temporary

`specs/*.yaml` were written by hand ahead of the code and are, right now, the
only record of the API design — so **do not delete them yet**. They stop being
the contract the moment the code that would generate them exists.

Per component, when its controllers are implemented:

1. Build the controllers to match the hand-written spec.
2. Download the generated document over the top of it.
3. Diff the two. Every difference is either a spec the code failed to honour or
   a design decision made in the controller and never written down — resolve it
   deliberately, do not accept the generated side by default.
4. From then on that file is **generated output**: regenerate it, never edit it.

Once all four are generated, the four files should collapse into one
`carelanka.json` — one app publishes one document. Two hand-maintained
contracts for one API is the desync this whole section exists to prevent.

## web-ui

```ts
// openapi-ts.config.ts
export default defineConfig({
  input: "./src/services/openapi/carelanka.json",
  output: { path: "./src/services/openapi/carelanka", clean: true },
  plugins: [
    { name: "@hey-api/client-fetch", baseUrl: false,
      runtimeConfigPath: "./src/services/api/runtime" },
    "@tanstack/react-query",
  ],
});
```

`baseUrl: false` drops the spec's `servers` entry, which names whichever host
published it. The real base URL is the relative path `/api`, set in
`src/services/api/runtime.ts`, with dev proxying `/api` to the backend —
relative in dev and production alike. **No base-URL environment variable**: it
buys a CORS surface and an environment contract for nothing.

```
src/services/openapi/carelanka.json   committed specification
src/services/openapi/carelanka/       GENERATED — disposable, never edit
src/services/api/                     the only hand-written seam
  runtime.ts       base URL only, consumed by the generator
  transport.ts     credential, interceptors, error handling — all HTTP
  query-client.ts  TanStack Query defaults
  session-bridge.tsx  the whole of the React coupling for auth
```

`transport.ts` imports the generated client, so `runtime.ts` must import nothing
leading back to it — the client calls `createClientConfig` while still
initialising, and a cycle there is a startup crash.

Scripts: `download:spec` (curl the swagger.json), `generate:spec` (`openapi-ts`),
`check:codegen`, `typecheck` (`tsc --noEmit`).

## mobile-ui

```yaml
# swagger_parser.yaml
swagger_parser:
  schema_path: lib/services/openapi/carelanka.json
  output_directory: lib/services/api_client
  language: dart
  json_serializer: json_serializable
```

`dart run swagger_parser`, then `dart run build_runner build
--delete-conflicting-outputs`. Config keys move between major versions — check
the pinned package's README if an option is rejected.

**Open decision:** is `lib/services/api_client/` committed or generated in CI?
`**/*.g.dart` is already gitignored, so this is currently ambiguous.

## Rules

1. **Generated directories are disposable.** Anything written inside one is lost
   on the next run. Fixes go in the ASP.NET code that publishes the spec.
2. **Generated types are the single source of truth for domain shapes.** Import
   `Admission` from the generated types, never a hand-written interface.
   `src/types/*` holds UI-only concerns — permission unions, column definitions,
   presentation maps. Key those off the generated enum so a contract change is a
   compile error.
3. **Regenerate in the same commit as the backend change.**
4. **A drift gate, not discipline.** `check:codegen` runs `generate:spec` and
   fails on any `git status --porcelain` difference in the generated directory,
   catching both a spec updated without regenerating and a hand-edited generated
   file. CI runs it **before** `typecheck` — a stale client makes every
   downstream type error a red herring.

---

# One app, one API surface

The four specs describe **one** ASP.NET application. Routes, `operationId`s and
schema names are therefore global, not per-component. A duplicate route throws
at startup; a duplicate `operationId` or schema name silently collides in the
generated clients.

- **Namespace by component** where a resource is not genuinely shared:
  `/reports/staff/agent-performance`, not a third claim on
  `/reports/agent-performance`.
- **`operationId` must be unique across all four specs** — it becomes the
  generated function name.
- **A schema name shared between specs must be byte-identical.** Where two
  components genuinely need different views of the same thing, give them
  different names (`EquipmentBed` / `AdmissionBed`), not one name and two shapes.
- **Genuinely shared types** (`ProblemDetails`, `PagedResult`, the workflow
  types) have one definition. They are group-owned: change them in all four
  specs in the same commit, or not at all.
- **Same enum name, same values.** A component-specific vocabulary gets a
  component-specific name.

Outstanding, verified against the current branches — each needs an owner:

| Collision | Where |
| :--- | :--- |
| `GET /reports/agent-performance` | staff, equipment, patient |
| `GET /beds` | equipment, patient |
| `GET /workflows/{workflowId}` | equipment, patient |
| `operationId: getAgentPerformanceReport` | staff, patient |
| `operationId: listBeds` | equipment, patient |
| `PagedResult` — staff says `total_count`, the others `total_items` | all three |
| `Urgency` — `[routine, urgent, emergency]` vs `[routine, urgent, critical]` | patient, equipment |
| `Bed`, `WorkflowSummary`, `WorkflowAccepted`, `AgentOutcome`, `AgentPerformanceReport` — same name, different shapes | across specs |

`emergency-spec.yaml` is still a 204-byte stub, which blocks anything that
depends on dispatch notification.

## Validate specs in CI

`npx @apidevtools/swagger-parser validate specs/*.yaml`, alongside the route and
`operationId` uniqueness check. `staff-spec.yaml` currently fails it:

```yaml
key: { type: string, description: Staff name, leave type, ward or month. }
```

Unquoted commas inside a YAML **flow mapping** split the description into
phantom keys — this parses as `{type, description: "Staff name",
"leave type": null, "ward or month.": null}`. Quote any inline description
containing a comma. Nothing catches this by eye; the validator catches it every
time.

---

# Exception handling

`specs/*.yaml` commit to `application/problem+json` with `ProblemDetails` /
`ValidationProblemDetails` — ASP.NET Core's native shape, and what the clients
generate against. Do not wrap responses in a custom envelope.

## Typed exceptions

One base carrying status, machine-readable code and message parameters; one
subclass per HTTP status:

```csharp
public abstract class ApiException : Exception
{
    public int Status { get; }
    public MessageCode Code { get; }
    public string[] Params { get; }
}

NotFoundException          // 404
BadRequestException        // 400
ForbiddenException         // 403
ConflictException          // 409
IllegalTransitionException : ConflictException
```

`IllegalTransitionException` is its own type because the specs define explicit
state machines and document illegal moves as a distinct response.

**Services return, facades throw.** `FindByIdAsync` returns `T?`; the facade
turns `null` into `NotFoundException`. Otherwise "not found" means two different
things at two layers.

## One central handler

A single `IExceptionHandler` via `AddExceptionHandler` + `UseExceptionHandler`:

- `ApiException` → `ProblemDetails` at the right status
- validation / model-state failures → `ValidationProblemDetails`
- everything else → 500, generic message, **no internal detail**
- code into `extensions["code"]`, trace id into `extensions["traceId"]` and a header
- log `Error` for 5xx, `Information` for 4xx — a 404 is not an incident

**No `try/catch` in controllers.** A controller that reshapes an error produces
a response the client cannot classify.

## Message codes

Stable enum codes, human text in a resource file keyed by code, parameterised
with `{0}`:

```
cl_err_004   The requested resource was not found
cl_pat_001   Could not find patient with id {0}
cl_adm_003   Admission {0} cannot move from {1} to {2}
```

Prefixes: `cl_err_` shared, then `cl_emg_`, `cl_stf_`, `cl_equ_`, `cl_pat_`.
Clients branch on the code; the text stays translatable, so Sinhala/Tamil is a
resource-file change rather than a code change.

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

**Controller → Facade → Service → Repository → Entity**, DTOs separate from EF
entities.

```
Controllers/{Component}/   thin — bind, delegate, return. No business logic.
Facades/{Component}/       orchestration, mapping, transactions, throwing
Services/{Component}/      business logic + data access, returns T?
Data/Entities/             EF entities
Models/{Component}/        DTOs — no `Dto` suffix: `Admission`, not `AdmissionDto`
Agents/{Component}/        the AI agent for that component
```

- The transaction boundary is the **facade**, not the service.
- **A component never reaches into another component's tables.** Cross-component
  reads go through the owning component's service interface;
  `docs/integration_of_functions.md` names the exact methods.
- **AI agents propose, never write.** Every agent output becomes an
  `AgentProposedChange` awaiting human approval.

## Data conventions

```
Entity (Id, CreatedAt, CreatedBy)
  └── AuditedEntity (+ UpdatedAt, UpdatedBy)
        └── SoftDeletableEntity (+ IsActive, DeletedAt)
```

- **Soft delete via EF global query filters** — `HasQueryFilter(e => e.IsActive)`.
- **Unique constraints on soft-deletable tables must be scoped `WHERE is_active`.**
  A plain `UNIQUE` is a live bug: deactivate ward `ICU-1` and you can never create
  another, and since the global filter hides the conflicting row the service-layer
  duplicate check passes and `SaveChanges` throws. Detail in `docs/entity_diagram.md`.
- **Auditing is automatic** — an `ISaveChangesInterceptor` fills `CreatedBy` /
  `UpdatedBy` from the JWT. Never set audit fields by hand.
- **`DateTimeOffset` in UTC** everywhere — Npgsql requires a UTC offset.
- **Migrations are DDL only.** Seed data lives in `docs/*.sql` as parameterised,
  idempotent scripts, so environment-specific ids never reach production.

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
  React. Work done walking a ward, in an ambulance, or in a bed belongs in Flutter.
- **Bulk writes are a loop over one endpoint.** Report *n* of *m* landed; never
  claim the batch succeeded because the first one did.
- **Blocks with no backend**: keep the UI, disable the write path, mark it
  visibly. A mock control that still looks live is worse than a missing one.
- **The generated type is the field list.** A mock field with no column behind it
  is deleted, not faked.

---

# CI and docs

On every PR into `main`: `dotnet build` + `dotnet test`; `bun install
--frozen-lockfile` → `check:codegen` → `typecheck`; `flutter analyze` +
`flutter test`.

`docs/` is prose design, `specs/` is contracts. A design document and its spec
move together — `patient-management-plan.md` states that `patient-spec.yaml` and
`integration_of_functions.md` follow from its decisions, so changing one means
changing all three. Cross-document references are bare filenames in prose, not
relative links. When a component needs data it does not own, the boundary is
negotiated in `docs/integration_of_functions.md` first.
