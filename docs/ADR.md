# CareLanka — Architecture Decision Record

**SE3090 Assignment 1 · Group-owned · Required by assignment §14.2**

One decision per section: what we were deciding, what we could have done, what we did,
and what it costs us. §14.2 asks that we record at minimum the React state approach, the
Flutter state approach, the Agentic AI framework and orchestration method, the database
schema strategy for agent workflow state, and the cloud deployment platform. All five are
here, plus three more that were blocking work.

**These are examinable at the viva.** Every member should be able to argue their own
component's decisions and the group ones below.

| # | Decision | Status | Date |
| :-- | :--- | :--- | :--- |
| 1 | Agentic AI framework and orchestration method | **Accepted** | 2026-09-07 |
| 2 | Language model provider | **Accepted** | 2026-09-07 |
| 3 | Database schema for agent workflow state | **Accepted** | 2026-09-07 |
| 4 | No facade layer | **Accepted** | 2026-09-07 |
| 5 | Enum storage strategy | **Accepted** | 2026-09-07 |
| 6 | React state management | **Accepted** | 2026-09-07 |
| 7 | Flutter state management | **Accepted** | 2026-09-07 |
| 8 | Cloud deployment platform | **Open — needs a group call** | — |

---

## ADR 1 — Agentic AI framework and orchestration method

**Status: Accepted, 2026-09-07**

### Context

Assignment §9 requires at least four distinct agents that plan, delegate, call
allow-listed tools, persist state, validate deterministically, pause for human approval
and either produce an auditable result or fail safely. §2 lists LangGraph, Microsoft Agent
Framework, LlamaIndex, Google ADK "or a custom orchestration approach", and adds a
mandatory rule: a Python agent service must sit behind ASP.NET Core and must never be
called directly by React or Flutter.

Two facts shaped this. First, the demanding parts of §9.1 — deterministic validation,
persisted state, human approval, safe failure — are database and transaction work, not
model work. Our four design documents already specify them in plain C#
(`patient-management-plan.md` §8.7 is the template). Second, we had roughly three weeks
of build time left when this was decided.

### Options considered

| Option | For | Against |
| :--- | :--- | :--- |
| **Python + LangGraph, separate service** | The stack used in the labs; listed first in §2; the graph/state model is a natural fit | §2 forces it behind ASP.NET, so it is a second runtime: second deploy target, second CI job, and an HTTP contract between .NET and Python that both sides must keep in step. Agent tools would reach domain data over HTTP, putting deterministic validation in a different process from the transaction it is meant to guard |
| **C# + Microsoft Agent Framework** | Named in §2, so justification is short; single process; a maintained tool/agent abstraction | Another API to learn under time pressure, and less control over the loop when something misbehaves live during the viva |
| **C# custom orchestration, in-process** | §2 permits it explicitly. Agents call our own services as typed C# methods, so tool inputs are validated by the compiler and validation runs inside the same EF transaction as the write it guards. One process, one deploy, one CI job | We build the plan/step/retry loop ourselves. "Custom" must be argued at the viva as a real agent architecture and not a pile of `if` statements |

### Decision

**Custom orchestration in C#, running inside the ASP.NET Core API.** Five agents live
under `api/Agents/`: a group-owned Coordinator plus one domain agent per member.

The Coordinator receives an allow-listed objective, produces an ordered plan, persists it
*before* running anything, delegates each step to a domain agent, collects the results and
assembles one approval package. It holds no domain tools — its allow-list is only "invoke
domain agent X" and "read workflow state" — and it never writes domain data.

Each domain agent runs the same internal loop: gather through allow-listed tools, filter,
rank, decide, propose, hand to a deterministic validator, pause. A tool is a typed C#
method on the owning component's service, so an agent physically cannot call a tool that
does not exist or pass it a wrongly-shaped argument.

The language model is used for **ranking and explanation only**. Every hard rule —
gender policy, isolation, skill currency, stock thresholds, divertibility — is checked in
ordinary C# against the database, before the proposal reaches a human and again under a
row lock at the moment of approval. The model never has the last word on anything that
touches a patient.

### Consequences

- One process, one deploy, one CI job. The whole system starts with `dotnet run`.
- Deterministic validation runs in the same transaction as the write it guards. This is
  the property a separate Python service could not have given us.
- We must be able to defend "custom" as genuinely agentic at the viva. The defence is
  §9.1's own checklist: a persisted structured plan, distinct roles with typed I/O
  contracts, per-agent tool allow-lists, deterministic validation, an approval pause and
  recorded safe failure. Every one of those is implemented and demonstrable.
- We give up the lab-stack familiarity of LangGraph. If a marker asks why not LangGraph,
  the answer is the mandatory-backend rule in §2: it would have doubled the deployable
  surface without changing what the agents do.
- **The bar this sets:** the loop must be a real one. Reviewers should be able to point at
  the plan row, the step rows, the tool names and the validation verdicts in PostgreSQL.

---

## ADR 2 — Language model provider

**Status: Accepted, 2026-09-07**

### Context

§14 requires that the assignment be completable on "institution-provided or no-cost
services" and that paid subscriptions are not required. §16 and §17.1 require the agent
subsystem to actually execute during the demonstration, and §18.2 forbids any external AI
assistant from helping us at that point — so whatever we pick has to work unattended, on
the day, on someone's laptop or a deployed host.

### Options considered

| Option | For | Against |
| :--- | :--- | :--- |
| **Google Gemini free tier** | Free key, no card, generous quota, reliable from Sri Lanka, strong structured/JSON output — which matters because §9.1 requires structured outputs and schema validation | A network call. A dead key, an exhausted quota or bad conference wifi during the viva takes the demo with it |
| **Groq free tier** | Free, no card, very fast — latency is reported in the §12 performance section | Structured output is less reliable than Gemini's, costing extra parsing and repair code |
| **Local Ollama only** | No key, no network, no quota, cannot be rate-limited mid-viva | Needs a capable machine; small local models follow tool and JSON instructions noticeably worse; every member needs it installed to run the test suite |

### Decision

**Google Gemini free tier**, reached through one `ILanguageModel` interface in
`api/Agents/`. Model name and API key come from configuration, never from source.

The interface exists so the provider is one DI registration. Nothing in an agent knows
which model answered.

### Consequences

- No cost, no card, satisfies §14.
- Strong JSON-mode output means the schema validation §9.1 asks for is checking a
  well-formed object rather than repairing a malformed one.
- **The demo depends on network and quota.** Mitigation, and it is required not optional:
  agent evaluation tests (§12) run against recorded fixtures, not the live API, so CI
  never burns quota and never goes red because a third party is down. Before the
  demonstration, verify the key and quota the same day.
- The API key is an environment variable, absent from Git, listed by name only in the
  README as §15 requires.
- Because `ILanguageModel` is an interface, adding a local Ollama fallback later is a
  registration change. We have not built one; if the group wants demo insurance, this is
  the cheapest place to buy it.

---

## ADR 3 — Database schema for agent workflow state

**Status: Accepted, 2026-09-07 — closes `integration_of_functions.md` §11.2**

### Context

All five agents must persist workflow id, objective, plan, completed steps, tool results,
validation results, errors, retries, approval status and final outcome (§9.1
Observability). §10 additionally requires one workflow that crosses all four components
and can be traced end to end.

This was the longest-open question in the project. It is scored under a **group**
criterion — "Integrated Architecture, Agent Orchestration and State Management (10)" — not
an individual one, and while it stayed open, `staff-spec.yaml` and `emergency-spec.yaml`
were both already written against a `GET /workflows/{workflowId}` that nobody owned.

### Options considered

| Option | For | Against |
| :--- | :--- | :--- |
| **One schema per component** | Each member owns their own table outright; no coordination needed | The §10 cross-component trace becomes a four-way join across four differently-shaped tables. Four people would each design and test the same thing four times |
| **One generic pair, group-owned** | One trace, one query, one approval gate. Written once | Needs a group owner, and the shape has to suit four different agents |
| **JSON blob on each domain row** | Trivial to write | No referential integrity on proposed writes, no way to filter the approval queue by content without `jsonb_path_query`, and no chain |

### Decision

**One group-owned pair of tables, `AgentWorkflow` and `AgentProposedChange`**, already
designed in `docs/entity_diagram.md`. Owned by the group and built as part of the common
bootstrap; every component links to them by `workflow_id` and otherwise leaves them alone.

The general rule this establishes: **anything that does not belong to a specific member is
common.** Common parts are built once by the member holding the common track, not four
times.

- One `AgentWorkflow` row per agent run. Runs in one coordinated plan share a
  `correlation_id`; `parent_workflow_id` points at the Coordinator's row. One query
  returns the whole chain.
- `Plan`, `CompletedSteps`, `ToolResults`, `ValidationResults` and `Errors` stay `jsonb` —
  genuinely variable in shape across five agents, and a relational schema for them would
  buy nothing.
- Concrete domain writes do **not** go in that JSON. They go in `AgentProposedChange`, one
  row per intended write, with typed nullable foreign keys. That is what gives the
  deterministic validator referential integrity — a proposal cannot reference a
  soft-deleted nurse or a retired bed — and what lets the approval queue filter by content.
- **Raw model reasoning is never persisted.** §6 forbids storing hidden reasoning. The
  `rationale` fields hold a short human-readable summary written for the approver.
- The HTTP surface is `specs/common-spec.yaml`: `GET /workflows`,
  `GET /workflows/{workflowId}`, and the single high-impact gate
  `POST /workflows/{workflowId}/{approve,reject,request-revision}`.

### Consequences

- The §10 trace is one query on `correlation_id`, not a four-way join.
- One approval gate exists for the coordinated plan, which is what §9.1 asks for. Each
  component keeps its own domain approval surface for its own standalone runs, so a member
  can still demo and test alone with the other three unfinished.
- `equipment-spec.yaml` gave up the generic route: its richer view moved to
  `GET /equipment/workflows/{workflowId}` returning `EquipmentWorkflowSummary`.
  `patient-spec.yaml`'s `/bed-workflows/{workflowId}` is no longer an interim name — it is
  now deliberately the Patient-specific detail view alongside the shared one.
  `staff-spec.yaml`'s `ProposedChange`, `ProposedChangeType` and `ValidationResult` became
  `RosterProposedChange`, `RosterProposedChangeType` and `RosterValidationResult`, because
  the generic names now belong to the group-owned shapes.
- A change to these two tables is a group change and affects four people. It is not one
  member's call.

---

## ADR 4 — No facade layer

**Status: Accepted, 2026-09-07**

### Context

`CLAUDE.md` described a `Controller → Facade → Service → Entity` pipeline with the
transaction boundary and exception throwing at the facade. But `api/` had no `Facades/`
folder, and no other document mentioned the layer. `CLAUDE.md` flagged it itself: *"A
facade that exists in three components and not the fourth is worse than neither."*

### Options considered

| Option | For | Against |
| :--- | :--- | :--- |
| **Add `Facades/{Component}/`** | Clean split: services return data, facades own transactions and translate `null` into a 404 | A fourth layer for every operation, in a codebase where most operations are one service call. Four people must adopt it consistently or it is worse than not having it |
| **Drop it; service owns the transaction and throws** | One fewer layer, one fewer file per feature. Still satisfies §5's "service/application layer" | The service both returns `T?` internally and throws `NotFoundException` outward, so the two conventions live in one class and must be applied deliberately |

### Decision

**No facade layer.** `Controller → Service → Entity`. The transaction boundary and the
throwing both move into the service.

The `FindByIdAsync` returns `T?` convention stays for internal lookups. The public service
method a controller calls is the one that throws. So `FindByIdAsync` returns null;
`GetByIdAsync` throws `NotFoundException`. Both live in the same service and the naming is
what tells you which you are calling.

Everything else in `CLAUDE.md`'s exception design is unchanged: one `ApiException`
hierarchy, one central `IExceptionHandler`, `ProblemDetails` out, no `try/catch` in
controllers.

### Consequences

- `api/` matches its documentation. There is no `Facades/` folder and now none is expected.
- One fewer file per feature, across four components and roughly 138 endpoints.
- The risk we accept: a service that both returns `T?` and throws is easier to get
  inconsistent. The `Find*` / `Get*` naming rule is the guard, and it is a review point.
- `CLAUDE.md` and `integration_of_functions.md` §1 both updated in the same commit as this
  decision.

---

## ADR 5 — Enum storage strategy

**Status: Accepted, 2026-09-07**

### Context

The schema carries roughly thirty enums. How they are stored affects **every migration**,
and deciding after the migrations exist means rewriting all of them —
`docs/BUILD_PLAN.md` listed this as a decision that could not wait.

### Options considered

| Option | For | Against |
| :--- | :--- | :--- |
| **Native PostgreSQL enum** | Real type safety in the database; compact | EF Core handles `ALTER TYPE` awkwardly, and our schema has added or changed a dozen enum values across three revisions already. Each one becomes a hand-written migration |
| **`int`** | Smallest, fastest, EF's default | Unreadable in `psql`. A `status = 3` in a bug report means nothing without the C# source. Reordering the enum silently corrupts existing rows |
| **`HasConversion<string>()` + CHECK constraint** | Readable in the database, trivial to extend, and the CHECK keeps integrity | Slightly wider rows; the CHECK must be kept in step with the C# enum |

### Decision

**`HasConversion<string>()` with a CHECK constraint** on every enum column, as
`docs/entity_diagram.md` Open Decision 3 recommended. Values are stored `snake_case`,
matching the wire format the specs publish.

### Consequences

- `SELECT * FROM admissions` is readable during the demo and during debugging. This matters
  more than it sounds: §17.1 requires showing PostgreSQL data changes live.
- Adding an enum value is a value added to a C# enum plus a CHECK constraint update, not an
  `ALTER TYPE`.
- The database column and the JSON wire value are the same string, so there is one
  vocabulary to learn instead of two.
- The cost we accept: the CHECK constraint can drift from the C# enum. It is written in the
  same configuration class as the property, so both are visible in one file.

---

## ADR 6 — React state management

**Status: Accepted, 2026-09-07**

### Context

§7 requires "a suitable state-management approach such as Context API, Redux Toolkit,
Zustand or another justified option". Nearly everything in the React app is *server*
state — admissions, rosters, stock, workflows — not client state. It is fetched, cached,
invalidated and refetched.

### Options considered

| Option | For | Against |
| :--- | :--- | :--- |
| **Redux Toolkit** | Familiar, explicit, good devtools | We would hand-write loading, error, caching and invalidation for every one of ~138 endpoints. That is the bulk of the code and none of it is our business logic |
| **Zustand / Context** | Minimal | Same problem: neither knows anything about server state |
| **TanStack Query, wired to the generated client** | Caching, loading and error states, refetch and invalidation come free. The OpenAPI generator emits query options per endpoint, so a new backend endpoint becomes a typed hook with no hand-written code | A second concept for genuinely client-side state, which needs Context alongside it |

### Decision

**TanStack Query for server state, React Context for the little client state there is**
(the authenticated principal, theme, active ward filter).

Server state is never copied into Context. Every mutation invalidates the queries it
changed, and a detail mutation invalidates the list too.

This is inseparable from our codegen rule: `@hey-api/openapi-ts` generates the typed
client, the types and the query options from the API's OpenAPI document. Nobody
hand-writes `fetch` or a domain type. `check:codegen` fails CI on any drift.

### Consequences

- Loading, empty and error states — which §7 grades explicitly — come from one place
  rather than being reimplemented per screen.
- A backend contract change becomes a TypeScript compile error rather than a runtime
  surprise, because the types are generated from the same document.
- **Do not fetch in `useEffect`.** It bypasses the cache and the invalidation, and it is
  the single easiest way to break this decision without noticing.
- Queries do not retry: `transport.ts` already toasted the failure, so a retry would toast
  it again. Lists render "Try again" instead of an empty table.

---

## ADR 7 — Flutter state management

**Status: Accepted, 2026-09-07**

### Context

§8 requires "a suitable state-management approach". All four members build screens in the
same Flutter app, so this cannot be per-member — switching after screens exist is painful,
and two members using two approaches in one app is worse than either.

`provider` was already listed in `mobile-ui/pubspec.yaml` as the agreed baseline, but had
never been confirmed as a decision.

### Options considered

| Option | For | Against |
| :--- | :--- | :--- |
| **Bloc** | Explicit, testable, well suited to the status machines we have | The most boilerplate of the three, and the steepest to learn while also learning Dart |
| **Riverpod** | Compile-safe, no `BuildContext` dependency, better testing story than `provider` | A different mental model again, and nobody has used it |
| **`provider`** | Already in `pubspec.yaml`; the smallest thing that satisfies §8; `ChangeNotifier` is enough for screens that are mostly "load, show, act, reload" | Not compile-time safe — a missing provider is a runtime error. Scales poorly on very complex screens |

### Decision

**`provider`, confirmed for all four members.** One `ChangeNotifier` per feature screen,
holding loading / data / error explicitly so the three states §8 grades cannot be
forgotten.

Screens do not call `http` or `dio`. Domain shapes come from the generated `api_client`; a
feature's `services/` wraps that client; `core/network/` owns the JWT header and error
handling once for everyone. This overrides `mobile-ui/README.md`'s per-feature `models/`
folder, which predates the codegen decision.

`flutter_secure_storage` holds the tokens — §8 requires secure token storage, and
`SharedPreferences` is not that.

### Consequences

- One approach across four members' features, which is the point.
- Cheapest option to learn with three weeks left, and enough for screens that are mostly
  load / show / act / reload.
- The cost: `provider` is not compile-safe, so a screen reading a provider that is not
  above it in the tree fails at runtime, not at build. Widget tests are the guard.
- Each member works only inside `lib/features/<component>/`. `core/` and `pubspec.yaml`
  are shared — changing either needs the group.

---

## ADR 8 — Cloud deployment platform

**Status: OPEN — needs a group decision. This one is not settled.**

### Context

§14 requires the ASP.NET Core API deployed with a working health and Swagger URL,
PostgreSQL deployed with restricted credentials, React deployed and live, and a runnable
Android APK. §15 requires all of it reachable by evaluators until 21 October 2026. §14 also
requires that it all be achievable on no-cost services.

`GET /health` is specified in `specs/common-spec.yaml`, so the health URL requirement has
somewhere to point once a host exists.

### Options on the table

| Option | Notes |
| :--- | :--- |
| **Azure for Students** | $100 credit with a student account. App Service plus Azure Database for PostgreSQL. Closest to the .NET stack and the least friction for an ASP.NET deploy |
| **Render / Railway free tier + Neon or Supabase Postgres** | Free, straightforward Docker deploy, generous Postgres free tiers. Free instances sleep when idle, which is a live risk on demo day |
| **Fly.io + Neon** | Free allowance, good for a container, region close to Sri Lanka |

React can go to Vercel, Netlify or GitHub Pages under any of the three; it is a static
build and is not the hard part. The database and the API are.

### Why it is still open

Nobody has been assigned it and nobody has verified which free tiers are actually
available to this group. It is the last of §14.2's five required decisions and the only
one still unmade.

**Blocking on:** one person trying an actual deploy and reporting back. Until that
happens, this section is a placeholder and the group cannot claim §14.

### Consequences of leaving it

The deployment evidence is worth marks under "Documentation and Deployment (10)", and
"evaluator access is clear and setup is fully reproducible" is the difference between the
top band and the one below. A deployment attempted in the final week usually discovers a
connection-string or migration problem that takes a day.
