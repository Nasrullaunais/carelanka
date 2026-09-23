# Emergency end-to-end demonstration

## Prepare a clean database

Apply migrations and run the seed commands in `api/README.md` through
`006_emergency_demo_data.sql`. Start the API, web app, and Flutter app. The fixture leaves
`WP-CAL-101` available near central Colombo with both seeded crew members assigned.

Use these accounts from `TEST_ACCOUNTS.md`:

- Duty Manager: `duty.rajapaksa@carelanka.lk`
- responding crew: `crew.fernando@carelanka.lk`
- second crew: `crew.perera@carelanka.lk`
- linked patient: `demo.emergency`

## Rehearse the workflow

1. Sign into the web app as the Duty Manager, use **Log call** to record a front-desk call,
   and confirm it appears on the live board with its scene location.
2. Open the new call, select **Ask agent**, then open the resulting proposal in the
   proposal queue. Inspect the recommendation, eligibility, crew count, ETA, and
   explanation before confirming it.
3. Sign into Flutter as `crew.fernando@carelanka.lk`. On **My run**, verify the scene
   address, acknowledge, open Google Maps, and progress through travel to the scene and
   transport to hospital. Record handover notes and the patient condition.
4. Verify that Patient Management contains exactly one `awaiting_bed` admission for the
   dispatched call. Continue the cross-component checkpoint through manual bed assignment,
   staffing/equipment checks, and its human approval using those components' runbooks.
5. Sign into Flutter as `demo.emergency`, open **Request ambulance**, capture precise
   location, and submit a patient-owned call. Dispatch it from the web, then confirm the
   patient tracking screen changes to **An ambulance is on the way** without revealing
   crew identities, incident notes, or agent reasoning. Pull to refresh or resume the app
   and verify status, ETA, stale-location warning, and completion updates.
6. Exercise cancellation through the patient UI: cancel one received call directly, then
   submit and dispatch another, request cancellation in Flutter, and approve or reject it
   from the web cancellation queue. Confirm the review state returns to Flutter.
7. Open Emergency reports. The seed supplies two completed runs and accepted/rejected
   proposal outcomes; reconcile their response-time and agent totals before including the
   newly completed run.

The front-desk call and patient-tracked call are deliberately separate. A staff-recorded
phone call has no authenticated patient account to authorize `/me/emergency-calls`; joining
those records implicitly would violate the caller-scoped privacy boundary.

## Dependency-failure checks

- Disable Maps routing and confirm manual dispatch and coordinate-based Google Maps launch
  remain available with an honest fallback indication.
- Disable the dispatch agent and confirm manual dispatch remains available.
- Disable Patient Management temporarily and confirm dispatch and handover continue while
  the pre-admission notice remains retryable.
- Disable push credentials or delivery and confirm the assigned crew recovers the run by
  polling and app resume.

Background Firebase delivery still requires configured credentials and a physical Android
device. Record that check separately; do not treat polling recovery as proof that a
background push was delivered.
