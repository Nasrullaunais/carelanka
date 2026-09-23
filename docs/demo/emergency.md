# Emergency end-to-end demonstration

## Prepare a clean database

Start the Docker API, web app, and Flutter app using `LOCAL_SETUP.md`. The Docker seed leaves
`WP-CAL-101` available near central Colombo with both seeded crew members assigned.
Before dispatch, sign into Flutter as `crew.fernando@carelanka.lk`, open **My run**,
allow precise location, and wait for **Sharing your assigned ambulance location**.
The phone now reports GPS for its assigned vehicle even without an active dispatch.
Keep this screen open on a second phone/emulator for continuous readiness. With one
phone, switch to the patient account and dispatch within five minutes; if the web board
shows a stale position, switch back to the crew account to obtain a fresh GPS fix.
No database edits are needed.

The account `demo.emergency` is linked to a patient with no open admission in a clean demo
database. A completed test creates an `awaiting_bed` admission for that patient; to repeat
the complete pre-admission demonstration from a clean state, reset the local demo database
with `docker compose down -v` and `docker compose up -d --build`. The reset deletes local
database data.

Use these accounts from `TEST_ACCOUNTS.md`:

- Duty Manager: `duty.rajapaksa@carelanka.lk`
- responding crew: `crew.fernando@carelanka.lk`
- second crew: `crew.perera@carelanka.lk`
- linked patient: `demo.emergency`

Staff passwords are `CareLanka#2026`; the patient password is `Patient#2026`.

## Rehearse the workflow

1. Refresh ambulance readiness using the crew login above. In the web app at
   `http://localhost:5174`, sign in as `duty.rajapaksa@carelanka.lk`, open the emergency
   fleet board, and check `WP-CAL-101`: available, two crew, fresh location, eligible.
2. In Flutter, sign in as `demo.emergency`, open **Request ambulance**, allow precise
   location, and submit the request. It should appear in recent requests. Open its
   tracking screen: the call is `received`, with no ambulance assigned yet.
3. On the web emergency board, open that same call. Either dispatch `WP-CAL-101`
   manually, or select **Ask agent**, inspect its proposal in the proposal queue,
   and confirm it. Check that the call becomes `dispatched` and the vehicle becomes busy.
4. Refresh the patient tracking screen: it should say **An ambulance is on the way**.
   Patient tracking exposes the ambulance location and call state, without crew names
   or clinical notes. Patient arrival ETA is currently absent from this response;
   the dispatch board's road travel estimate is separate.
5. Sign into Flutter as `crew.fernando@carelanka.lk`. **My run** should show the same
   request and scene. Acknowledge, open navigation, then progress in order through
   travel to scene, at scene, and transporting to hospital. Complete handover with
   notes and patient condition. The active run should disappear and appear in history.
6. Sign back in as `demo.emergency`, open that request, and refresh tracking. It must
   show `completed`, with no active ambulance location. In the web app's Patient
   Management, find the linked patient's `awaiting_bed` admission. There should be
   exactly one admission for that dispatch; the pre-admission worker retries safely.
   Bed assignment and discharge continue through the Patient Management workflow.
7. Check the emergency reports for the completed run. For cancellation, create a new
   request and cancel it before dispatch: tracking should show `cancelled`. On another
   dispatched request, request cancellation from Flutter, then approve it in the web
   cancellation queue before the crew reaches the scene. Tracking should show both
   the approved review and `cancelled`, and the crew should have no active run.
   A rejected cancellation keeps the existing dispatch active.

Use a second phone/emulator if you want to watch patient tracking while the crew advances.
A staff **Log call** request is a separate scenario: it has no authenticated patient
owner, so it cannot appear under the demo patient's requests. It may create a provisional
patient during pre-admission.

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
