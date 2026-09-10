# Build Track 3 — Health Equipment

**Owner: Sethmin (Member 3)** · **Index:** `docs/BUILD_PLAN.md`

**Owns:** `EquipmentCategory`, `EquipmentItem`, **`Bed`**, `PharmacyCategory`,
`PharmacyItem`, `PharmacyTransaction`, `MaintenanceSchedule`, `Warning`, `ActionRequest`
**Contract:** `specs/equipment-spec.yaml` (28 paths) · **Design:** `specs/equipment-management-plan.md`
**Boundaries:** `specs/integration_of_functions.md` §13–§16

> **`docs/entity_diagram.md` is stale for your component and you own the fix.**
> *(flagged 2026-09-07, Open Decision 12.)* The diagram still has `EquipmentType` and
> `StockLevel`, which appear in no spec, and is missing `PharmacyCategory`, `PharmacyItem`,
> `PharmacyTransaction` and `ActionRequest`, which your spec publishes. The rule is that
> the spec wins — so the diagram needs updating to match, and **§6 and §15 require the ER
> diagram as a graded submitted artefact.** Two questions only you can answer: does
> `StockLevel` survive for equipment consumables, and is pharmacy stock central or
> per-ward (`equipment-management-plan.md` §15 leaves it open)?

---

## Steps

| # | Step | Notes |
| :-- | :--- | :--- |
| 0 | **Reconcile the entity diagram with your spec** | See the banner above. Cheap now; it is a graded artefact |
| 1 | Entities + configurations + migration | Nine entities, the largest set. Consider two migrations: `Equipment_AddCore` and `Equipment_AddPharmacy` |
| 2 | **`Bed` register + `GET /beds` — build this early** | Patient Management's hardest dependency (`integration_of_functions.md` §10). Until it exists, M4's bed agent has nothing to reason over |
| 3 | Categories + equipment items CRUD | |
| 4 | Pharmacy items + the atomic stock decrement | `UPDATE ... WHERE quantity_on_hand >= :qty` — **one conditional update, never read-then-write** |
| 5 | Maintenance schedules | Polymorphic over `equipment_item` and `bed` |
| 6 | **The bed-occupancy check before touching a bed** | Call M4's `GET /beds/{id}/occupancy` inside the same request, before committing. Occupied or held → 409, nothing written |
| 7 | Deterministic warnings | Threshold sweep: low stock, expiring medicine, overdue maintenance. **Not agent-generated** — the agent *reviews* these |
| 8 | Codegen gate | |
| 9 | React: stock dashboard, warning queue, approval queue | |
| 10 | Flutter: report a fault, ward stock, take a bed out of service | Camera for fault photos is your device feature |
| 11 | **The agent, last** | Reviews open warnings, produces a prioritised list with recommended actions |

---

## Things that will bite

**The stock decrement must be one conditional statement.** Read-then-write loses the race:

```sql
-- WRONG: two people dispensing at once both read 5, both write 4, six units leave the shelf
SELECT quantity_on_hand FROM pharmacy_items WHERE id = :id;   -- 5
UPDATE pharmacy_items SET quantity_on_hand = 4 WHERE id = :id;

-- RIGHT: the second one affects zero rows and you return 409
UPDATE pharmacy_items SET quantity_on_hand = quantity_on_hand - :qty
 WHERE id = :id AND quantity_on_hand >= :qty;
```

Check the affected row count. Zero means insufficient stock — that is the check, not a
prior `SELECT`.

**Maintenance never evicts a patient.** Step 6 is the safety rule of your component. You
must ask M4 whether a bed is occupied *before* taking it out of service, in the same
request, before committing.

While M4's endpoint is stubbed, **make your stub answer "occupied"**, not "free". A fake
that answers "free" lets maintenance be scheduled on a bed with a patient in it, and it
will pass all your tests. `STUBS.md` calls this out as the one genuinely dangerous stub in
the project.

**`Bed` is yours; the occupant is not.** You own the frame — it exists, its number, its
condition, repairs, retirement. M4 owns `BedAssignment`, which carries both the 30-minute
hold and the occupancy in one row *(the separate `BedReservation` table was dropped on
2026-09-09 — `patient-spec.yaml` never had one)*. Neither writes the other's table. This is settled (`integration_of_functions.md` §6.1), not open.

**Where does `Cleaning` live?** (Open Decision 9.) A bed being turned over between patients
is a real state, `BedCondition` has no value for it, and neither spec can record it.
Options: a third `BedCondition` value, or a short-lived reservation-style row. **Your
table, your decision** — but M4 triggers it at discharge, so tell them.

**Is Equipment one role or two?** (Open Decision 11.) Your plan §2 is written around
Inventory Administrator (React) and Equipment Technician (Flutter) with different endpoint
permissions. `StaffRole` carries a single `equipment_manager`. One role cannot express that
split — settle it with M2 before you write `[Authorize]` attributes.

---

## Auth

Your role: `equipment_manager` — pending the one-or-two question above.

If it becomes two roles, both need adding to `StaffRole` in **both** `staff-spec.yaml` and
`common-spec.yaml` (byte-identical), plus a policy each in the common track. That is a
conversation with M2 and the common owner, not a solo edit.

`POST /equipment/monitor` is your agent's entry point and may also be called by the
scheduler. It returns 202 and a `workflow_id`; the shared trace is at
`GET /workflows/{workflowId}` and your fuller view at
`GET /equipment/workflows/{workflowId}`.

---

## Dependencies

**You will need to stub:**

| What | From | Contract |
| :--- | :--- | :--- |
| `GET /beds/{id}/occupancy` | M4 | **Make the fake answer "occupied"** so it fails safe |
| `GET /wards` | M4 | Ward list for equipment allocation |
| Admission summary by id | M4 | Showing who an assigned item belongs to |
| `POST /staff/lookup` | M2 | Rendering "Approved by …" |

**Others are waiting on you for:** the **bed register** — this is the big one. M4 cannot
test their bed agent for real until `GET /beds` exists.
