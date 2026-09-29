import { cleanup, screen } from '@testing-library/react';
import type { UseQueryResult } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../../test/api-mocks';
import type { AmbulanceSummaryPagedResult, EmergencyCallDetail, ListAmbulancesError, ProblemDetails } from '../../../services/api/generated';
import { CallDetail } from './call-detail';

vi.mock('./location-picker', () => ({ LocationPicker: () => <div>Scene map</div> }));

const ambulances = { data: undefined, isPending: false } as UseQueryResult<AmbulanceSummaryPagedResult, ListAmbulancesError>;

function renderCall(call: EmergencyCallDetail) {
  const query = { data: call, isPending: false, isError: false } as UseQueryResult<EmergencyCallDetail, ProblemDetails>;
  renderWithProviders(<CallDetail callId="call-1" query={query} ambulances={ambulances} />);
}

describe('CallDetail', () => {
  afterEach(cleanup);

  it('says how a run that finished at the scene ended, with the crew notes', () => {
    renderCall({
      id: 'call-1', status: 'completed', priority: 'high', transported: false,
      scene_outcome: 'patient_refused', scene_outcome_notes: 'Wants to see her own doctor',
      dispatches: [{ id: 'd-1', status: 'ended_at_scene', ambulance_registration: 'WP-CA-1234', crew_count: 2 }],
    });

    expect(screen.getByText('Finished at scene')).toBeInTheDocument();
    expect(screen.getByText('How it ended')).toBeInTheDocument();
    expect(screen.getByText(/Patient refused to go/)).toBeInTheDocument();
    expect(screen.getByText('Wants to see her own doctor')).toBeInTheDocument();
  });

  it('shows no ending for a call that went to hospital', () => {
    renderCall({
      id: 'call-1', status: 'completed', priority: 'high', transported: true,
      dispatches: [{ id: 'd-1', status: 'handed_over', ambulance_registration: 'WP-CA-1234', crew_count: 2 }],
    });

    expect(screen.getByText('Handed over')).toBeInTheDocument();
    expect(screen.queryByText('How it ended')).not.toBeInTheDocument();
  });
});
