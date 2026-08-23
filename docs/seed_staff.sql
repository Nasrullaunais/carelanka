-- CareLanka — development seed: one staff member per role.
--
-- Run AFTER `dotnet ef database update`. Migrations are DDL only; seed data lives here.
--
--   psql -h localhost -p 5433 -U carelanka -d carelanka -f docs/seed_staff.sql
--
-- Idempotent: safe to run as many times as you like. Existing rows are left alone.
--
-- Every account below has the same password:  CareLanka#2026
-- That hash is a development convenience. Never load this file into production.

INSERT INTO staff_members
    (id, email, password_hash, first_name, last_name, phone_number, department, role,
     created_at, updated_at, is_active, deleted_at)
VALUES
    ('11111111-1111-4111-8111-111111111111', 'admin@carelanka.lk',
     '$2a$11$8.rkqYwPl0a3rbmWkBD2JeDn3r/pmtcEpnnLZgJ0Jt2cbg.Fe6sdG',
     'Amara', 'Silva', '+94112000001', 'Administration', 'HospitalAdministrator',
     now(), now(), true, null),

    ('22222222-2222-4222-8222-222222222222', 'duty@carelanka.lk',
     '$2a$11$8.rkqYwPl0a3rbmWkBD2JeDn3r/pmtcEpnnLZgJ0Jt2cbg.Fe6sdG',
     'Ruwan', 'Fernando', '+94112000002', 'Emergency', 'DutyManager',
     now(), now(), true, null),

    ('33333333-3333-4333-8333-333333333333', 'nurse@carelanka.lk',
     '$2a$11$8.rkqYwPl0a3rbmWkBD2JeDn3r/pmtcEpnnLZgJ0Jt2cbg.Fe6sdG',
     'Nimali', 'Perera', '+94112000003', 'Ward 5B', 'WardNurse',
     now(), now(), true, null),

    ('44444444-4444-4444-8444-444444444444', 'doctor@carelanka.lk',
     '$2a$11$8.rkqYwPl0a3rbmWkBD2JeDn3r/pmtcEpnnLZgJ0Jt2cbg.Fe6sdG',
     'Suresh', 'Jayawardena', '+94112000004', 'General Medicine', 'Doctor',
     now(), now(), true, null),

    ('55555555-5555-4555-8555-555555555555', 'crew@carelanka.lk',
     '$2a$11$8.rkqYwPl0a3rbmWkBD2JeDn3r/pmtcEpnnLZgJ0Jt2cbg.Fe6sdG',
     'Kasun', 'Bandara', '+94112000005', 'Ambulance', 'AmbulanceCrew',
     now(), now(), true, null),

    ('66666666-6666-4666-8666-666666666666', 'equipment@carelanka.lk',
     '$2a$11$8.rkqYwPl0a3rbmWkBD2JeDn3r/pmtcEpnnLZgJ0Jt2cbg.Fe6sdG',
     'Dilani', 'Wickrama', '+94112000006', 'Biomedical', 'EquipmentManager',
     now(), now(), true, null),

    ('77777777-7777-4777-8777-777777777777', 'staff@carelanka.lk',
     '$2a$11$8.rkqYwPl0a3rbmWkBD2JeDn3r/pmtcEpnnLZgJ0Jt2cbg.Fe6sdG',
     'Tharindu', 'Rajapaksa', '+94112000007', 'Support', 'GeneralStaff',
     now(), now(), true, null)

ON CONFLICT (id) DO NOTHING;
