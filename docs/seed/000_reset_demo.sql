-- CareLanka — wipe the demo data and rebuild the ward board.
--
-- **This deletes patient data. It is a development and demo script and nothing else.**
--
-- What it clears: every patient, visit, bed assignment, discharge and bill, and the whole ward
-- and bed register. What it keeps: the staff logins, the patient login, and Equipment's
-- catalogues — so `TEST_ACCOUNTS.md` still works the moment it finishes.
--
-- Then it rebuilds the eight wards the hospital actually has, and puts beds in them:
-- thirty in the general ward, fifteen everywhere else.
--
-- Run it whole, in one go:
--   psql -U postgres -d carelanka -f docs/seed/000_reset_demo.sql

BEGIN;

-- ---------------------------------------------------------------------------
-- 1. Clear the visit data
-- ---------------------------------------------------------------------------
--
-- Children before parents. `bed_assignments` and `beds` have no declared foreign key between
-- them (they belong to two different components), so the order here is about the rows that DO
-- point at each other: a bill line points at a bill, a bill at an admission, an admission at a
-- patient.

TRUNCATE TABLE
    bill_line_items,
    bills,
    discharge_checklist_items,
    discharges,
    bed_assignments,
    appointments,
    admissions,
    patients
RESTART IDENTITY CASCADE;

-- ---------------------------------------------------------------------------
-- 2. Clear the ward board
-- ---------------------------------------------------------------------------
--
-- `beds` is **Equipment Management's** table. Emptying it is a demo reset, not a schema
-- decision — the bed model is still theirs. Equipment's own catalogues are left alone.

TRUNCATE TABLE beds RESTART IDENTITY CASCADE;
TRUNCATE TABLE wards RESTART IDENTITY CASCADE;

-- ---------------------------------------------------------------------------
-- 3. The eight wards
-- ---------------------------------------------------------------------------
--
-- One ward per kind of care, which is how the hospital talks about them. The old board split
-- the general wards by sex and had ten entries; this one does not, so every ward except
-- maternity takes anybody. Maternity is `female` because that is a property of the ward and
-- hard rule H3 reads it — a male patient is never offered a bed there.

INSERT INTO wards (id, name, ward_type, gender_policy, created_at, updated_at, is_active, deleted_at)
VALUES
    (gen_random_uuid(), 'General Ward',       'general',       'mixed',  now(), now(), true, NULL),
    (gen_random_uuid(), 'ICU',                'icu',           'mixed',  now(), now(), true, NULL),
    (gen_random_uuid(), 'Maternity Ward',     'maternity',     'female', now(), now(), true, NULL),
    (gen_random_uuid(), 'Pediatric Ward',     'pediatric',     'mixed',  now(), now(), true, NULL),
    (gen_random_uuid(), 'Surgical Ward',      'surgical',      'mixed',  now(), now(), true, NULL),
    (gen_random_uuid(), 'Emergency Ward',     'emergency',     'mixed',  now(), now(), true, NULL),
    (gen_random_uuid(), 'Isolation Ward',     'isolation',     'mixed',  now(), now(), true, NULL),
    (gen_random_uuid(), 'Mental Health Ward', 'mental_health', 'mixed',  now(), now(), true, NULL);

-- ---------------------------------------------------------------------------
-- 4. The beds
-- ---------------------------------------------------------------------------
--
-- `has_isolation` means this bed can be shut away from the rest of the ward — a single room
-- off the main bay, with a door. It is a property of the BED, not the ward, which is what hard
-- rule H4 turns on: an infectious patient is only ever offered one of these.
--
-- ICU and the isolation ward are single rooms throughout. Every other ward gets two, which is
-- what stops an infectious patient being unplaceable the moment the isolation ward fills up.

INSERT INTO beds
    (id, ward_id, bed_number, has_isolation, nurse_station_distance, condition,
     asset_tag, created_at, updated_at, is_active, deleted_at)
SELECT
    gen_random_uuid(),
    w.id,
    plan.prefix || '-' || lpad(n::text, 2, '0'),
    n <= plan.single_rooms,

    -- Bed 01 is nearest the nurses' station and the numbers run away from it. The bed agent
    -- uses this to put the sickest patient closest; nothing enforces it, it is just true.
    n,

    'usable',
    'CL-' || plan.prefix || '-' || lpad(n::text, 2, '0'),
    now(), now(), true, NULL
FROM (VALUES
    -- ward name             prefix, beds, single rooms
    ('General Ward',         'GEN',    30,  3),
    ('ICU',                  'ICU',    15, 15),
    ('Maternity Ward',       'MAT',    15,  2),
    ('Pediatric Ward',       'PED',    15,  2),
    ('Surgical Ward',        'SUR',    15,  2),
    ('Emergency Ward',       'EMR',    15,  2),
    ('Isolation Ward',       'ISO',    15, 15),
    ('Mental Health Ward',   'MEN',    15,  2)
) AS plan(ward_name, prefix, beds, single_rooms)
JOIN wards w ON w.is_active AND w.name = plan.ward_name
CROSS JOIN LATERAL generate_series(1, plan.beds) AS n;

-- ---------------------------------------------------------------------------
-- 5. Prices
-- ---------------------------------------------------------------------------
--
-- Left empty on purpose. `BillingRateService` fills every missing cell from the built-in
-- defaults in `BillingRateDefaults.cs`, so the price grid is complete from the first request
-- and a row only ever appears here once the administrator has actually changed something.
-- Seeding 72 rows that match the defaults exactly would only hide which ones were edited.

TRUNCATE TABLE billing_rates, admission_fee_rates RESTART IDENTITY;

COMMIT;

-- What you should have afterwards: 8 wards, 135 beds, no patients.
SELECT
    (SELECT count(*) FROM wards WHERE is_active)    AS wards,
    (SELECT count(*) FROM beds  WHERE is_active)    AS beds,
    (SELECT count(*) FROM patients)                 AS patients;
