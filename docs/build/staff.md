# Build Track 2 — Staff Management

**Owner: Nasrullah (Member 2)** · **Index:** `docs/BUILD_PLAN.md`

**Owns:** `Skill`, `StaffMemberSkill`, `Shift`, `Allocation`, `LeaveRequest`, `WardStaffingRule`
**Contract:** `specs/staff-spec.yaml` (32 paths) · **Design:** no plan document yet — see below
**Boundaries:** `specs/integration_of_functions.md` §17–§21

> **`StaffMember` itself is common, not yours.** The table, login and JWT are built in
> `docs/build/common.md` §2 because every component depends on the role claim. You own
> everything *about* a staff member's work — skills, shifts, allocations, leave — and you
> own `POST /staff/lookup`. Same owner in this case, different track: do the common one
> first, because three people are blocked on it.

---

## Steps

| # | Step | Notes |
| :-- | :--- | :--- |
| 1 | Entities + configurations + migration | `Allocation` needs `clocked_in_at` / `clocked_out_at`, which the spec publishes but `entity_diagram.md` does not have. Add it to the diagram in the same commit |
| 2 | **`POST /staff/lookup` — build this early** | Every other component needs it to render "Approved by …". Small, and it deletes a stub in three other people's code |
| 3 | Staff CRUD + skills | Deactivate / reactivate is a soft delete — allocations reference the row |
| 4 | Shifts + the overnight-shift rule | `end_time < start_time` means it ends the next day. **Every** overlap query has to apply it |
| 5 | Allocations + coverage calculation | `CoverageStatus` derived from confirmed allocations vs `minimum_headcount` |
| 6 | Leave requests + approval | Approving releases allocations and can drop a shift below minimum |
| 7 | **Manual reallocation, no AI** | Move a nurse between shifts by hand. The agent proposes exactly this later |
| 8 | Codegen gate | |
| 9 | React: staff CRUD, roster grid, coverage dashboard, leave approval | |
| 10 | Flutter: my shifts, clock in/out, request leave/swap | Date/time picker is your device feature |
| 11 | **The agent, last** | The cascading swap. Deterministic validation in plain C#, never the agent checking itself |
| 12 | React: roster proposal approval | |

**Step 0 is done.** `staff-spec.yaml` validates and `PagedResult` uses `total_items`, both
fixed 2026-08-21. Do not go looking for those.

---

## Things that will bite

**Overnight shifts are the recurring bug in this component.** `Shift` splits `Date` +
`StartTime` + `EndTime`, so a 22:00–06:00 shift has `EndTime < StartTime`. Every overlap
check, every coverage count and every double-booking guard has to handle the roll-over.
`entity_diagram.md` Open Decision 6 suggests `StartAt`/`EndAt` as `timestamptz` instead —
worth deciding at step 1, because changing it after the queries exist means rewriting all
of them.

**`StaffMember.Department` is free text next to a `Ward` entity** (Open Decision 5). That
makes "which staff cover this ward" unqueryable — which is precisely what your allocation
agent needs. Either FK it to `Ward` or drop it. Your call, but make it before step 5.

**`Skill` should probably be soft-deletable** (Open Decision 4). It is an admin-editable
lookup and an FK target of `StaffMemberSkill` and `Shift` — the same profile as
`EquipmentCategory`, which *is* soft-deletable. Decide at step 1.

**Skill currency is a date range, not a boolean.** `StaffMemberSkill` carries
`ValidFrom`/`ExpiresAt` because clinical certifications lapse. A nurse who *held* ICU
certification is not a nurse who holds it today, and the agent's validator has to check the
dates, not the row's existence.

**No `staff-management-plan.md` exists.** The other three components each have one, and
yours is the design doc a marker will look for. The spec is good and detailed, so this is a
documentation gap rather than a design one — but it is still a gap, and §9.1 asks each
agent to publish its objective, I/O contract, tool allow-list and safe-failure behaviour
somewhere.

---

## Auth

Your roles: `hospital_administrator` (React) and `general_staff` (Flutter).

`/me/shifts`, `/me/allocations/*`, `/me/leave-requests` are scoped by the `sub` claim.
Never take a staff id as a parameter on those — see `docs/build/common.md` §7.

You also own the role vocabulary in practice: `StaffRole` is byte-identical in
`staff-spec.yaml` and `common-spec.yaml` and changes in both at once. Adding a role means
adding a policy in the common track too.

---

## Dependencies

**You will need to stub:**

| What | From | Contract |
| :--- | :--- | :--- |
| `GET /wards` | M4 | Ward list for shifts and staffing rules |
| `GET /wards/{id}/occupancy` | M4 | Occupancy and care mix, to work out staffing demand |

**Others are waiting on you for:** `POST /staff/lookup` (M1, M3 and M4 all render
"Approved by …"), and the `ambulance_crew` / `doctor` role claims being present on the JWT.
