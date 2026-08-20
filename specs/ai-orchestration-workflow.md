# AI Orchestration Workflow

**Owned by the Group · Status: proposal for group review — not yet agreed**

How the four component agents connect into **one** assessed workflow. Everything
below needs a group decision; it is written as a concrete starting point so the
discussion is about a real design rather than a blank page. The open questions
are collected in §7.

---

## 1. What the assignment asks for

Alongside four agents that each do their own job well, §9.1 asks for **one**
workflow that ties them together:

> **Minimum assessed workflow** — "At least one assessed workflow must receive a
> domain objective; create a structured multi-step plan; delegate steps to
> distinct agent roles; call allow-listed tools using validated inputs and
> structured outputs; persist workflow state; apply deterministic checks such as
> schema or business-rule validation; pause a defined high-impact action for
> approval by an authorized user; and produce either an auditable result or a
> safe, clearly recorded failure."

§10 adds that the same workflow should cross both clients — start in one, get
approved in the other, and return the result to whoever started it.

The university's sample project (AutoCare AI) shows the shape: four components
one per student, four agents, and the first is a **Planning Agent** that
"converts the customer objective into a structured plan and delegates tasks."
All four then run inside a single workflow with one validation step, one human
approval and one final transaction.

---

## 2. Where we already are

Good news: `CareLanka_Component_Plan.md` §6 already describes the right chain —
one emergency call fans out to all four agents, then the Duty Manager approves the
whole plan. Structurally that is the sample's shape already.

**Each member's own agent design is fine and does not change.** The internal loop
— plan the steps, gather via allow-listed tools, filter, rank, decide, propose,
hand to a deterministic validator, pause for approval — is exactly right.
`patient-management-plan.md` §8.7 is the worked example; the other three can
follow that pattern inside their own component and get on with it.

Three things are left for later, once the four agents exist:

| To do | Related |
| :--- | :--- |
| Something to create the plan and delegate — the §6 sequence is prose today | §3 below |
| Pick an owner for the shared `AgentWorkflow` tables | `integration_of_functions.md` §11.2 |
| `emergency-spec.yaml` — the workflow starts with a dispatch | `integration_of_functions.md` §11.6 |

**None of this blocks anyone.** Build your own agent first; this layer goes on
top afterwards and does not change what sits underneath it.

---

## 3. Proposal: a fifth Coordinator Agent, group-owned

Add one **Coordinator Agent** above the four domain agents, once those exist.
Keep all four domain agents exactly as designed — nothing below changes.

Why a fifth rather than converting one of the four: §3 requires every student to
have "a distinct Agentic AI contribution," and §9.1 asks for "**at least** four
distinct agents" — so five is allowed and everyone keeps their own domain agent.

| Agent | Owner | Responsibility |
| :--- | :--- | :--- |
| **Coordinator** | Group | Receives the objective, produces a structured plan, delegates each step to a domain agent, collects results, assembles one approval package |
| Dispatch & Routing | M1 | Which ambulance, which route, which destination ward |
| Patient Admission & Bed | M4 | Which ward and bed, or a flagged downgrade |
| Staff Allocation | M2 | Whether the destination ward is staffed, and what to reallocate |
| Equipment Monitoring | M3 | Whether the destination ward has the equipment it needs |

**The Coordinator proposes and delegates. It never writes domain data**, and it
holds no domain tools — its allow-list contains only "invoke domain agent X" and
"read workflow state." Every domain write still goes through the owning
component's service, behind human approval, exactly as `integration_of_functions.md`
§2 requires.

---

## 4. The assessed workflow, end to end

```
Patient taps "I need an ambulance"                      [Flutter · screen M4, endpoint M1]
        │  objective: "get this caller emergency care"
        ▼
COORDINATOR — plan
  produces an ordered plan and persists it before running anything
  1 find an ambulance and a destination ward     -> Dispatch agent
  2 find a bed in that ward                      -> Bed agent
  3 check that ward is staffed                   -> Staff agent
  4 check that ward has the equipment            -> Equipment agent
        │
        ▼
  step 1 → Dispatch & Routing Agent      [M1] → proposes ambulance + route + ward
  step 2 → Patient Admission & Bed Agent [M4] → proposes bed, 30-min hold, may flag downgrade
  step 3 → Staff Allocation Agent        [M2] → flags short-staffing, proposes reallocation
  step 4 → Equipment Monitoring Agent    [M3] → ready / not_ready for that ward
        │
        │  each step writes its own AgentProposedChange rows, linked by correlation_id
        ▼
DETERMINISTIC VALIDATION — plain C#, no model involved
  each component re-checks its own hard rules; the coordinator checks the plan is
  internally consistent (the bed is in the ward the ambulance was routed to)
  any failure → back to that step for revision, or safe recorded failure
        │
        ▼
HUMAN APPROVAL — the one high-impact gate
  Duty Manager reviews the whole plan in React: route, bed, staffing, equipment,
  every tool call, timings, validation results
        │
   ┌────┴─────────────┐
APPROVE          REJECT / REVISE
   │                  └─► back to the named step; admission stays awaiting_bed
   ▼
COMMIT — one transaction, re-checked under a row lock
  dispatch confirmed, bed assigned, reallocation applied, equipment reserved
        │
        ▼
Crew and ward nurse get their tasks; patient sees their ward and bed  [Flutter]
```

This satisfies §10 in one run: begins in Flutter, through ASP.NET Core and
PostgreSQL and the agents, approved in React, status returns to the initiating
user in Flutter.

**A domain agent still runs alone.** Nothing above stops M4's bed agent being
invoked directly for a walk-in admission with no dispatch. The coordinator is the
path for the *assessed* workflow, not the only way in — which matters because each
member must be able to demo and test their component without the other three
finished.

---

## 5. Shared state — resolves `integration_of_functions.md` §11.2

One `AgentWorkflow` row per agent run, all rows in one chain sharing a
`correlation_id`, with `parent_workflow_id` pointing at the coordinator's row.
Both columns already exist in `entity_diagram.md`. Concrete proposed writes go in
`AgentProposedChange`, also already designed.

**Proposed owner: the group**, since this document is already group-owned and
four separately designed schemas would make the §10 trace a four-way join.
Each component links to it by `workflow_id` and otherwise leaves it alone.

Persisted per §9.1 Observability: workflow id, objective, plan, completed steps,
tool calls with inputs/outputs/timings, validation results, errors, retries,
approval status, final outcome.

---

## 6. What each member owns

| Member | Owns |
| :--- | :--- |
| **All four** | Their own domain agent: responsibility, structured I/O contract, allow-listed tools, error handling, its own deterministic validator, tests |
| **The group** | The Coordinator Agent, the shared workflow tables, this document |

Each domain agent must publish, in its own `*-management-plan.md`: its objective,
its input and output contract, its tool allow-list, its hard rules and who
enforces them, and what "safe failure" looks like for it.
`patient-management-plan.md` §8.4–§8.9 is the template.

---

## 7. Open decisions

1. **Do we accept the fifth Coordinator Agent, and who builds it?**
   The alternative is a deterministic C# orchestrator with no LLM in the planning
   step. That is simpler and more reliable in a demo, but the §9.1 wording asks
   for a plan to be *created*, so it needs a deliberate justification in the ADR.
2. **Framework.** LangGraph is what the labs use and what §2 lists first. If the
   agents run as a Python service it must be called *by* ASP.NET Core and never
   directly by React or Flutter (§2, mandatory backend rule). Needs an ADR entry.
3. **Does M1 call M4 directly for pre-admission, or does the coordinator drive
   both?** Currently open as `integration_of_functions.md` §11.3. If the
   coordinator is adopted, the coordinator drives both and §11.3 closes.
4. **Blocking:** `emergency-spec.yaml` is still a stub, so the workflow has no
   entry point. See `integration_of_functions.md` §11.6.
