-- CareLanka — replace the patient visit data with twelve clean, complete records,
-- two for each of six real-world scenarios.
--
-- The records built up during development testing are broken: some admissions have no
-- category-setter, some discharges have no confirming staff member, some bills settle
-- with nobody recorded as having settled them. This script clears every one of those and
-- replaces them with twelve records that are whole end to end, each pointing at a real
-- seeded staff account (`docs/seed/001_identity.sql`) for every "who did this" field.
--
-- **What it touches:** `patients`, `admissions`, `appointments`, `bed_assignments`,
-- `discharges`, `discharge_checklist_items`, `bills`, `bill_line_items` — Patient
-- Management's own tables. **What it does not touch:** `wards` and `beds` (Equipment's
-- tables — it only reads four existing beds from them) and every table belonging to
-- Emergency, Staff or common auth. Run `001_identity.sql` and `000_reset_demo.sql` first
-- if you have not — this script fails loudly if it can't find the staff accounts or the
-- beds it links to.
--
-- The six scenarios, two patients each:
--   1. Walk-in intake, admitted, no bed assigned yet   — status `awaiting_bed`, no bed row.
--   2. Walk-in intake, admitted, bed assigned          — status `admitted`, bed occupied now.
--   3. Appointment booked, not confirmed yet           — status `scheduled`.
--   4. Appointment confirmed, patient hasn't arrived    — status `confirmed`.
--   5. Discharged                                       — full stay: admitted, bed released,
--                                                          discharged, checklist ticked, billed
--                                                          and settled.
--   6. Billed appointment, never admitted (a scan or a test, not an overnight stay) — status
--                                                          `completed`, billed on the
--                                                          appointment and settled.
--
-- Run it whole, in one go, after 000-003:
--   psql -U postgres -d carelanka -f docs/seed/004_patient_demo_data.sql

BEGIN;

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

DO $$
DECLARE
    v_nurse       uuid;
    v_doctor      uuid;
    v_reception   uuid;

    v_bed_ids     uuid[];
    v_bed_s2_1    uuid;
    v_bed_s2_2    uuid;
    v_bed_s5_1    uuid;
    v_bed_s5_2    uuid;

    -- Scenario 1 — walk-in, admitted, no bed assigned yet
    v_p1 uuid := gen_random_uuid(); v_ad1 uuid := gen_random_uuid();
    v_p2 uuid := gen_random_uuid(); v_ad2 uuid := gen_random_uuid();

    -- Scenario 2 — walk-in, admitted, bed assigned
    v_p3 uuid := gen_random_uuid(); v_ad3 uuid := gen_random_uuid(); v_ba3 uuid := gen_random_uuid();
    v_p4 uuid := gen_random_uuid(); v_ad4 uuid := gen_random_uuid(); v_ba4 uuid := gen_random_uuid();
    v_admitted_3 timestamptz := now() - interval '20 hours';
    v_admitted_4 timestamptz := now() - interval '30 hours';

    -- Scenario 3 — appointment booked, not confirmed
    v_p5 uuid := gen_random_uuid(); v_ap5 uuid := gen_random_uuid();
    v_p6 uuid := gen_random_uuid(); v_ap6 uuid := gen_random_uuid();

    -- Scenario 4 — appointment confirmed, not arrived
    v_p7 uuid := gen_random_uuid(); v_ap7 uuid := gen_random_uuid();
    v_p8 uuid := gen_random_uuid(); v_ap8 uuid := gen_random_uuid();

    -- Scenario 5 — discharged
    v_p9  uuid := gen_random_uuid(); v_ad9  uuid := gen_random_uuid(); v_ba9  uuid := gen_random_uuid();
    v_dc9 uuid := gen_random_uuid(); v_bl9  uuid := gen_random_uuid();
    v_p10 uuid := gen_random_uuid(); v_ad10 uuid := gen_random_uuid(); v_ba10 uuid := gen_random_uuid();
    v_dc10 uuid := gen_random_uuid(); v_bl10 uuid := gen_random_uuid();
    v_admitted_9   timestamptz := now() - interval '5 days';
    v_discharged_9 timestamptz := now() - interval '2 days';
    v_admitted_10   timestamptz := now() - interval '8 days';
    v_discharged_10 timestamptz := now() - interval '4 days';

    -- Scenario 6 — billed appointment, never admitted (scan / test)
    v_p11 uuid := gen_random_uuid(); v_ap11 uuid := gen_random_uuid(); v_bl11 uuid := gen_random_uuid();
    v_p12 uuid := gen_random_uuid(); v_ap12 uuid := gen_random_uuid(); v_bl12 uuid := gen_random_uuid();
    v_visit_11 timestamptz := now() - interval '3 days';
    v_visit_12 timestamptz := now() - interval '6 days';
BEGIN
    SELECT id INTO v_nurse     FROM staff_members WHERE email = 'nurse.perera@carelanka.lk'     AND is_active;
    SELECT id INTO v_doctor    FROM staff_members WHERE email = 'dr.silva@carelanka.lk'          AND is_active;
    SELECT id INTO v_reception FROM staff_members WHERE email = 'staff.jayasuriya@carelanka.lk'  AND is_active;

    IF v_nurse IS NULL OR v_doctor IS NULL OR v_reception IS NULL THEN
        RAISE EXCEPTION 'Staff accounts from 001_identity.sql are missing — run that script first.';
    END IF;

    SELECT array_agg(id) INTO v_bed_ids
    FROM (
        SELECT id FROM beds
        WHERE is_active AND ward_id IN (SELECT id FROM wards WHERE is_active)
        ORDER BY ward_id, bed_number
        LIMIT 4
    ) sub;

    IF array_length(v_bed_ids, 1) < 4 THEN
        RAISE EXCEPTION 'Need at least four active beds — run 000_reset_demo.sql (Equipment''s ward/bed seed) first.';
    END IF;

    v_bed_s2_1 := v_bed_ids[1];
    v_bed_s2_2 := v_bed_ids[2];
    v_bed_s5_1 := v_bed_ids[3];
    v_bed_s5_2 := v_bed_ids[4];

    -- ==============================================================================
    -- 1. Walk-in intake, admitted, no bed assigned yet
    -- ==============================================================================

    INSERT INTO patients
        (id, patient_code, full_name, nic, gender, date_of_birth, phone, address,
         emergency_contact_name, emergency_contact_phone,
         created_at, updated_at, is_active, deleted_at)
    VALUES
        (v_p1, 'P2H4K7M9', 'Kasun Mendis', '927654321V', 'male', '1992-03-25',
         '+94771234501', '12 Kandy Road, Kadawatha', 'Nadeesha Mendis', '+94771234502',
         now() - interval '3 hours', now() - interval '3 hours', true, NULL),
        (v_p2, 'P3J8N2Q5', 'Nadeesha Wickrama', '889012345V', 'female', '1988-01-14',
         '+94771234503', '34 High Level Road, Nugegoda', 'Chaminda Wickrama', '+94771234504',
         now() - interval '90 minutes', now() - interval '90 minutes', true, NULL);

    INSERT INTO admissions
        (id, patient_id, source, category, urgency, status, is_infectious,
         category_set_by_staff_member_id, category_set_at,
         admitted_at, discharged_at, missing_fields, details_completed_at,
         created_at, updated_at)
    VALUES
        (v_ad1, v_p1, 'walk_in', 'inpatient', 'routine', 'awaiting_bed', false,
         v_nurse, now() - interval '3 hours',
         NULL, NULL, '{}'::text[], now() - interval '3 hours',
         now() - interval '3 hours', now() - interval '3 hours'),
        (v_ad2, v_p2, 'walk_in', 'inpatient', 'urgent', 'awaiting_bed', false,
         v_nurse, now() - interval '90 minutes',
         NULL, NULL, '{}'::text[], now() - interval '90 minutes',
         now() - interval '90 minutes', now() - interval '90 minutes');

    -- ==============================================================================
    -- 2. Walk-in intake, admitted, bed assigned
    -- ==============================================================================

    INSERT INTO patients
        (id, patient_code, full_name, nic, gender, date_of_birth, phone, address,
         emergency_contact_name, emergency_contact_phone,
         created_at, updated_at, is_active, deleted_at)
    VALUES
        (v_p3, 'P4K9R3T6', 'Ishara Gunawardena', '199732401256', 'female', '1997-11-02',
         '+94771234505', '78 Negombo Road, Wattala', 'Priyantha Gunawardena', '+94771234506',
         now() - interval '1 day', v_admitted_3, true, NULL),
        (v_p4, 'P5M2W7X4', 'Ruwan Dissanayake', '199245123456', 'male', '1992-06-19',
         '+94771234507', '9 Station Road, Maharagama', 'Sanduni Dissanayake', '+94771234508',
         now() - interval '2 days', v_admitted_4, true, NULL);

    INSERT INTO admissions
        (id, patient_id, source, category, urgency, status, is_infectious,
         category_set_by_staff_member_id, category_set_at,
         admitted_at, discharged_at, missing_fields, details_completed_at,
         created_at, updated_at)
    VALUES
        (v_ad3, v_p3, 'walk_in', 'inpatient', 'routine', 'admitted', false,
         v_nurse, now() - interval '1 day',
         v_admitted_3, NULL, '{}'::text[], now() - interval '1 day',
         now() - interval '1 day', v_admitted_3),
        (v_ad4, v_p4, 'walk_in', 'inpatient', 'urgent', 'admitted', false,
         v_nurse, now() - interval '2 days',
         v_admitted_4, NULL, '{}'::text[], now() - interval '2 days',
         now() - interval '2 days', v_admitted_4);

    INSERT INTO bed_assignments
        (id, admission_id, bed_id, status, assigned_by, occupied_at, is_downgrade,
         created_at, updated_at)
    VALUES
        (v_ba3, v_ad3, v_bed_s2_1, 'occupied', 'user', v_admitted_3, false, v_admitted_3, v_admitted_3),
        (v_ba4, v_ad4, v_bed_s2_2, 'occupied', 'user', v_admitted_4, false, v_admitted_4, v_admitted_4);

    -- ==============================================================================
    -- 3. Appointment booked, not confirmed yet
    -- ==============================================================================

    INSERT INTO patients
        (id, patient_code, full_name, nic, gender, date_of_birth, phone, address,
         emergency_contact_name, emergency_contact_phone,
         created_at, updated_at, is_active, deleted_at)
    VALUES
        (v_p5, 'P6N4Y8Z2', 'Dinesh Abeywardena', '199088712345', 'male', '1990-09-18',
         '+94771234509', '5 Havelock Road, Colombo 05', 'Sanjeewani Abeywardena', '+94771234510',
         now() - interval '1 hour', now() - interval '1 hour', true, NULL),
        (v_p6, 'P7Q3H5K9', 'Manisha Fonseka', '199612345678', 'female', '1996-04-07',
         '+94771234511', '21 Baseline Road, Colombo 09', 'Rukmal Fonseka', '+94771234512',
         now() - interval '2 hours', now() - interval '2 hours', true, NULL);

    INSERT INTO appointments
        (id, patient_id, scheduled_at, status, reason, booked_by_staff_member_id,
         created_at, updated_at)
    VALUES
        (v_ap5, v_p5, now() + interval '5 days', 'scheduled',
         'General consultation — persistent cough', v_reception,
         now() - interval '1 hour', now() - interval '1 hour'),
        (v_ap6, v_p6, now() + interval '8 days', 'scheduled',
         'Follow-up consultation — blood pressure review', v_reception,
         now() - interval '2 hours', now() - interval '2 hours');

    -- ==============================================================================
    -- 4. Appointment confirmed, patient hasn't arrived yet
    -- ==============================================================================

    INSERT INTO patients
        (id, patient_code, full_name, nic, gender, date_of_birth, phone, address,
         emergency_contact_name, emergency_contact_phone,
         created_at, updated_at, is_active, deleted_at)
    VALUES
        (v_p7, 'P8R7M2N4', 'Tharaka Senanayake', '198845123987', 'male', '1988-12-30',
         '+94771234513', '67 Union Place, Colombo 02', 'Kumudini Senanayake', '+94771234514',
         now() - interval '2 days', now() - interval '1 day', true, NULL),
        (v_p8, 'P9T4X8W3', 'Chathurika Peris', '199356781234', 'female', '1993-02-23',
         '+94771234515', '15 W A D Ramanayake Mawatha, Colombo 02', 'Ajith Peris', '+94771234516',
         now() - interval '3 days', now() - interval '2 days', true, NULL);

    INSERT INTO appointments
        (id, patient_id, scheduled_at, status, reason, booked_by_staff_member_id,
         confirmed_at, confirmed_by_staff_member_id, created_at, updated_at)
    VALUES
        (v_ap7, v_p7, now() + interval '2 days', 'confirmed',
         'Post-operative review', v_reception,
         now() - interval '1 day', v_reception, now() - interval '2 days', now() - interval '1 day'),
        (v_ap8, v_p8, now() + interval '4 days', 'confirmed',
         'Antenatal check-up', v_reception,
         now() - interval '2 days', v_reception, now() - interval '3 days', now() - interval '2 days');

    -- ==============================================================================
    -- 5. Discharged — full stay, billed and settled
    -- ==============================================================================

    INSERT INTO patients
        (id, patient_code, full_name, nic, gender, date_of_birth, phone, address,
         emergency_contact_name, emergency_contact_phone,
         created_at, updated_at, is_active, deleted_at)
    VALUES
        (v_p9, 'PA2C7F5H', 'Chamari Ranasinghe', '857654321V', 'female', '1985-07-12',
         '+94771234517', '45 Galle Road, Colombo 03', 'Sunil Ranasinghe', '+94771234518',
         v_admitted_9 - interval '1 day', v_discharged_9, true, NULL),
        (v_p10, 'PB4D9G2K', 'Suresh Karunaratne', '197623456789', 'male', '1976-05-09',
         '+94771234519', '3 Lake Road, Kandy', 'Malini Karunaratne', '+94771234520',
         v_admitted_10 - interval '1 day', v_discharged_10, true, NULL);

    INSERT INTO admissions
        (id, patient_id, source, category, urgency, status, is_infectious,
         category_set_by_staff_member_id, category_set_at,
         admitted_at, discharged_at, missing_fields, details_completed_at,
         created_at, updated_at)
    VALUES
        (v_ad9, v_p9, 'walk_in', 'inpatient', 'routine', 'discharged', false,
         v_doctor, v_admitted_9 - interval '1 day',
         v_admitted_9, v_discharged_9, '{}'::text[], v_admitted_9 - interval '1 day',
         v_admitted_9 - interval '1 day', v_discharged_9),
        (v_ad10, v_p10, 'walk_in', 'inpatient', 'urgent', 'discharged', false,
         v_doctor, v_admitted_10 - interval '1 day',
         v_admitted_10, v_discharged_10, '{}'::text[], v_admitted_10 - interval '1 day',
         v_admitted_10 - interval '1 day', v_discharged_10);

    INSERT INTO bed_assignments
        (id, admission_id, bed_id, status, assigned_by, occupied_at, released_at,
         release_reason, is_downgrade, created_at, updated_at)
    VALUES
        (v_ba9, v_ad9, v_bed_s5_1, 'released', 'user', v_admitted_9, v_discharged_9,
         'discharged', false, v_admitted_9, v_discharged_9),
        (v_ba10, v_ad10, v_bed_s5_2, 'released', 'user', v_admitted_10, v_discharged_10,
         'discharged', false, v_admitted_10, v_discharged_10);

    INSERT INTO discharges
        (id, admission_id, flagged_by, flagged_at, confirmed_by_staff_member_id, confirmed_at,
         summary_note, created_at, updated_at)
    VALUES
        (v_dc9, v_ad9, 'user', v_discharged_9 - interval '3 hours', v_nurse, v_discharged_9,
         'Patient recovered well. Discharged home with a follow-up appointment in two weeks.',
         v_discharged_9 - interval '3 hours', v_discharged_9),
        (v_dc10, v_ad10, 'user', v_discharged_10 - interval '3 hours', v_nurse, v_discharged_10,
         'Patient stable and mobile. Discharged home with wound-care instructions.',
         v_discharged_10 - interval '3 hours', v_discharged_10);

    INSERT INTO discharge_checklist_items
        (id, discharge_id, item_type, is_mandatory, ticked_at, ticked_by_staff_member_id,
         created_at, updated_at)
    VALUES
        (gen_random_uuid(), v_dc9, 'clinical_clearance', true,
         v_discharged_9 - interval '2 hours', v_doctor,
         v_discharged_9 - interval '3 hours', v_discharged_9 - interval '2 hours'),
        (gen_random_uuid(), v_dc9, 'billing_settled', true,
         v_discharged_9 - interval '1 hour', v_reception,
         v_discharged_9 - interval '3 hours', v_discharged_9 - interval '1 hour'),
        (gen_random_uuid(), v_dc10, 'clinical_clearance', true,
         v_discharged_10 - interval '2 hours', v_doctor,
         v_discharged_10 - interval '3 hours', v_discharged_10 - interval '2 hours'),
        (gen_random_uuid(), v_dc10, 'billing_settled', true,
         v_discharged_10 - interval '1 hour', v_reception,
         v_discharged_10 - interval '3 hours', v_discharged_10 - interval '1 hour');

    INSERT INTO bills
        (id, admission_id, bill_number, raised_by_staff_member_id,
         settled_at, settled_by_staff_member_id, settlement_note, created_at, updated_at)
    VALUES
        (v_bl9, v_ad9, 'B2H4K7M9', v_reception,
         v_discharged_9 - interval '1 hour', v_reception, 'Paid in full by cash.',
         v_discharged_9 - interval '4 hours', v_discharged_9 - interval '1 hour'),
        (v_bl10, v_ad10, 'B3J8N2Q5', v_reception,
         v_discharged_10 - interval '1 hour', v_reception, 'Paid in full by card.',
         v_discharged_10 - interval '4 hours', v_discharged_10 - interval '1 hour');

    INSERT INTO bill_line_items
        (id, bill_id, source, description, quantity, unit_price, bed_assignment_id,
         created_at, updated_at)
    VALUES
        (gen_random_uuid(), v_bl9, 'admission_fee', 'Inpatient admission fee', 1, 5000.00,
         NULL, v_discharged_9 - interval '4 hours', v_discharged_9 - interval '4 hours'),
        (gen_random_uuid(), v_bl9, 'bed_stay', 'General Ward bed charge (3 nights)', 3, 3500.00,
         v_ba9, v_discharged_9 - interval '4 hours', v_discharged_9 - interval '4 hours'),
        (gen_random_uuid(), v_bl10, 'admission_fee', 'Inpatient admission fee', 1, 6000.00,
         NULL, v_discharged_10 - interval '4 hours', v_discharged_10 - interval '4 hours'),
        (gen_random_uuid(), v_bl10, 'bed_stay', 'General Ward bed charge (5 nights)', 5, 3200.00,
         v_ba10, v_discharged_10 - interval '4 hours', v_discharged_10 - interval '4 hours');

    -- ==============================================================================
    -- 6. Billed appointment, never admitted (a scan or a test, not an overnight stay)
    -- ==============================================================================

    INSERT INTO patients
        (id, patient_code, full_name, nic, gender, date_of_birth, phone, address,
         emergency_contact_name, emergency_contact_phone,
         created_at, updated_at, is_active, deleted_at)
    VALUES
        (v_p11, 'PC5E8H3M', 'Anjali Perumal', '199578912345', 'female', '1995-08-21',
         '+94771234521', '88 Duplication Road, Colombo 04', 'Vijay Perumal', '+94771234522',
         v_visit_11 - interval '1 day', v_visit_11, true, NULL),
        (v_p12, 'PD6F2J9N', 'Roshan Fernando', '198934567123', 'male', '1989-03-16',
         '+94771234523', '27 Park Road, Colombo 05', 'Nilanthi Fernando', '+94771234524',
         v_visit_12 - interval '1 day', v_visit_12, true, NULL);

    INSERT INTO appointments
        (id, patient_id, scheduled_at, status, reason, booked_by_staff_member_id,
         confirmed_at, confirmed_by_staff_member_id, created_at, updated_at)
    VALUES
        (v_ap11, v_p11, v_visit_11, 'completed',
         'Full blood count — routine screening', v_reception,
         v_visit_11 - interval '1 day', v_reception,
         v_visit_11 - interval '1 day', v_visit_11),
        (v_ap12, v_p12, v_visit_12, 'completed',
         'Chest X-ray — persistent cough follow-up', v_reception,
         v_visit_12 - interval '1 day', v_reception,
         v_visit_12 - interval '1 day', v_visit_12);

    INSERT INTO bills
        (id, appointment_id, bill_number, raised_by_staff_member_id,
         settled_at, settled_by_staff_member_id, settlement_note, created_at, updated_at)
    VALUES
        (v_bl11, v_ap11, 'B4K9R3T6', v_reception,
         v_visit_11, v_reception, 'Paid in full by cash.', v_visit_11, v_visit_11),
        (v_bl12, v_ap12, 'B5M2W7X4', v_reception,
         v_visit_12, v_reception, 'Paid in full by card.', v_visit_12, v_visit_12);

    INSERT INTO bill_line_items
        (id, bill_id, source, description, quantity, unit_price, bed_assignment_id,
         created_at, updated_at)
    VALUES
        (gen_random_uuid(), v_bl11, 'consultation_fee', 'Outpatient consultation fee', 1, 2000.00,
         NULL, v_visit_11, v_visit_11),
        (gen_random_uuid(), v_bl11, 'manual', 'Complete blood count (CBC) test', 1, 1500.00,
         NULL, v_visit_11, v_visit_11),
        (gen_random_uuid(), v_bl12, 'consultation_fee', 'Outpatient consultation fee', 1, 2000.00,
         NULL, v_visit_12, v_visit_12),
        (gen_random_uuid(), v_bl12, 'manual', 'Chest X-ray', 1, 3000.00,
         NULL, v_visit_12, v_visit_12);
END $$;

COMMIT;

-- What you should have afterwards: 12 patients, 6 admissions (2 awaiting_bed, 2 admitted,
-- 2 discharged), 6 appointments (2 scheduled, 2 confirmed, 2 completed), 4 bills all settled.
SELECT
    (SELECT count(*) FROM patients)                                   AS patients,
    (SELECT count(*) FROM admissions)                                 AS admissions,
    (SELECT count(*) FROM appointments)                               AS appointments,
    (SELECT count(*) FROM bed_assignments WHERE status = 'occupied')  AS beds_occupied,
    (SELECT count(*) FROM bills WHERE settled_at IS NOT NULL)         AS bills_settled;
