# Patient Management handoff — emergency intake

Patient Management owns the Flutter screen; Emergency owns the API and stored call.
The screen uses the generated OpenAPI operations `createEmergencyCall` and
`getMyEmergencyCalls`. It must send the patient's bearer token and must not send a
`caller_user_id`; Emergency reads the caller's `sub` and `typ` JWT claims.

## Create a call

`POST /api/emergency-calls`

```json
{
  "patient_is_caller": false,
  "patient_id": null,
  "latitude": 6.927079,
  "longitude": 79.861244,
  "location_accuracy_metres": 12.5,
  "location_captured_at": "2026-09-14T08:20:00Z",
  "idempotency_key": "9d540e45-941d-4940-a253-2436feec8f3f",
  "details": "A person collapsed near the bus stop"
}
```

Required fields are `patient_is_caller`, both coordinates,
`location_accuracy_metres`, `location_captured_at`, and `idempotency_key`.
`patient_id`, `caller_name`, `caller_phone`, and `details` are optional. For a
bystander report, send `patient_is_caller: false` and leave `patient_id` null.

The mobile app creates one UUID when submission starts and keeps that same
`idempotency_key` for every retry. Repeating it returns the original call and does not
store a second row.

Example `201 Created` response:

```json
{
  "id": "0db6ba48-2506-4e98-9790-d724643ec75b",
  "patient_id": null,
  "caller_user_id": "1d40c832-e29e-4e34-9908-d9131586de10",
  "patient_is_caller": false,
  "caller_name": null,
  "caller_phone": null,
  "latitude": 6.927079,
  "longitude": 79.861244,
  "location_accuracy_metres": 12.5,
  "location_captured_at": "2026-09-14T08:20:00Z",
  "idempotency_key": "9d540e45-941d-4940-a253-2436feec8f3f",
  "address_label": null,
  "details": "A person collapsed near the bus stop",
  "priority": "high",
  "status": "received",
  "outcome": null,
  "transported": null,
  "cancellation_request_status": null,
  "created_at": "2026-09-14T08:20:01Z",
  "updated_at": "2026-09-14T08:20:01Z",
  "dispatches": [],
  "open_proposal_id": null
}
```

Patients must omit `priority`; intake defaults it to `high`. Only a Duty Manager can
set it during intake or change it later. Invalid input returns
`application/problem+json` with the standard `ValidationProblemDetails.errors` map.

## List the caller's calls

`GET /api/me/emergency-calls?status=received&page=1&pageSize=20`

The bearer token scopes this list. There is no caller id query parameter, and calls
raised for another person still appear because ownership follows who reported the call.
The response is a normal `PagedResult<MyEmergencyCallSummary>`.

Emergency intake does not create a Patient record, admission, dispatch, or agent
proposal, and it does not write any Patient Management table.
