-- ============================================================================
-- CareLanka seed 001 — identity
--
-- Seven staff accounts, one per role, plus one patient account. Assignment §15
-- requires test accounts in the submission, and the demo has to log in as four
-- different roles inside ten minutes.
--
-- Run AFTER the migrations:
--     dotnet ef database update --project api
--     psql -U postgres -d carelanka -f docs/seed/001_identity.sql
--
-- Two properties this script keeps, and why:
--
--   * Idempotent. ON CONFLICT DO NOTHING against the partial unique index, so
--     running it twice does not duplicate anybody and does not fail.
--   * No hard-coded ids. gen_random_uuid() means the same script is safe on
--     four laptops and on the deployed database, and no environment-specific id
--     ever ends up in a migration.
--
-- Seed data is NOT in a migration. Migrations are DDL only.
-- ============================================================================

-- ----------------------------------------------------------------------------
-- Staff — password for all seven: CareLanka#2026
--
-- The hashes below are real PBKDF2 output from ASP.NET Core's PasswordHasher,
-- one random salt each, so identical passwords produce different hashes. That is
-- correct, not a mistake in the file.
--
-- These are demo accounts on a demo database. Change every password before this
-- is ever pointed at something real.
-- ----------------------------------------------------------------------------

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

-- ----------------------------------------------------------------------------
-- One deactivated staff member.
--
-- Not padding. It is the only way to test the thing the spec is most explicit
-- about: signing in as a deactivated account must return the SAME 401 as a wrong
-- password, so login cannot be used to work out who used to work here.
--
-- It is also the row that proves ux_staff_members_email is scoped WHERE is_active
-- — this email is free to be reused by a new account, which a plain UNIQUE would
-- have blocked forever.
--
-- Password: CareLanka#2026 (the same hash as the ward nurse — it is a dead
-- account, and nothing should let anyone in with it regardless).
--
-- ON CONFLICT cannot help here: is_active is false, so the partial index does not
-- cover this row and there is no conflict for Postgres to detect. NOT EXISTS is
-- what keeps the second run from inserting a second copy.
-- ----------------------------------------------------------------------------

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

-- ----------------------------------------------------------------------------
-- Patient account — password: Patient#2026
--
-- A LOGIN, not a medical record. There is deliberately no patients row here and
-- nothing linked to one: GET /auth/me returns patient_id = null for this account
-- and that is the ordinary state, not an error. A Flutter screen that assumes
-- patient_id is non-null crashes on exactly this account.
-- ----------------------------------------------------------------------------

INSERT INTO patient_accounts
    (id, phone_number, password_hash, full_name, last_login_at,
     created_at, updated_at, is_active, deleted_at)
VALUES
    (gen_random_uuid(), '+94771234567',
     'AQAAAAIAAYagAAAAEKv/cB2nN3X3BWOfSYbIUEmA6rjHv/NYE0wyfZJSUoMnQX0GoPnDmcPY+RxdSU2NWQ==',
     'Chathura Wijesinghe', NULL,
     now(), now(), true, NULL)

ON CONFLICT (phone_number) WHERE is_active DO NOTHING;
