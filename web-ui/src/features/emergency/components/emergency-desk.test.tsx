import { useState } from 'react';
import { cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../../test/api-mocks';
import { EmergencyDesk } from './emergency-desk';

const mocks = vi.hoisted(() => ({
  dispatch: vi.fn(), createCall: vi.fn(), confirm: vi.fn(), toastError: vi.fn(), selected: vi.fn(),
  closeCall: vi.fn(), cancelRun: vi.fn(), reassignRun: vi.fn(), update: vi.fn(),
  callStatus: 'received',
  dispatches: [] as Array<Record<string, unknown>>,
  closedCall: {} as Record<string, unknown>,
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
      details: 'Severe chest pain', address_label: 'Colombo Fort', latitude: 6.927, longitude: 79.861, location_accuracy_metres: 30,
      dispatches: mocks.dispatches, latest_proposal: mocks.latest, ...mocks.closedCall,
    }),
  }),
  listAmbulancesOptions: () => ({
    queryKey: ['ambulances'],
    queryFn: () => Promise.resolve({
      items: [
        { id: 'amb-1', registration_number: 'WP-CA-1234', status: 'available', current_crew_count: 2, required_crew_count: 2, is_eligible: true, is_divertible: true },
        { id: 'amb-9', registration_number: 'WP-CA-9999', status: 'available', current_crew_count: 2, required_crew_count: 2, is_eligible: true, is_divertible: true, distance_km: 2.4 },
      ],
      page: 1, page_size: 100, total_items: 1, total_pages: 1,
    }),
  }),
  getDispatchProposalOptions: ({ path }: { path: { id: string } }) => ({
    queryKey: ['proposal', path.id],
    queryFn: () => Promise.resolve({ id: path.id, status: 'pending_confirmation', proposed_ambulance_registration: 'WP-CA-1234', estimated_minutes_to_scene: 6 }),
  }),
  dispatchEmergencyCallMutation: () => ({ mutationFn: mocks.dispatch }),
  updateEmergencyCallMutation: () => ({ mutationFn: mocks.update }),
  cancelEmergencyCallMutation: () => ({ mutationFn: mocks.closeCall }),
  cancelDispatchMutation: () => ({ mutationFn: mocks.cancelRun }),
  reassignDispatchMutation: () => ({ mutationFn: mocks.reassignRun }),
  searchSceneAddressesOptions: ({ query }: { query: { query: string } }) => ({
    queryKey: ['address-search', query.query],
    queryFn: () => Promise.resolve([{ label: 'Ward Place, Colombo 07', latitude: 6.9125, longitude: 79.8667, approximate_accuracy_metres: 80 }]),
  }),
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
    mocks.dispatches = [];
    mocks.closedCall = {};
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

    expect(await screen.findByText('WP-CA-1234')).toBeInTheDocument();
    expect(screen.getByText('Checking…')).toBeInTheDocument();
    expect(screen.getByText('Pick by hand')).toBeInTheDocument();
  });

  it('dispatches by hand when there is no recommendation', async () => {
    mocks.dispatch.mockResolvedValue({ id: 'dispatch-1' });
    renderWithProviders(<Desk />);

    fireEvent.click((await screen.findByText('A. Perera')).closest('tr')!);
    await userEvent.click((await screen.findAllByRole('button', { name: 'Dispatch' }))[0]!);

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
    await userEvent.click((await screen.findAllByRole('button', { name: 'Dispatch' }))[0]!);

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
    expect(screen.getByLabelText('Accuracy (m)')).toHaveValue(50);
    await userEvent.type(screen.getByLabelText('Emergency details'), 'Fall injury');
    await userEvent.click(screen.getByRole('button', { name: 'Log call' }));

    await waitFor(() => expect(mocks.createCall).toHaveBeenCalled());
    expect(mocks.createCall.mock.calls[0]![0].body).toMatchObject({ latitude: 7.1234, longitude: 80.5678, location_accuracy_metres: 50, priority: 'high' });
  });

  it('logs a call at the chosen priority and at a searched address', async () => {
    mocks.createCall.mockResolvedValue({ id: 'call-4' });
    renderWithProviders(<Desk />);

    await userEvent.click(await screen.findByRole('button', { name: 'Log emergency call' }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.type(within(dialog).getByLabelText('Emergency details'), 'Unconscious');
    await userEvent.click(within(dialog).getByRole('button', { name: /Priority/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Critical' }));
    await userEvent.type(within(dialog).getByLabelText('Search for the scene address'), 'ward place');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Search' }));
    await userEvent.click(await within(dialog).findByRole('button', { name: /Ward Place, Colombo 07/ }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Log call' }));

    await waitFor(() => expect(mocks.createCall).toHaveBeenCalled());
    expect(mocks.createCall.mock.calls[0]![0].body).toMatchObject({ priority: 'critical', latitude: 6.9125, longitude: 79.8667, location_accuracy_metres: 80 });
  });

  it('closes a waiting call with an outcome', async () => {
    mocks.closeCall.mockResolvedValue({});
    renderWithProviders(<Desk initial="call-1" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Close call' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/No ambulance will be sent/)).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: /Why is it closing/ }));
    await userEvent.click(await screen.findByRole('option', { name: 'Duplicate call' }));
    await userEvent.type(within(dialog).getByLabelText('Notes (optional)'), 'Same as the 10:42 call');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Close call' }));

    await waitFor(() => expect(mocks.closeCall.mock.calls[0]?.[0]).toEqual({
      path: { id: 'call-1' },
      body: { outcome: 'duplicate_call', notes: 'Same as the 10:42 call' },
    }));
  });

  it('calls off an ambulance that has not reached the patient', async () => {
    mocks.callStatus = 'dispatched';
    mocks.dispatches = [{ id: 'run-1', ambulance_id: 'amb-1', ambulance_registration: 'WP-CA-1234', status: 'assigned', acknowledgement_overdue: true, crew_count: 2 }];
    mocks.cancelRun.mockResolvedValue({});
    renderWithProviders(<Desk initial="call-1" />);

    expect(await screen.findByText(/has not accepted yet/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Call off this ambulance' }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.type(within(dialog).getByLabelText('Reason'), 'Crew not answering');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Call off ambulance' }));

    await waitFor(() => expect(mocks.cancelRun.mock.calls[0]?.[0]).toEqual({ path: { id: 'run-1' }, body: { reason: 'Crew not answering' } }));
  });

  it('sends a different ambulance, never offering the one already on the run', async () => {
    mocks.callStatus = 'dispatched';
    mocks.dispatches = [{ id: 'run-1', ambulance_id: 'amb-1', ambulance_registration: 'WP-CA-1234', status: 'en_route_to_scene', crew_count: 2 }];
    mocks.reassignRun.mockResolvedValue({ ambulance_registration: 'WP-CA-9999' });
    renderWithProviders(<Desk initial="call-1" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Send a different ambulance' }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /Send instead/ }));
    expect(screen.queryByRole('option', { name: /WP-CA-1234/ })).not.toBeInTheDocument();
    await userEvent.click(await screen.findByRole('option', { name: /WP-CA-9999 · 2.4 km/ }));
    await userEvent.type(within(dialog).getByLabelText('Reason'), 'Closer ambulance free');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Send this ambulance' }));

    await waitFor(() => expect(mocks.reassignRun.mock.calls[0]?.[0]).toEqual({
      path: { id: 'run-1' },
      body: { replacement_ambulance_id: 'amb-9', reason: 'Closer ambulance free' },
    }));
  });

  it('offers no way to stop a run once the crew is with the patient', async () => {
    mocks.callStatus = 'en_route';
    mocks.dispatches = [{ id: 'run-1', ambulance_id: 'amb-1', ambulance_registration: 'WP-CA-1234', status: 'at_scene', crew_count: 2 }];
    renderWithProviders(<Desk initial="call-1" />);

    expect(await screen.findByText(/can only be ended by the crew/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Call off this ambulance' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Send a different ambulance' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Close call' })).not.toBeInTheDocument();
  });

  it('shows what the crew wrote and how a closed call ended', async () => {
    mocks.callStatus = 'completed';
    mocks.closedCall = { outcome: 'refused_transport', outcome_notes: 'Signed the refusal form', closed_at: '2026-10-03T09:00:00Z', transported: false, patient_name: 'Nimal Perera' };
    mocks.dispatches = [
      { id: 'run-0', ambulance_registration: 'WP-CB-1111', status: 'declined', declined_reason: 'Flat tyre', crew_count: 2 },
      { id: 'run-1', ambulance_registration: 'WP-CA-1234', status: 'closed_at_scene', crew_count: 2 },
    ];
    renderWithProviders(<Desk initial="call-1" />);

    expect(await screen.findByText('Patient refused transport')).toBeInTheDocument();
    expect(screen.getByText('Signed the refusal form')).toBeInTheDocument();
    expect(screen.getByText('Nimal Perera')).toBeInTheDocument();
    expect(screen.getByText('Flat tyre')).toBeInTheDocument();
    expect(screen.getByText('Ended at the scene')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Edit call' })).not.toBeInTheDocument();
  });

  it('corrects the scene and caller phone, sending only what changed', async () => {
    mocks.update.mockResolvedValue({});
    renderWithProviders(<Desk initial="call-1" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Edit call' }));
    const dialog = await screen.findByRole('dialog');
    const phone = within(dialog).getByLabelText('Caller phone');
    await userEvent.clear(phone);
    await userEvent.type(phone, '0771234567');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Choose map location' }));
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(mocks.update.mock.calls[0]?.[0]).toEqual({
      path: { id: 'call-1' },
      body: { caller_phone: '0771234567', latitude: 7.1234, longitude: 80.5678, location_accuracy_metres: 50 },
    }));
  });
});
