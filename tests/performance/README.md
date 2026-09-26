# Performance tests (k6)

k6 sends many HTTP requests at once and reports how fast and how reliably the API answered.
Scripts are plain JavaScript run by the k6 program; they are not part of `dotnet test` or
`npm test`.

**Status: tooling only.** The load scenarios are written once the API is frozen (after
30 Sep), so the numbers describe the version we submit.

## Install k6 (once per machine)

k6 is a single program, not an npm package. Pick one:

```powershell
winget install GrafanaLabs.k6     # or: choco install k6
k6 version
```

Or with no install, through Docker (Docker Desktop must be running):

```bash
docker run --rm -i -v "$PWD/tests/performance:/scripts" -e BASE_URL=http://host.docker.internal:5231/api \
  grafana/k6 run /scripts/check-setup.js
```

## Before every run

1. **Use a copy of the database, not your working one.** Write scenarios create rows.
   With the API stopped, `createdb -T carelanka carelanka_perf` (found in
   `C:\Program Files\PostgreSQL\18\bin`) copies the seeded database,
   then point the API at it:
   `$env:ConnectionStrings__CareLanka = "Host=localhost;Port=5432;Database=carelanka_perf;Username=...;Password=..."`.
2. **Turn off SQL logging.** Development prints every SQL command to the console, which slows
   the API down under load and makes the results look worse than they are. In PowerShell
   (the braces are needed because the name has dots in it):
   `${env:Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command} = "Warning"`.
3. Start the API the normal way: `dotnet run --project api --launch-profile http`.
4. Check the tool works end to end: `k6 run tests/performance/check-setup.js`.
   All three checks should pass.

## Writing a scenario

- One file per scenario in `tests/performance/scenarios/`, named after the `PERF-xx` id
  in the report.
- **Log in once, in `setup()`**, with `lib/auth.js`, and pass the token to the test. Login is
  limited per IP address (20 a minute; 200 in Development), so logging in inside the test
  measures the rejections instead of the endpoint.
- **An access token lasts 15 minutes.** Keep a run shorter than that, or requests start
  failing with 401 part-way through.
- Put the pass/fail target in `options.thresholds` (NFR-02 says under 500 ms for normal
  requests), so k6 itself marks the run passed or failed.
- Agent endpoints answer `202 Accepted` straight away and do the work in the background.
  Timing the request only measures the queueing; agent latency means polling the workflow
  until it finishes. Without a Gemini key every agent uses its fixed fallback, which is a
  different number from a real model call — say which one was measured.

## Keeping the evidence

```bash
k6 run --summary-export tests/performance/results/PERF-01.json tests/performance/scenarios/PERF-01.js
```

Setting `K6_WEB_DASHBOARD=true` and `K6_WEB_DASHBOARD_EXPORT=tests/performance/results/PERF-01.html`
also saves an HTML report with graphs, which is what goes in the report's figures.
