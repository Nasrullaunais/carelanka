-- CareLanka — medical profiles for the seeded demo patients.
--
-- **Why this file exists:** the Patient Care Advisory Agent reads
-- `patient_medical_profiles`, and with an empty table it has nothing to reason over but
-- demographics — which is the exact complaint that caused the 2026-09-16 redesign. Nobody
-- is going to type four paragraphs of clinical history live in front of an examiner, so the
-- demo patients arrive with one.
--
-- **What it touches:** `patient_medical_profiles` only — Patient Management's own table.
-- It reads `patients` (by patient code) and `staff_members` (by email) and writes neither.
--
-- Run it after `004_patient_demo_data.sql`, which creates the patients it keys off:
--   psql -U postgres -d carelanka -f docs/seed/005_patient_medical_profiles.sql
--
-- Idempotent: re-running replaces each profile rather than adding a second one, which is
-- what `UNIQUE(patient_id)` would refuse anyway.
--
-- **Two patients are deliberately left without a profile** — Kasun Mendis (`P2H4K7M9`) and
-- Nadeesha Wickrama (`P3J8N2Q5`), the two still `awaiting_bed`. An empty profile is the
-- ordinary state of somebody nobody has got to yet, and the agent is supposed to cope with
-- it by drafting from the patient's own words and saying it had no history to work from.
-- If every seeded patient had one, that path would never be demonstrated.

BEGIN;

DO $$
DECLARE
    v_nurse  uuid;
    v_doctor uuid;
BEGIN
    SELECT id INTO v_nurse  FROM staff_members WHERE email = 'nurse.perera@carelanka.lk'  AND is_active;
    SELECT id INTO v_doctor FROM staff_members WHERE email = 'dr.silva@carelanka.lk'     AND is_active;

    IF v_nurse IS NULL OR v_doctor IS NULL THEN
        RAISE EXCEPTION 'Missing seeded staff accounts — run docs/seed/001_identity.sql first.';
    END IF;

    -- Every character below is what a clinician would have typed. Nothing here is a
    -- conclusion the system reached, and there is no diagnosis, no vitals and no lab result
    -- in it — that line is what keeps this out of being an electronic health record.
    INSERT INTO patient_medical_profiles
        (id, patient_id, known_conditions, allergies, current_symptoms, recent_situation,
         updated_by_staff_member_id, created_at, updated_at)
    SELECT
        gen_random_uuid(), p.id, v.known_conditions, v.allergies, v.current_symptoms,
        v.recent_situation, v.author, now() - v.written_ago, now() - v.written_ago
    FROM (VALUES
        -- Admitted, bed assigned. Penicillin is here because it is what rule CR5 checks
        -- against: an agent draft naming it is rejected before any reviewer sees it, and
        -- that check only works because the allergy is a stored field.
        ('P4K9R3T6',
         'Type 2 diabetes, diagnosed 2019. Hypertension, on medication.',
         'Penicillin',
         'Headache since admission, mild fever on arrival.',
         'Admitted after two days of dizziness at home.',
         v_nurse, interval '18 hours'),

        ('P5M2W7X4',
         'Asthma since childhood. Uses an inhaler most days.',
         'Dust, pollen. No known drug allergies.',
         'Shortness of breath and a tight chest since yesterday evening.',
         'Finished a course of antibiotics for a chest infection last week.',
         v_doctor, interval '26 hours'),

        -- Discharged, so the profile is last visit's. Deliberate: the table is one row per
        -- patient, not one per visit, so `current_symptoms` describes whichever stay it was
        -- last written during. A per-admission profile is one a nurse retypes every visit,
        -- and the one that gets retyped is the one that stops being filled in.
        ('PA2C7F5H',
         'High cholesterol. Family history of heart disease.',
         'None recorded.',
         'Chest tightness on exertion, settled during the stay.',
         'Came in after feeling faint at work.',
         v_doctor, interval '3 days'),

        ('PB4D9G2K',
         'None recorded.',
         'Seafood.',
         'Abdominal pain, resolved before discharge.',
         'Two days of nausea before coming in.',
         v_nurse, interval '5 days'),

        -- Outpatients. Thin on purpose: somebody in for a scan gets a line or two, not a
        -- history, and the agent has to read that as "not much known" rather than "nothing
        -- wrong".
        ('PC5E8H3M',
         'Hypertension, well controlled.',
         NULL,
         NULL,
         'Here for a routine scan.',
         v_nurse, interval '2 days')
    ) AS v(patient_code, known_conditions, allergies, current_symptoms, recent_situation,
           author, written_ago)
    JOIN patients p ON p.patient_code = v.patient_code AND p.is_active
    ON CONFLICT (patient_id) DO UPDATE SET
        known_conditions           = EXCLUDED.known_conditions,
        allergies                  = EXCLUDED.allergies,
        current_symptoms           = EXCLUDED.current_symptoms,
        recent_situation           = EXCLUDED.recent_situation,
        updated_by_staff_member_id = EXCLUDED.updated_by_staff_member_id,
        updated_at                 = EXCLUDED.updated_at;
END $$;

COMMIT;
