-- CareLanka seed 002 — wards (Patient Management). Run after the migrations, and after
-- 001_identity.sql. Idempotent: safe to run twice, and it never deletes a row.
--
-- Why this file exists: the first three wards anybody typed in were ICU-1, Maternity-A and
-- Pediatric-B — placeholders that made the ward list read like a test fixture. A real
-- hospital's ward board is the set below, and both the bed-placement rules and the capacity
-- screen only mean something against a realistic one.
--
-- Two things a reader needs to know before changing it:
--
--   * `ward_type` is what care the ward can give, not how the patient got there. There is no
--     'emergency' type on purpose — an Emergency Treatment Unit's beds are ordinary beds, and
--     "came in by ambulance" is already `admissions.source`. Putting it in both places would
--     make it possible for the two to disagree.
--
--   * `gender_policy` is a property of the ward and is never bent. Male and female wards take
--     only that gender; a patient recorded as `other` or `unknown` can *only* be placed in a
--     mixed ward, so ICU, HDU, ETU, isolation and pediatric are mixed — real intensive care
--     units are open bays, and an unidentified arrival has to land somewhere by rule.
--
-- Beds are Equipment Management's table, so this file does not create any. A ward with no
-- beds is real but unusable: it shows on the capacity screen with a total of zero and offers
-- nothing to assign. Add beds through POST /api/beds.

-- Step 1 — rename the placeholders onto their real names rather than retiring them, so any
-- beds and live bed assignments already sitting in them survive. Matched loosely on purpose:
-- nobody wrote the placeholder names down and "ICU-1", "ICU - 1" and "ICU 1" all happened.
--
-- Each rename is skipped if the real name is already taken, which is what makes re-running
-- this safe: the partial unique index would otherwise throw on the second pass.

UPDATE wards SET name = 'Intensive Care Unit', ward_type = 'icu', gender_policy = 'mixed',
                 updated_at = now()
WHERE is_active
  AND name ILIKE 'icu%' AND name <> 'Intensive Care Unit'
  AND NOT EXISTS (SELECT 1 FROM wards w WHERE w.is_active AND w.name = 'Intensive Care Unit');

UPDATE wards SET name = 'Maternity Ward', ward_type = 'maternity', gender_policy = 'female',
                 updated_at = now()
WHERE is_active
  AND name ILIKE 'maternity%' AND name <> 'Maternity Ward'
  AND NOT EXISTS (SELECT 1 FROM wards w WHERE w.is_active AND w.name = 'Maternity Ward');

UPDATE wards SET name = 'Pediatric Ward', ward_type = 'pediatric', gender_policy = 'mixed',
                 updated_at = now()
WHERE is_active
  AND (name ILIKE 'pediatric%' OR name ILIKE 'paediatric%') AND name <> 'Pediatric Ward'
  AND NOT EXISTS (SELECT 1 FROM wards w WHERE w.is_active AND w.name = 'Pediatric Ward');

-- Step 2 — the ward board itself. Ten wards, which is a small district hospital: two levels
-- of critical care, an emergency unit, single-sex medical and surgical wards, and the three
-- specialist wards. The single-sex split is doubled because it has to be — one "General Ward"
-- with a male policy leaves every female inpatient unplaceable.

INSERT INTO wards (id, name, ward_type, gender_policy, created_at, updated_at, is_active, deleted_at)
VALUES
    (gen_random_uuid(), 'Intensive Care Unit',          'icu',       'mixed',  now(), now(), true, NULL),
    (gen_random_uuid(), 'High Dependency Unit',         'hdu',       'mixed',  now(), now(), true, NULL),
    (gen_random_uuid(), 'Emergency Treatment Unit',     'general',   'mixed',  now(), now(), true, NULL),
    (gen_random_uuid(), 'Isolation Unit',               'isolation', 'mixed',  now(), now(), true, NULL),
    (gen_random_uuid(), 'General Medical Ward (Male)',  'general',   'male',   now(), now(), true, NULL),
    (gen_random_uuid(), 'General Medical Ward (Female)','general',   'female', now(), now(), true, NULL),
    (gen_random_uuid(), 'General Surgical Ward (Male)', 'general',   'male',   now(), now(), true, NULL),
    (gen_random_uuid(), 'General Surgical Ward (Female)','general',  'female', now(), now(), true, NULL),
    (gen_random_uuid(), 'Maternity Ward',               'maternity', 'female', now(), now(), true, NULL),
    (gen_random_uuid(), 'Pediatric Ward',               'pediatric', 'mixed',  now(), now(), true, NULL)

ON CONFLICT (name) WHERE is_active DO NOTHING;

-- Step 3 — retire whatever is left of the placeholder era, but only if it is empty.
-- A ward with beds in it is somebody's data: retiring it hides the ward from every read,
-- which leaves its beds pointing at nothing and refusing every placement with
-- cl_pat_019 "ward not in service". So this touches only a ward that (a) still looks like a
-- placeholder and (b) no bed has ever been put in. Anything a teammate created deliberately
-- is left exactly as it is — this script does not get to decide that.
--
-- `beds` is Equipment Management's table and this is a read of it, which is the only thing
-- Patient Management is allowed to do with it.

UPDATE wards SET is_active = false, deleted_at = now(), updated_at = now()
WHERE is_active
  AND (
      name ILIKE 'icu%' OR name ILIKE 'maternity - %' OR name ILIKE 'maternity-%'
      OR name ILIKE 'pediatric - %' OR name ILIKE 'pediatric-%'
      OR name ILIKE 'paediatric - %' OR name ILIKE 'paediatric-%'
      OR name ILIKE 'ward %' OR name ILIKE 'test%' OR name ILIKE 'demo%'
  )
  AND name NOT IN (
      'Intensive Care Unit', 'High Dependency Unit', 'Emergency Treatment Unit',
      'Isolation Unit', 'General Medical Ward (Male)', 'General Medical Ward (Female)',
      'General Surgical Ward (Male)', 'General Surgical Ward (Female)',
      'Maternity Ward', 'Pediatric Ward'
  )
  AND NOT EXISTS (SELECT 1 FROM beds b WHERE b.ward_id = wards.id);
