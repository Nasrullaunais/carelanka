-- The five pharmacy categories from equipment-management-plan.md section 1.2.
--
-- Idempotent and parameterless, like 001_identity.sql: safe to run twice, and it carries no
-- environment-specific ids, so nothing here can leak a developer's local uuid into another
-- environment. Migrations stay DDL only; this is the data.
--
-- requires_prescription records the rule, it does not enforce a clinical decision. Deciding
-- what a patient should be given belongs to clinical staff, never to this component.
--
--   psql -U postgres -d carelanka -f docs/seed/002_pharmacy_categories.sql

INSERT INTO pharmacy_categories (id, name, requires_prescription, created_at, updated_at, is_active)
VALUES
    (gen_random_uuid(), 'Prescription Medicines',      true,  now(), now(), true),
    (gen_random_uuid(), 'OTC Medicines',               false, now(), now(), true),
    (gen_random_uuid(), 'Behind-the-Counter Medicines', true, now(), now(), true),
    (gen_random_uuid(), 'Chronic Medicines',           true,  now(), now(), true),
    (gen_random_uuid(), 'Medical Supplies',            false, now(), now(), true)
ON CONFLICT DO NOTHING;

-- Behind-the-counter is marked as requiring a prescription on purpose. The category is
-- pharmacist-controlled rather than prescription-only, but this schema has one boolean, and
-- the safe reading of a restricted medicine is the stricter one. If the group wants the
-- three-way distinction, that is a column change, not a data change.
