-- CareLanka seed 003 — beds. Run after 002_patient_wards.sql. Idempotent; deletes nothing.
--
-- `beds` is **Equipment Management's** table. This file only puts dev data in it, so that the
-- ward board from 002 has furniture in it and the capacity and bed-assignment screens have
-- something real to show. Nothing about the bed *model* is decided here — that is
-- `Bed.cs` and `BedConfiguration.cs`, and they are Equipment's. If Equipment writes its own
-- seed later, this file is the one that goes.
--
-- How the numbers were picked: a small district hospital. Critical care is small, general
-- wards are large.
--
-- One field needs a word, because it is not obvious from its name. `has_isolation` means this
-- bed can be shut away from the rest of the ward. A real ward is mostly one open room with
-- beds in rows, plus a few single rooms off it with a door; an infectious patient goes in one
-- of those. It is a property of the BED, not the ward, which is what hard rule H4 turns on.

INSERT INTO beds
    (id, ward_id, bed_number, has_isolation, condition,
     asset_tag, created_at, updated_at, is_active, deleted_at)
SELECT
    gen_random_uuid(),
    w.id,
    plan.prefix || '-' || lpad(n::text, 2, '0'),

    -- The lowest-numbered beds are the single rooms. An isolation unit is all single rooms;
    -- a 20-bed general ward has one.
    n <= plan.single_rooms,

    'usable',
    'CL-' || plan.prefix || '-' || lpad(n::text, 2, '0'),
    now(), now(), true, NULL
FROM (VALUES
    -- ward name                         prefix,  beds, single rooms
    ('Intensive Care Unit',              'ICU',      8,  8),
    ('High Dependency Unit',             'HDU',      6,  2),
    ('Emergency Treatment Unit',         'ETU',     10,  2),
    ('Isolation Unit',                   'ISO',      6,  6),
    ('General Medical Ward (Male)',      'GMM',     20,  1),
    ('General Medical Ward (Female)',    'GMF',     20,  1),
    ('General Surgical Ward (Male)',     'GSM',     16,  1),
    ('General Surgical Ward (Female)',   'GSF',     16,  1),
    ('Maternity Ward',                   'MAT',     12,  1),
    ('Pediatric Ward',                   'PED',     14,  2)
) AS plan(ward_name, prefix, bed_count, single_rooms)

-- An inner join on purpose. A ward that does not exist gets no beds and no error: run this
-- before 002 and you get nothing, not half a hospital.
JOIN wards w ON w.is_active AND w.name = plan.ward_name
CROSS JOIN LATERAL generate_series(1, plan.bed_count) AS n

ON CONFLICT (ward_id, bed_number) WHERE is_active DO NOTHING;

-- Two beds away for repair, so "Out for repair" on the capacity screen is not permanently
-- zero and the H1 refusal has something to refuse. Equipment owns what `condition` means;
-- this only picks two beds to set it on.

UPDATE beds SET condition = 'out_of_service', updated_at = now()
WHERE is_active
  AND condition = 'usable'
  AND asset_tag IN ('CL-GMM-20', 'CL-ETU-10');
