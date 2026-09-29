import { useState } from 'react';
import { cleanup, fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../../test/api-mocks';
import { EmergencyDesk } from './emergency-desk';

const mocks = vi.hoisted(() => ({
  dispatch: vi.fn(), createCall: vi.fn(), confirm: vi.fn(), toastError: vi.fn(), selected: vi.fn(),
  callStatus: 'received',
  latest: undefined as undefined | { id: string; status: string; proposed_ambulance_registration?: string; estimated_minutes_to_scene?: number },
}));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: mocks.toastError } }));
vi.mock('./location-picker', () => ({
  LocationPicker: ({ onChange, readOnly }: { onChange: (location: { latitude: number; longitude: number }) => void; readOnly?: boolean }) => readOnly
    ? <div>Scene map</div>
    : <button type="button" onClick={() => onChange({ latitude: 7.1234, longitude: 80.5678 })}>Choose map location</button>,
}));
vi.mock('../../../services/api/generated/@tanstack/react-query.gen', () => ({
  listEmergencyCallsOptions: () => ({
    queryKey: ['calls'],
    queryFn: () => Promise.resolve({
      items: [
        { id: 'call-1', priority: 'high', status: mocks.callStatus, caller_name: 'A. Perera', address_label: 'Colombo Fort', waiting_minutes: 4, latest_proposal: mocks.latest },
        { id: 'call-2', priority: 'medium', status: 'received', caller_name: 'B. Silva', waiting_minutes: 20, latest_proposal: { id: 'p-2', status: 'pending' } },
        { id: 'call-3', priority: 'critical', status: 'received', caller_name: 'C. Fernando', waiting_minutes: 1, latest_proposal: { id: 'p-3', status: 'failed' } },
      ],
      page: 1, page_size: 100, total_items: 3, total_pages: 1,
    }),
  }),
  getEmergencyCallOptions: ({ path }: { path: { id: string } }) => ({
    queryKey: ['call', path.id],
    queryFn: () => Promise.resolve({
      id: path.id, priority: 'high', status: mocks.callStatus, caller_name: 'A. Perera', caller_phone: '0712345678',
      details: 'Severe chest pain', address_label: 'Colombo Fort', latitude: 6.927, longitude: 79.861, dispatches: [], latest_proposal: mocks.latest,
    }),
  }),
  listAmbulancesOptions: () => ({
    queryKey: ['ambulances'],
    queryFn: () => Promise.resolve({
      items: [{ id: 'amb-1', registration_number: 'WP-CA-1234', status: 'available', current_crew_count: 2, required_crew_count: 2, is_eligible: true, is_divertible: true }],
      page: 1, page_size: 100, total_items: 1, total_pages: 1,
    }),
  }),
  getDispatchProposalOptions: ({ path }: { path: { id: string } }) => ({
    queryKey: ['proposal', path.id],
    queryFn: () => Promise.resolve({ id: path.id, status: 'pending_confirmation', proposed_ambulance_registration: 'WP-CA-1234', estimated_minutes_to_scene: 6 }),
  }),
  dispatchEmergencyCallMutation: () => ({ mutationFn: mocks.dispatch }),
  updateEmergencyCallMutation: () => ({ mutationFn: vi.fn() }),
  createEmergencyCallMutation: () => ({ mutationFn: mocks.createCall }),
  createDispatchProposalMutation: () => ({ mutationFn: vi.fn() }),
  confirmDispatchProposalMutation: () => ({ mutationFn: mocks.confirm }),
  approveDispatchProposalMutation: () => ({ mutationFn: vi.fn() }),
  rejectDispatchProposalMutation: () => ({ mutationFn: vi.fn() }),
}));

function Desk({ initial }: { initial?: string }) {
  const [selected, setSelected] = useState(initial);
  return (
    <EmergencyDesk
      selectedCallId={selected}
      onSelectCall={(id) => { mocks.selected(id); setSelected(id); }}
      onCloseCall={() => setSelected(undefined)}
    />
  );
}

describe('EmergencyDesk', () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    mocks.callStatus = 'received';
    mocks.latest = undefined;
  });

  it.each(['dispatched', 'en_route', 'at_scene', 'transporting', 'completed', 'cancelled'])('hides dispatch and recommendation actions for %s calls', async (status) => {
    mocks.callStatus = status;
    renderWithProviders(<Desk />);
    await userEvent.click(screen.getByRole('button', { name: 'All calls' }));
    fireEvent.click((await screen.findByText('A. Perera')).closest('tr')!);
    await screen.findByText('Severe chest pain');
    expect(screen.queryByRole('button', { name: 'Send' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Re-check' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Dispatch$/ })).not.toBeInTheDocument();
  });

  it('shows each waiting call\'s recommendation on the list', async () => {
    mocks.latest = { id: 'p-1', status: 'pending_confirmation', proposed_ambulance_registration: 'WP-CA-1234', estimated_minutes_to_scene: 6 };
    renderWithProviders(<Desk />);

    expect(await screen.findByText('WP-CA-1234 · 6 min')).toBeInTheDocument();
    expect(screen.getByText('Checking…')).toBeInTheDocument();
    expect(screen.getByText('Pick by hand')).toBeInTheDocument();
  });

  it('dispatches by hand when there is no recommendation', async () => {
    mocks.dispatch.mockResolvedValue({ id: 'dispatch-1' });
    renderWithProviders(<Desk />);

    fireEvent.click((await screen.findByText('A. Perera')).closest('tr')!);
    await userEvent.click(await screen.findByRole('button', { name: 'Dispatch' }));

    await waitFor(() => expect(mocks.dispatch.mock.calls[0]?.[0]).toEqual({ path: { id: 'call-1' }, body: { ambulance_id: 'amb-1' } }));
  });

  it('folds the hand-pick list away while a recommendation is ready', async () => {
    mocks.latest = { id: 'p-1', status: 'pending_confirmation' };
    renderWithProviders(<Desk initial="call-1" />);

    expect(await screen.findByRole('button', { name: 'Send' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Other ambulances/ })).toHaveAttribute('aria-expanded', 'false');
  });

  it('opens the most urgent waiting call after sending', async () => {
    mocks.latest = { id: 'p-1', status: 'pending_confirmation' };
    mocks.confirm.mockResolvedValue({});
    renderWithProviders(<Desk initial="call-1" />);
    await screen.findByText('C. Fernando');

    await userEvent.click(await screen.findByRole('button', { name: 'Send' }));

    await waitFor(() => expect(mocks.selected).toHaveBeenLastCalledWith('call-3'));
  });

  it('leaves a dispatch conflict to the shared error message', async () => {
    mocks.dispatch.mockRejectedValue({ status: 409, detail: 'Ambulance is no longer available.' });
    renderWithProviders(<Desk />);

    fireEvent.click((await screen.findByText('A. Perera')).closest('tr')!);
    await userEvent.click(await screen.findByRole('button', { name: 'Dispatch' }));

    await waitFor(() => expect(mocks.dispatch).toHaveBeenCalledTimes(1));
    expect(mocks.toastError).not.toHaveBeenCalled();
  });

  it('logs a front-desk call with a stable idempotency key and capture time, then opens it', async () => {
    mocks.createCall.mockResolvedValue({ id: 'call-2' });
    renderWithProviders(<Desk />);

    await userEvent.click(await screen.findByRole('button', { name: 'Log emergency call' }));
    await userEvent.type(screen.getByLabelText('Caller name'), 'N. Silva');
    await userEvent.type(screen.getByLabelText('Emergency details'), 'Breathing difficulty');
    await userEvent.type(screen.getByLabelText('Latitude'), '6.9');
    await userEvent.type(screen.getByLabelText('Longitude'), '79.8');
    await userEvent.type(screen.getByLabelText('Accuracy (m)'), '12');
    await userEvent.click(screen.getByRole('button', { name: 'Log call' }));

    await waitFor(() => expect(mocks.createCall).toHaveBeenCalled());
    const request = mocks.createCall.mock.calls[0]![0].body;
    expect(request).toMatchObject({ caller_name: 'N. Silva', details: 'Breathing difficulty', latitude: 6.9, longitude: 79.8, location_accuracy_metres: 12 });
    expect(request.idempotency_key).toMatch(/[0-9a-f-]{36}/i);
    expect(Number.isNaN(Date.parse(request.location_captured_at))).toBe(false);
    await waitFor(() => expect(mocks.selected).toHaveBeenCalledWith('call-2'));
  });

  it('submits coordinates selected with the location picker', async () => {
    mocks.createCall.mockResolvedValue({ id: 'call-3' });
    renderWithProviders(<Desk />);

    await userEvent.click(await screen.findByRole('button', { name: 'Log emergency call' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Choose map location' }));
    await userEvent.type(screen.getByLabelText('Accuracy (m)'), '25');
    await userEvent.type(screen.getByLabelText('Emergency details'), 'Fall injury');
    await userEvent.click(screen.getByRole('button', { name: 'Log call' }));

    await waitFor(() => expect(mocks.createCall).toHaveBeenCalled());
    expect(mocks.createCall.mock.calls[0]![0].body).toMatchObject({ latitude: 7.1234, longitude: 80.5678, location_accuracy_metres: 25 });
  });
});
