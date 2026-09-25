import { cleanup, fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../../test/api-mocks';
import { EmergencyDesk } from './emergency-desk';

const mocks = vi.hoisted(() => ({ dispatch: vi.fn(), createCall: vi.fn(), toastError: vi.fn(), callStatus: 'received' }));

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
      items: [{ id: 'call-1', priority: 'high', status: mocks.callStatus, caller_name: 'A. Perera', address_label: 'Colombo Fort', waiting_minutes: 4 }],
      page: 1, page_size: 100, total_items: 1, total_pages: 1,
    }),
  }),
  getEmergencyCallOptions: () => ({
    queryKey: ['call', 'call-1'],
    queryFn: () => Promise.resolve({
      id: 'call-1', priority: 'high', status: mocks.callStatus, caller_name: 'A. Perera', caller_phone: '0712345678',
      details: 'Severe chest pain', address_label: 'Colombo Fort', latitude: 6.927, longitude: 79.861, dispatches: [],
    }),
  }),
  listAmbulancesOptions: () => ({
    queryKey: ['ambulances'],
    queryFn: () => Promise.resolve({
      items: [{ id: 'amb-1', registration_number: 'WP-CA-1234', status: 'available', current_crew_count: 2, required_crew_count: 2, is_eligible: true, is_divertible: true }],
      page: 1, page_size: 100, total_items: 1, total_pages: 1,
    }),
  }),
  dispatchEmergencyCallMutation: () => ({ mutationFn: mocks.dispatch }),
  updateEmergencyCallMutation: () => ({ mutationFn: vi.fn() }),
  createEmergencyCallMutation: () => ({ mutationFn: mocks.createCall }),
  createDispatchProposalMutation: () => ({ mutationFn: vi.fn() }),
}));

describe('EmergencyDesk', () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    mocks.callStatus = 'received';
  });

  it.each(['dispatched', 'en_route', 'at_scene', 'transporting', 'completed', 'cancelled'])('hides dispatch and agent actions for %s calls', async (status) => {
    mocks.callStatus = status;
    renderWithProviders(<EmergencyDesk />);
    await userEvent.click(screen.getByRole('button', { name: 'All calls' }));
    fireEvent.click((await screen.findByText('A. Perera')).closest('tr')!);
    await screen.findByText('Severe chest pain');
    expect(screen.queryByRole('button', { name: 'Ask the agent' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Dispatch$/ })).not.toBeInTheDocument();
  });

  it('selects a call and dispatches an eligible ambulance', async () => {
    mocks.dispatch.mockResolvedValue({ id: 'dispatch-1' });
    renderWithProviders(<EmergencyDesk />);

    const caller = await screen.findByText('A. Perera');
    fireEvent.click(caller.closest('tr')!);
    await userEvent.click(await screen.findByRole('button', { name: 'Dispatch' }));

    await waitFor(() => expect(mocks.dispatch.mock.calls[0]?.[0]).toEqual({ path: { id: 'call-1' }, body: { ambulance_id: 'amb-1' } }));
  });

  it('explains a dispatch conflict and refreshes the operational state', async () => {
    mocks.dispatch.mockRejectedValue({ status: 409, detail: 'Ambulance is no longer available.' });
    renderWithProviders(<EmergencyDesk />);

    fireEvent.click((await screen.findByText('A. Perera')).closest('tr')!);
    await userEvent.click(await screen.findByRole('button', { name: 'Dispatch' }));

    await waitFor(() => expect(mocks.dispatch).toHaveBeenCalledTimes(1));
    expect(mocks.toastError).toHaveBeenCalledWith(expect.stringMatching(/another dispatcher changed/i));
  });

  it('logs a front-desk call with a stable idempotency key and capture time', async () => {
    mocks.createCall.mockResolvedValue({ id: 'call-2' });
    renderWithProviders(<EmergencyDesk />);

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
  });

  it('submits coordinates selected with the location picker', async () => {
    mocks.createCall.mockResolvedValue({ id: 'call-3' });
    renderWithProviders(<EmergencyDesk />);

    await userEvent.click(await screen.findByRole('button', { name: 'Log emergency call' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Choose map location' }));
    await userEvent.type(screen.getByLabelText('Accuracy (m)'), '25');
    await userEvent.type(screen.getByLabelText('Emergency details'), 'Fall injury');
    await userEvent.click(screen.getByRole('button', { name: 'Log call' }));

    await waitFor(() => expect(mocks.createCall).toHaveBeenCalled());
    expect(mocks.createCall.mock.calls[0]![0].body).toMatchObject({ latitude: 7.1234, longitude: 80.5678, location_accuracy_metres: 25 });
  });
});
