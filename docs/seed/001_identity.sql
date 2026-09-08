-- CareLanka seed 001 — identity. Run after the migrations; seed data is not in a
-- migration, because migrations are DDL only.

-- Staff — password for all seven: CareLanka#2026. Identical passwords hash differently
-- because each has its own salt; that is correct, not a mistake in the file.

INSERT INTO staff_members
    (id, email, password_hash, first_name, last_name, phone_number, department, role,
     created_at, updated_at, is_active, deleted_at)
VALUES
    (gen_random_uuid(), 'nurse.perera@carelanka.lk',
     'AQAAAAIAAYagAAAAEJ6WgQ9Wtb91a2eHJQ3lEoVC2aIIJI42X9npfUFTxMl7NAid9bDx2fQ7Lotj6cepsA==',
     'Amara', 'Perera', '+94771000001', 'Ward A', 'ward_nurse',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'dr.silva@carelanka.lk',
     'AQAAAAIAAYagAAAAEDieu0Vgs9uMCso+JVVWEABFisF+O6x9RyeowaF4dqS8p54Uq6AdsJTvYw02r5dpxg==',
     'Nimal', 'Silva', '+94771000002', 'General Medicine', 'doctor',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'crew.fernando@carelanka.lk',
     'AQAAAAIAAYagAAAAEIrtiEHUuSr7fqusZOgi2yPIVGVwVUbsHY7SlUpm2YDocakGkIfanTXxqPpwD6F7Sw==',
     'Kasun', 'Fernando', '+94771000003', 'Ambulance', 'ambulance_crew',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'staff.jayasuriya@carelanka.lk',
     'AQAAAAIAAYagAAAAEC4BX8IMVN3U85XIxj6Khfq9XWWSI+dk20yHVCyIImPMt2e+/wT9Brm+SAFQpywgNg==',
     'Ishara', 'Jayasuriya', '+94771000004', 'Front Desk', 'general_staff',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'duty.rajapaksa@carelanka.lk',
     'AQAAAAIAAYagAAAAECaFvpfXXELqulf+Nk9WchGh9dvpPZnlVIizQsmJAqj0MF86xrDYvKGCCEcNvHq1wA==',
     'Sanduni', 'Rajapaksa', '+94771000005', 'Operations', 'duty_manager',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'admin.wickrama@carelanka.lk',
     'AQAAAAIAAYagAAAAELwlaiElRNiTyQezAlMWhdMu02fKckzIK6/MsAj+yR1xzTcJhSsuFkFCJJK2Qx1ydg==',
     'Tharindu', 'Wickramasinghe', '+94771000006', 'Administration', 'hospital_administrator',
     now(), now(), true, NULL),

    (gen_random_uuid(), 'equip.bandara@carelanka.lk',
     'AQAAAAIAAYagAAAAEDTQLjSs/kFoc4vG6lPvTd2kRnyURNI4YfqoVfUv+DN789fAy3U6NSoHCVkeT4hrxQ==',
     'Ruwan', 'Bandara', '+94771000007', 'Biomedical', 'equipment_manager',
     now(), now(), true, NULL)

ON CONFLICT (email) WHERE is_active DO NOTHING;

-- One deactivated staff member, so the "same 401 as a wrong password" rule is testable.
-- NOT EXISTS rather than ON CONFLICT: is_active is false, so the partial index misses this row.

INSERT INTO staff_members
    (id, email, password_hash, first_name, last_name, phone_number, department, role,
     created_at, updated_at, is_active, deleted_at)
SELECT
    gen_random_uuid(), 'former.gunasekara@carelanka.lk',
    'AQAAAAIAAYagAAAAEJ6WgQ9Wtb91a2eHJQ3lEoVC2aIIJI42X9npfUFTxMl7NAid9bDx2fQ7Lotj6cepsA==',
    'Dilini', 'Gunasekara', '+94771000008', 'Ward B', 'ward_nurse',
    now(), now(), false, now()
WHERE NOT EXISTS (
    SELECT 1 FROM staff_members WHERE email = 'former.gunasekara@carelanka.lk'
);

-- Patient account — password: Patient#2026. A login with no patients row linked, so
-- GET /auth/me returns patient_id = null for it. That is the ordinary state, not an error.

INSERT INTO patient_accounts
    (id, phone_number, password_hash, full_name, last_login_at,
     created_at, updated_at, is_active, deleted_at)
VALUES
    (gen_random_uuid(), '+94771234567',
     'AQAAAAIAAYagAAAAEKv/cB2nN3X3BWOfSYbIUEmA6rjHv/NYE0wyfZJSUoMnQX0GoPnDmcPY+RxdSU2NWQ==',
     'Chathura Wijesinghe', NULL,
     now(), now(), true, NULL)

ON CONFLICT (phone_number) WHERE is_active DO NOTHING;
