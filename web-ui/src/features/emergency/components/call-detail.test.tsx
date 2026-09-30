import { cleanup, screen } from '@testing-library/react';
import type { UseQueryResult } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../../test/api-mocks';
import type { AmbulanceSummaryPagedResult, CallDispatch, EmergencyCallDetail, ListAmbulancesError, ProblemDetails } from '../../../services/api/generated';
import { CallDetail } from './call-detail';

vi.mock('./location-picker', () => ({ LocationPicker: () => <div>Scene map</div> }));

const ambulances = { data: undefined, isPending: false } as UseQueryResult<AmbulanceSummaryPagedResult, ListAmbulancesError>;

function renderCall(call: Partial<EmergencyCallDetail>, ...dispatches: Partial<CallDispatch>[]) {
  const detail = { id: 'call-1', status: 'completed', priority: 'high', dispatches: dispatches.map((item, index) => ({ id: `d-${index}`, ambulance_registration: 'WP-CA-1234', crew_count: 2, ...item })), ...call } as EmergencyCallDetail;
  const query = { data: detail, isPending: false, isError: false } as UseQueryResult<EmergencyCallDetail, ProblemDetails>;
  renderWithProviders(<CallDetail callId="call-1" query={query} ambulances={ambulances} />);
}

describe('CallDetail dispatch history', () => {
  afterEach(cleanup);

  it('shows what the crew reported at handover', () => {
    renderCall({}, { status: 'handed_over', patient_condition: 'Conscious, blood pressure low', handover_notes: 'Left leg splinted' });

    expect(screen.getByText('Handed over', { selector: 'span' })).toBeInTheDocument();
    expect(screen.getByText('Conscious, blood pressure low')).toBeInTheDocument();
    expect(screen.getByText('Left leg splinted')).toBeInTheDocument();
  });

  it('says so when the crew wrote nothing at handover', () => {
    renderCall({}, { status: 'handed_over' });

    expect(screen.getByText('Not recorded')).toBeInTheDocument();
    expect(screen.getByText('No notes were written at handover.')).toBeInTheDocument();
  });

  it('shows why a run finished at the scene, with the crew notes', () => {
    renderCall(
      { transported: false, scene_outcome: 'patient_refused', scene_outcome_notes: 'Wants to see her own doctor' },
      { status: 'ended_at_scene' },
    );

    expect(screen.getByText('Finished at scene')).toBeInTheDocument();
    expect(screen.getByText('Patient refused to go')).toBeInTheDocument();
    expect(screen.getByText('Wants to see her own doctor')).toBeInTheDocument();
  });

  it.each([
    ['declined', { declined_reason: 'Flat tyre' }, 'Declined by the crew:', 'Flat tyre'],
    ['cancelled', { cancellation_reason: 'Caller called back' }, 'Cancelled:', 'Caller called back'],
    ['reassigned', { reassignment_reason: 'Closer ambulance became free' }, 'Given to another ambulance:', 'Closer ambulance became free'],
  ] as const)('shows the reason a run was %s', (status, reason, label, text) => {
    renderCall({}, { status, ...reason });

    expect(screen.getByText(label)).toBeInTheDocument();
    expect(screen.getByText(text)).toBeInTheDocument();
  });

  it('shows every run of a call in order, each with its own ending', () => {
    renderCall(
      {},
      { status: 'reassigned', ambulance_registration: 'WP-CA-1111', reassignment_reason: 'Closer ambulance became free' },
      { status: 'handed_over', ambulance_registration: 'WP-CA-2222', handover_notes: 'Left leg splinted' },
    );

    expect(screen.getByText('WP-CA-1111')).toBeInTheDocument();
    expect(screen.getByText('Closer ambulance became free')).toBeInTheDocument();
    expect(screen.getByText('WP-CA-2222')).toBeInTheDocument();
    expect(screen.getByText('Left leg splinted')).toBeInTheDocument();
  });

  it('shows nothing extra for a run that is still going', () => {
    renderCall({ status: 'en_route' }, { status: 'en_route_to_scene' });

    expect(screen.queryByText(/Condition on arrival/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Declined by the crew/)).not.toBeInTheDocument();
  });
});
