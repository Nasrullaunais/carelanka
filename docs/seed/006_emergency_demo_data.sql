-- CareLanka Emergency demo fixtures. Run after 001_identity.sql and
-- 004_patient_demo_data.sql. Safe to run repeatedly on a demo database.

BEGIN;

DO $$
DECLARE
    v_manager uuid;
    v_crew_one uuid;
    v_crew_two uuid;
    v_patient_account uuid;
    v_patient uuid;
    v_ambulance constant uuid := '10000000-0000-4000-8000-000000000001';
    v_call_one constant uuid := '10000000-0000-4000-8000-000000000011';
    v_call_two constant uuid := '10000000-0000-4000-8000-000000000012';
    v_dispatch_one constant uuid := '10000000-0000-4000-8000-000000000021';
    v_dispatch_two constant uuid := '10000000-0000-4000-8000-000000000022';
    v_workflow_one constant uuid := '10000000-0000-4000-8000-000000000031';
    v_workflow_two constant uuid := '10000000-0000-4000-8000-000000000032';
    v_proposal_one constant uuid := '10000000-0000-4000-8000-000000000041';
    v_proposal_two constant uuid := '10000000-0000-4000-8000-000000000042';
BEGIN
    SELECT id INTO v_manager FROM staff_members
    WHERE email = 'duty.rajapaksa@carelanka.lk' AND is_active;
    SELECT id INTO v_crew_one FROM staff_members
    WHERE email = 'crew.fernando@carelanka.lk' AND is_active;
    SELECT id INTO v_crew_two FROM staff_members
    WHERE email = 'crew.perera@carelanka.lk' AND is_active;
    SELECT id INTO v_patient_account FROM patient_accounts
    WHERE username = 'demo.emergency' AND is_active;
    SELECT id INTO v_patient FROM patients ORDER BY patient_code LIMIT 1;

    IF v_manager IS NULL OR v_crew_one IS NULL OR v_crew_two IS NULL
       OR v_patient_account IS NULL OR v_patient IS NULL THEN
        RAISE EXCEPTION 'Emergency fixtures require identity seed 001 and patient seed 004';
    END IF;

    UPDATE patients SET user_account_id = v_patient_account, updated_at = now()
    WHERE id = v_patient AND user_account_id IS NULL;

    INSERT INTO ambulances
        (id, registration_number, current_latitude, current_longitude,
         location_updated_at, status, created_at, updated_at, is_active, deleted_at)
    VALUES
        (v_ambulance, 'WP-CAL-101', 6.914700, 79.871900,
         now(), 'available', now(), now(), true, NULL)
    ON CONFLICT (id) DO UPDATE SET
        current_latitude = EXCLUDED.current_latitude,
        current_longitude = EXCLUDED.current_longitude,
        location_updated_at = EXCLUDED.location_updated_at,
        status = 'available', is_active = true, deleted_at = NULL;

    INSERT INTO ambulance_status_history
        (id, ambulance_id, status, started_at, created_at)
    VALUES
        ('10000000-0000-4000-8000-000000000091', v_ambulance, 'available', now() - interval '9 days', now()),
        ('10000000-0000-4000-8000-000000000092', v_ambulance, 'dispatched', now() - interval '8 days' + interval '4 minutes', now()),
        ('10000000-0000-4000-8000-000000000093', v_ambulance, 'available', now() - interval '8 days' + interval '42 minutes', now()),
        ('10000000-0000-4000-8000-000000000094', v_ambulance, 'dispatched', now() - interval '4 days' + interval '5 minutes', now()),
        ('10000000-0000-4000-8000-000000000095', v_ambulance, 'available', now() - interval '4 days' + interval '51 minutes', now())
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO ambulance_crew_assignments
        (id, ambulance_id, staff_member_id, assigned_at, assigned_by_staff_id, created_at)
    VALUES
        ('10000000-0000-4000-8000-000000000051', v_ambulance, v_crew_one,
         now() - interval '1 day', v_manager, now()),
        ('10000000-0000-4000-8000-000000000052', v_ambulance, v_crew_two,
         now() - interval '1 day', v_manager, now())
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO emergency_calls
        (id, patient_id, caller_user_id, patient_is_caller, caller_name,
         latitude, longitude, location_accuracy_metres, location_captured_at,
         idempotency_key, address_label, details, priority, status, outcome,
         transported, created_at, updated_at)
    VALUES
        (v_call_one, v_patient, v_patient_account, true, 'Emergency demo patient',
         6.927100, 79.861200, 8, now() - interval '8 days',
         '10000000-0000-4000-8000-000000000061', 'Galle Face, Colombo',
         'Completed high-priority demo response', 'high', 'completed',
         'Handed over at CareLanka Hospital', true,
         now() - interval '8 days', now() - interval '8 days' + interval '42 minutes'),
        (v_call_two, v_patient, v_patient_account, true, 'Emergency demo patient',
         6.902200, 79.860700, 12, now() - interval '4 days',
         '10000000-0000-4000-8000-000000000062', 'Bambalapitiya, Colombo',
         'Completed medium-priority demo response', 'medium', 'completed',
         'Handed over at CareLanka Hospital', true,
         now() - interval '4 days', now() - interval '4 days' + interval '51 minutes')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO agent_workflows
        (id, agent_type, entity_type, entity_id, correlation_id, objective,
         plan, completed_steps, tool_results, status, required_approver_role,
         started_at, completed_at, attempt_count, reviewed_by_staff_member_id,
         reviewed_at, final_outcome, created_at, updated_at)
    VALUES
        (v_workflow_one, 'dispatch_routing', 'EmergencyCall', v_call_one, v_workflow_one,
         'Recommend an ambulance', '[]', '[]', '[]', 'executed', 'duty_manager',
         now() - interval '8 days' + interval '2 minutes',
         now() - interval '8 days' + interval '4 minutes', 1, v_manager,
         now() - interval '8 days' + interval '3 minutes', 'confirmed',
         now() - interval '8 days', now() - interval '8 days' + interval '4 minutes'),
        (v_workflow_two, 'dispatch_routing', 'EmergencyCall', v_call_two, v_workflow_two,
         'Recommend an ambulance', '[]', '[]', '[]', 'rejected', 'duty_manager',
         now() - interval '4 days' + interval '2 minutes',
         now() - interval '4 days' + interval '4 minutes', 1, v_manager,
         now() - interval '4 days' + interval '3 minutes', 'manual_fallback',
         now() - interval '4 days', now() - interval '4 days' + interval '4 minutes')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO dispatch_proposals
        (id, workflow_id, emergency_call_id, call_priority, status, outcome,
         is_diversion, allow_diversion, rationale, proposed_ambulance_id,
         estimated_minutes_to_scene, reviewed_by_staff_member_id, reviewed_at,
         review_notes, created_at, updated_at)
    VALUES
        (v_proposal_one, v_workflow_one, v_call_one, 'high', 'executed',
         'free_ambulance_proposed', false, false, 'Closest eligible ambulance',
         v_ambulance, 9, v_manager, now() - interval '8 days' + interval '3 minutes',
         'Accepted for demo', now() - interval '8 days' + interval '2 minutes',
         now() - interval '8 days' + interval '4 minutes'),
        (v_proposal_two, v_workflow_two, v_call_two, 'medium', 'rejected',
         'free_ambulance_proposed', false, false, 'Eligible ambulance available',
         v_ambulance, 12, v_manager, now() - interval '4 days' + interval '3 minutes',
         'Manual dispatch used', now() - interval '4 days' + interval '2 minutes',
         now() - interval '4 days' + interval '4 minutes')
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO dispatches
        (id, emergency_call_id, ambulance_id, status, dispatch_proposal_id,
         dispatched_at, acknowledged_at, acknowledged_by_staff_id,
         handover_notes, patient_condition, completed_at, created_at, updated_at)
    VALUES
        (v_dispatch_one, v_call_one, v_ambulance, 'handed_over', v_proposal_one,
         now() - interval '8 days' + interval '4 minutes',
         now() - interval '8 days' + interval '6 minutes', v_crew_one,
         'Transferred to emergency intake', 'Stable at handover',
         now() - interval '8 days' + interval '42 minutes',
         now() - interval '8 days' + interval '4 minutes',
         now() - interval '8 days' + interval '42 minutes'),
        (v_dispatch_two, v_call_two, v_ambulance, 'handed_over', NULL,
         now() - interval '4 days' + interval '5 minutes',
         now() - interval '4 days' + interval '8 minutes', v_crew_two,
         'Transferred to emergency intake', 'Stable at handover',
         now() - interval '4 days' + interval '51 minutes',
         now() - interval '4 days' + interval '5 minutes',
         now() - interval '4 days' + interval '51 minutes')
    ON CONFLICT (id) DO NOTHING;

    UPDATE dispatch_proposals SET resulting_dispatch_id = v_dispatch_one
    WHERE id = v_proposal_one;

    INSERT INTO dispatch_crew (id, dispatch_id, staff_member_id, created_at)
    VALUES
        ('10000000-0000-4000-8000-000000000071', v_dispatch_one, v_crew_one, now()),
        ('10000000-0000-4000-8000-000000000072', v_dispatch_one, v_crew_two, now()),
        ('10000000-0000-4000-8000-000000000073', v_dispatch_two, v_crew_one, now()),
        ('10000000-0000-4000-8000-000000000074', v_dispatch_two, v_crew_two, now())
    ON CONFLICT (id) DO NOTHING;

    INSERT INTO route_logs
        (id, dispatch_id, origin_latitude, origin_longitude,
         destination_latitude, destination_longitude, planned_distance_km,
         planned_duration_minutes, departed_at, arrived_at, maps_api_reference,
         created_at, updated_at)
    VALUES
        ('10000000-0000-4000-8000-000000000081', v_dispatch_one,
         6.914700, 79.871900, 6.927100, 79.861200, 4.20, 9,
         now() - interval '8 days' + interval '9 minutes',
         now() - interval '8 days' + interval '20 minutes', 'demo-fixture', now(), now()),
        ('10000000-0000-4000-8000-000000000082', v_dispatch_two,
         6.914700, 79.871900, 6.902200, 79.860700, 5.10, 12,
         now() - interval '4 days' + interval '11 minutes',
         now() - interval '4 days' + interval '25 minutes', 'demo-fixture', now(), now())
    ON CONFLICT (id) DO NOTHING;
END $$;

COMMIT;
