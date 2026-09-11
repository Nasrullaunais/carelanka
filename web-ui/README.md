# web-ui — the React app

Plain React + Vite. Server state in TanStack Query. **The API client is generated,
never hand-written.**

---

## Running it

You need three things up, in this order.

```
1. PostgreSQL, with the migrations applied and both docs/seed/*.sql run
       dotnet ef database update --project api --startup-project api

2. The API, on port 5231
       dotnet run --project api --launch-profile http

3. This app, on port 5173
       npm install
       npm run dev
```

Then open http://localhost:5173 and sign in with an account from `TEST_ACCOUNTS.md`.

**Start the API before this app.** Vite proxies `/api` to `localhost:5231`; without it
every request toasts "Could not reach the server".

`bun` works too — the scripts are runner-agnostic. `npm` is used above because that is
what is installed on the machine this was built on.

---

## This app is for staff only

**There is no patient sign-in here, and there should never be one.**

`CareLanka_Component_Plan.md` §2.1: **React decides, Flutter does.** Anything that reviews
a decision and says yes or no is React; anything done while walking a ward, sitting in an
ambulance or lying in a bed is Flutter. §3 puts both **Patient** and **Ward Nurse** on
Flutter, so neither belongs in this app.

For Patient Management that splits the screens like this:

| | Screens |
| :--- | :--- |
| **React (here)** | Admissions dashboard, bed board, ICU/downgrade bed approval, discharge confirmation, occupancy report |
| **Flutter (`mobile-ui/`)** | Nurse: approve a normal-ward bed, update status, complete details, request discharge. Patient: my stay, book a visit, discharge instructions, ask about a symptom |

`POST /auth/patient/login` exists in the API and is generated into the client here. That is
fine — the generated client mirrors the whole API. It is simply never called from this app.

## What exists so far

| Screen | Who sees it | Endpoints |
| :--- | :--- | :--- |
| **Sign in** | staff | `POST /auth/login` |
| **Wards** | any staff role | `GET /wards`, and `POST /wards` for a hospital administrator only |

**Ward `total_beds` is a stub** — every ward reports six until Equipment Management's bed
register exists. `STUBS.md` row 1, and the screen says so on the page.

---

## The client is generated

```
api/  →  /swagger/v1/swagger.json  →  @hey-api/openapi-ts  →  src/services/api/generated/
```

- **Never write `fetch()` or a model for a CareLanka endpoint.** Import it from the
  generated client.
- **`src/services/api/generated/` is disposable.** Anything edited inside it is gone on the
  next run. Fixes go in the ASP.NET code that publishes the spec.
- **Regenerate in the same commit as a backend change**: `npm run codegen` with the API
  running.
- `npm run check:codegen` regenerates and fails on any difference — a drift gate rather
  than discipline. It needs the API running, so it belongs after `dotnet run` in CI.

The generated types are the single source of truth for domain shapes. `src/types/*` holds
UI-only concerns — permission checks and label maps — keyed off the generated enums, so a
contract change becomes a TypeScript error rather than a blank table cell.

---

## Three things that are easy to get wrong

**`runtime.ts` must not import anything that leads back to the generated client.** The
client calls `createClientConfig` while it is still initialising, and a cycle there is a
startup crash. The one import it has is `import type`, which TypeScript erases.

**There is no base-URL environment variable, deliberately.** The base URL is the relative
path `/api` in dev and production alike. A variable buys a CORS surface and an environment
contract for nothing.

**A page never handles an HTTP status code.** `transport.ts` owns one interceptor that
toasts every failure with the server's own message. Successes are the page's job — there is
no interceptor signal for them. Queries do not retry, and a failed list renders "Try again"
rather than an empty table.

---

## Known caveats

- **Tokens live in `sessionStorage`.** A browser has no secure storage, so the real choices
  were memory only (signed out on every refresh, untestable) or this. A known compromise for
  a coursework app, written down in `src/services/auth/session.ts` rather than hidden.
- **`npm audit` reports 4 high advisories**, all inside `@hey-api/openapi-ts` (its YAML
  parser). That is a build-time code generator; none of it is shipped to the browser. There
  is no non-breaking fix available today.
- **No token refresh yet.** The access token lasts 15 minutes and then the interceptor
  signs you out. `POST /auth/refresh` exists and is generated — wiring it up is not done.
