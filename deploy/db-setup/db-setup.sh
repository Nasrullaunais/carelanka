#!/bin/sh
# Runs as the database owner after every migration: gives the API its limited login,
# then loads the demo data the first time only.
set -eu

: "${CARELANKA_DB_APP_PASSWORD:?Set CARELANKA_DB_APP_PASSWORD}"

psql -v ON_ERROR_STOP=1 -v app_password="$CARELANKA_DB_APP_PASSWORD" -f /db-setup/app-role.sql

if [ "$(psql -Atc "SELECT to_regclass('public.local_seed_complete') IS NOT NULL")" = t ]; then
  echo 'Demo data already seeded'
  exit 0
fi

for file in \
  001_identity.sql \
  002_patient_wards.sql \
  002_pharmacy_categories.sql \
  003_equipment_beds.sql \
  004_patient_demo_data.sql \
  005_patient_medical_profiles.sql \
  006_emergency_demo_data.sql \
  007_emergency_demo_fleet.sql
do
  psql -v ON_ERROR_STOP=1 -f "/seed/$file"
done

psql -v ON_ERROR_STOP=1 -c 'CREATE TABLE local_seed_complete (seeded_at timestamptz NOT NULL DEFAULT now())'
