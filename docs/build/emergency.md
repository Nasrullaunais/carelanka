# Build Track 1 — Emergency / Ambulance

**Owner: Kaveesha (Member 1)** · **Index:** `docs/BUILD_PLAN.md`

**Owns:** `EmergencyCall`, `Ambulance`, `Dispatch`, `DispatchCrew`, `RouteLog`
**Contract:** `specs/emergency-spec.yaml` (33 paths) · **Design:** `specs/emergency-management-plan.md`
**Boundaries:** `specs/integration_of_functions.md` §22–§26

> **You own the project's only third-party integration.** The maps API is step 6 and it
> satisfies assignment §4.1 and §11 for the *whole group*. Nobody else has a fallback if it
> does not land, so do not leave it to the end.

---

## Steps

| # | Step | Notes |
| :-- | :--- | :--- |
| 1 | Entities + configurations + migration | Five entities. Mind the partial unique index `UNIQUE(ambulance_id) WHERE status IN ('assigned','en_route')` — it is the real guarantee against double-booking, not an application check |
| 2 | Ambulance CRUD end to end | Simplest entity. Proves the stack: service → controller → Swagger → one test |
| 3 | Call intake + the three status enums | `CallStatus`, `DispatchStatus`, `AmbulanceStatus`. Write them in one transaction so they cannot disagree |
| 4 | **Manual dispatch, no AI** | Assign an ambulance to a call by hand. This is exactly what the agent will later propose — get it right first |
| 5 | The divertibility rule | `at_scene` / `transporting` are never divertible. Enforced in C# in three places (plan §5.2). **Test this hardest** — it is the safety rule of the whole component |
| 6 | **Maps API integration** | ETA ranking, `RouteLog`, reverse geocode, crew navigation. Include the failure path — a dispatch must never block on the provider being down |
| 7 | Remaining endpoints + codegen gate | |
| 8 | React: call board, fleet map, ambulance register | |
| 9 | Flutter: crew screens, then patient tracking | Two distinct audiences — worth pointing at in the demo |
| 10 | **The agent, last** | Ranks by real ETA, proposes. Both human gates sit on top of the manual path from step 4 |
| 11 | React: dispatch queue + diversion review | The approval UI. Needs the agent to exist |

---

## Things that will bite

**The three status enums all carry `EnRoute`.** `CallStatus`, `DispatchStatus` and
`AmbulanceStatus` are three rows to keep in sync on every transition
(`entity_diagram.md` Open Decision 7). Pick one as authoritative and derive the rest, or
write the sync rule down. Do this at step 3, not after the agent exists.

**The maps provider will be down at some point, probably during a test.** Design the
failure path first: a dispatch falls back to straight-line distance and records that it
did. A dispatch that blocks on a third party is a dispatch that fails when it matters.

**Two human gates, sized differently.** A routine send is one tap. A *diversion* — turning
an ambulance around that is already driving to another call — goes to the Duty Manager with
the cost to that other patient shown. An ambulance that has reached its patient is never
diverted by anyone, by anyone, ever. `emergency-management-plan.md` §5.

**Urgency translation at the M1 → M4 boundary.** Your `critical/high/medium/low` becomes
Patient's `routine/urgent/emergency`. A mismatch is a 400 and it will only show up at
integration checkpoint 3.

---

## Auth

Your roles: `ambulance_crew` (Flutter) and `duty_manager` (React).

`/me/dispatches/*` and `/me/emergency-calls/*` are scoped by the `sub` claim — never take
a crew id or a caller id as a parameter. `docs/build/common.md` §7 has the reasoning.

`POST /emergency-calls` is called by a **patient** account, not staff. That is the one
place your component sees `typ: patient`, and `caller_user_id` is a `PatientAccount.Id`,
not a `Patient.Id`. The bystander case is why: someone ringing in for a stranger has an
account and no medical record.

---

## Dependencies

**You will need to stub:**

| What | From | Contract |
| :--- | :--- | :--- |
| `GET /capacity/wards` | M4 | Free bed counts, to choose a destination |
| `POST /admissions/pre-admit` | M4 | Creates the admission when you dispatch |
| `POST /staff/lookup` | M2 | Rendering "Approved by …" |
| The maps API | third party | Stub it anyway, so you can work offline and test the provider-down path |

**Others are waiting on you for:** `POST /emergency-calls` (M4's patient app posts to it),
and the dispatch notification that triggers M4's pre-admission.
