# Security scan (OWASP ZAP)

ZAP reads the API's Swagger document, calls every endpoint it lists, then calls them again
with attack inputs (SQL injection, script injection, oversized values, missing headers) and
reports what looked unsafe, graded High / Medium / Low / Informational.

**Status: not run yet.** The scan runs against the frozen API after 30 Sep.

## What gets scanned

- **API scan (main one):** `zap-api-scan.py` against
  `http://localhost:5231/swagger/v1/swagger.json`. Swagger is only served in Development,
  which is how the API runs locally anyway.
- **Baseline scan of the React app (optional):** `zap-baseline.py` against the Vite server.
  This only looks at responses (headers, cookies), it does not attack.

## Before the scan

1. **Scan a copy of the database, never your working one.** The attack phase sends junk to
   every POST, PUT, PATCH and DELETE it finds. With the API stopped:
   `createdb -T carelanka carelanka_scan` (found in `C:\Program Files\PostgreSQL\18\bin`), then set
   `$env:ConnectionStrings__CareLanka` to that database before starting the API.
2. **Remove the Gemini key for the scan** (`$env:LanguageModel__ApiKey = ""`). Otherwise
   every care query and reorder request the scanner sends is a real model call and uses up
   the free-tier quota.
3. Start the API so the container can reach it:
   `dotnet run --project api --launch-profile http --urls http://0.0.0.0:5231`.
   The normal profile only listens on `localhost`, which a Docker container cannot always
   reach. Stop the API after the scan.
4. Get a token for the scan (any staff role; repeat with a patient token for `/api/me/*`):

   ```bash
   curl -s -H "Content-Type: application/json" \
     -d '{"email":"duty.rajapaksa@carelanka.lk","password":"CareLanka#2026"}' \
     http://localhost:5231/api/auth/login
   ```

   Copy `access_token` from the answer. **It expires in 15 minutes**, so start the scan
   straight away. Anything the scanner reaches after that answers 401, and the report must
   say the later part ran without a valid login.

## Running it (Docker Desktop must be running)

```bash
docker run --rm -v "$PWD/tests/security/results:/zap/wrk:rw" \
  -e ZAP_AUTH_HEADER_VALUE="Bearer PASTE_TOKEN_HERE" \
  ghcr.io/zaproxy/zaproxy:stable zap-api-scan.py \
  -t http://host.docker.internal:5231/swagger/v1/swagger.json -f openapi \
  -r zap-api-report.html -J zap-api-report.json
```

`ZAP_AUTH_HEADER_VALUE` makes ZAP send `Authorization: Bearer ...` on every request, so it
tests behind the login instead of only collecting 401s. Run once without it too: that run
shows what someone with no account can reach.

The HTML report is the evidence for the report's SEC table. Each finding is either fixed
(and the scan rerun to show it gone) or explained as not applying, with the reason.

## Installing ZAP without Docker

The Windows installer from zaproxy.org includes its own Java. The desktop app can import the
same Swagger URL (Import → Import an OpenAPI definition from a URL) and run an active scan
by hand, but the Docker command above is what makes the scan repeatable.
