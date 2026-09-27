-- CareLanka Emergency demo fleet. Run after 006_emergency_demo_data.sql. Safe to run repeatedly.
-- Four more ambulances parked around Colombo (Rajagiriya, Dehiwala, Nugegoda, Kelaniya), so the
-- dispatch agent has real choices. WP-CAL-102 has three crew, the rest two.
-- Crew password for all nine: CareLanka#2026. DemoFleetLocationWorker keeps positions fresh;
-- its parking spots in appsettings.json must match the coordinates below.

BEGIN;

INSERT INTO staff_members
    (id, email, password_hash, first_name, last_name, phone_number, department, role,
     created_at, updated_at, is_active, deleted_at)
VALUES
    (gen_random_uuid(), 'crew.wickramasinghe@carelanka.lk',
     'AQAAAAIAAYagAAAAEIrtiEHUuSr7fqusZOgi2yPIVGVwVUbsHY7SlUpm2YDocakGkIfanTXxqPpwD6F7Sw==',
     'Dilshan', 'Wickramasinghe', '+94771000101', 'Ambulance', 'ambulance_crew',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'crew.herath@carelanka.lk',
     'AQAAAAIAAYagAAAAEIrtiEHUuSr7fqusZOgi2yPIVGVwVUbsHY7SlUpm2YDocakGkIfanTXxqPpwD6F7Sw==',
     'Nadeesha', 'Herath', '+94771000102', 'Ambulance', 'ambulance_crew',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'crew.bandara@carelanka.lk',
     'AQAAAAIAAYagAAAAEIrtiEHUuSr7fqusZOgi2yPIVGVwVUbsHY7SlUpm2YDocakGkIfanTXxqPpwD6F7Sw==',
     'Ruwan', 'Bandara', '+94771000103', 'Ambulance', 'ambulance_crew',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'crew.jayawardena@carelanka.lk',
     'AQAAAAIAAYagAAAAEIrtiEHUuSr7fqusZOgi2yPIVGVwVUbsHY7SlUpm2YDocakGkIfanTXxqPpwD6F7Sw==',
     'Chamara', 'Jayawardena', '+94771000104', 'Ambulance', 'ambulance_crew',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'crew.dissanayake@carelanka.lk',
     'AQAAAAIAAYagAAAAEIrtiEHUuSr7fqusZOgi2yPIVGVwVUbsHY7SlUpm2YDocakGkIfanTXxqPpwD6F7Sw==',
     'Hiruni', 'Dissanayake', '+94771000105', 'Ambulance', 'ambulance_crew',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'crew.gunawardena@carelanka.lk',
     'AQAAAAIAAYagAAAAEIrtiEHUuSr7fqusZOgi2yPIVGVwVUbsHY7SlUpm2YDocakGkIfanTXxqPpwD6F7Sw==',
     'Lahiru', 'Gunawardena', '+94771000106', 'Ambulance', 'ambulance_crew',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'crew.ekanayake@carelanka.lk',
     'AQAAAAIAAYagAAAAEIrtiEHUuSr7fqusZOgi2yPIVGVwVUbsHY7SlUpm2YDocakGkIfanTXxqPpwD6F7Sw==',
     'Sachini', 'Ekanayake', '+94771000107', 'Ambulance', 'ambulance_crew',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'crew.kumara@carelanka.lk',
     'AQAAAAIAAYagAAAAEIrtiEHUuSr7fqusZOgi2yPIVGVwVUbsHY7SlUpm2YDocakGkIfanTXxqPpwD6F7Sw==',
     'Pradeep', 'Kumara', '+94771000108', 'Ambulance', 'ambulance_crew',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'crew.ratnayake@carelanka.lk',
     'AQAAAAIAAYagAAAAEIrtiEHUuSr7fqusZOgi2yPIVGVwVUbsHY7SlUpm2YDocakGkIfanTXxqPpwD6F7Sw==',
     'Madushani', 'Ratnayake', '+94771000109', 'Ambulance', 'ambulance_crew',
     now(), now(), true, NULL)

ON CONFLICT (email) WHERE is_active DO NOTHING;

DO $$
DECLARE
    v_manager uuid;
BEGIN
    SELECT id INTO v_manager FROM staff_members
    WHERE email = 'duty.rajapaksa@carelanka.lk' AND is_active;

    IF v_manager IS NULL THEN
        RAISE EXCEPTION 'The demo fleet requires identity seed 001';
    END IF;

    INSERT INTO ambulances
        (id, registration_number, current_latitude, current_longitude,
         location_updated_at, status, created_at, updated_at, is_active, deleted_at)
    VALUES
        ('10000000-0000-4000-8000-000000001102', 'WP-CAL-102', 6.909400, 79.894000, now(), 'available', now(), now(), true, NULL),
        ('10000000-0000-4000-8000-000000001103', 'WP-CAL-103', 6.851100, 79.865300, now(), 'available', now(), now(), true, NULL),
        ('10000000-0000-4000-8000-000000001104', 'WP-CAL-104', 6.864900, 79.899700, now(), 'available', now(), now(), true, NULL),
        ('10000000-0000-4000-8000-000000001105', 'WP-CAL-105', 6.955300, 79.922000, now(), 'available', now(), now(), true, NULL)
    ON CONFLICT DO NOTHING;

    INSERT INTO ambulance_status_history
        (id, ambulance_id, status, started_at, created_at)
    VALUES
        ('10000000-0000-4000-8000-000000001302', '10000000-0000-4000-8000-000000001102', 'available', now() - interval '1 day', now()),
        ('10000000-0000-4000-8000-000000001303', '10000000-0000-4000-8000-000000001103', 'available', now() - interval '1 day', now()),
        ('10000000-0000-4000-8000-000000001304', '10000000-0000-4000-8000-000000001104', 'available', now() - interval '1 day', now()),
        ('10000000-0000-4000-8000-000000001305', '10000000-0000-4000-8000-000000001105', 'available', now() - interval '1 day', now())
    ON CONFLICT DO NOTHING;

    INSERT INTO ambulance_crew_assignments
        (id, ambulance_id, staff_member_id, assigned_at, assigned_by_staff_id, created_at)
    SELECT crew.id::uuid, crew.ambulance_id::uuid, staff.id, now() - interval '1 day', v_manager, now()
    FROM (VALUES
            ('10000000-0000-4000-8000-000000001101', '10000000-0000-4000-8000-000000001102', 'crew.wickramasinghe@carelanka.lk'),
            ('10000000-0000-4000-8000-000000001102', '10000000-0000-4000-8000-000000001102', 'crew.herath@carelanka.lk'),
            ('10000000-0000-4000-8000-000000001103', '10000000-0000-4000-8000-000000001102', 'crew.bandara@carelanka.lk'),
            ('10000000-0000-4000-8000-000000001104', '10000000-0000-4000-8000-000000001103', 'crew.jayawardena@carelanka.lk'),
            ('10000000-0000-4000-8000-000000001105', '10000000-0000-4000-8000-000000001103', 'crew.dissanayake@carelanka.lk'),
            ('10000000-0000-4000-8000-000000001106', '10000000-0000-4000-8000-000000001104', 'crew.gunawardena@carelanka.lk'),
            ('10000000-0000-4000-8000-000000001107', '10000000-0000-4000-8000-000000001104', 'crew.ekanayake@carelanka.lk'),
            ('10000000-0000-4000-8000-000000001108', '10000000-0000-4000-8000-000000001105', 'crew.kumara@carelanka.lk'),
            ('10000000-0000-4000-8000-000000001109', '10000000-0000-4000-8000-000000001105', 'crew.ratnayake@carelanka.lk')
    ) AS crew (id, ambulance_id, email)
    JOIN staff_members staff ON staff.email = crew.email AND staff.is_active
    ON CONFLICT DO NOTHING;
END $$;

COMMIT;
