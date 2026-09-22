import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { EmergencyPage } from './EmergencyPage';

const dispatch = vi.fn();
const assignCrew = vi.fn();

vi.mock('../services/auth/useSession', () => ({
  useSession: () => ({ principal: { role: 'duty_manager' } }),
}));

vi.mock('../services/api/generated/@tanstack/react-query.gen', () => ({
  listEmergencyCallsOptions: () => ({
    queryKey: ['calls'],
    queryFn: () => Promise.resolve({
      items: [{
        id: 'call-1', priority: 'high', status: 'received', caller_name: 'A. Perera',
        details: 'Severe chest pain', latitude: 6.927, longitude: 79.861, waiting_minutes: 4,
      }], page: 1, page_size: 50, total_items: 1, total_pages: 1,
    }),
  }),
  getEmergencyCallOptions: () => ({
    queryKey: ['call', 'call-1'],
    queryFn: () => Promise.resolve({
      id: 'call-1', priority: 'high', status: 'received', caller_name: 'A. Perera',
      details: 'Severe chest pain', latitude: 6.927, longitude: 79.861, dispatches: [],
    }),
  }),
  listAmbulancesOptions: () => ({
    queryKey: ['ambulances'],
    queryFn: () => Promise.resolve({
      items: [{ id: 'amb-1', registration_number: 'WP-CA-1234', status: 'available',
        current_crew_count: 2, required_crew_count: 2, is_eligible: true, is_divertible: true }],
      page: 1, page_size: 50, total_items: 1, total_pages: 1,
    }),
  }),
  dispatchEmergencyCallMutation: () => ({ mutationFn: dispatch }),
  updateEmergencyCallMutation: () => ({ mutationFn: vi.fn() }),
  getCurrentAmbulanceCrewOptions: () => ({ queryKey: ['crew'], queryFn: () => Promise.resolve([]) }),
  assignCurrentAmbulanceCrewMutation: () => ({ mutationFn: assignCrew }),
}));

function renderPage() {
  return render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <EmergencyPage />
    </QueryClientProvider>,
  );
}

describe('EmergencyPage', () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it('lets a Duty Manager choose an eligible ambulance and dispatch the selected call', async () => {
    dispatch.mockResolvedValue({ id: 'dispatch-1', status: 'assigned', dispatched_at: new Date().toISOString() });
    renderPage();

    expect(await screen.findByText('A. Perera')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /A. Perera/i }));
    fireEvent.click(await screen.findByRole('button', { name: /dispatch WP-CA-1234/i }));

    await waitFor(() => expect(dispatch.mock.calls[0]?.[0]).toEqual({
      path: { id: 'call-1' }, body: { ambulance_id: 'amb-1' },
    }));
  });

  it('explains a stale two-dispatcher conflict and refreshes the board', async () => {
    dispatch.mockRejectedValue({ status: 409 });
    renderPage();

    fireEvent.click(await screen.findByRole('button', { name: /A. Perera/i }));
    fireEvent.click(await screen.findByRole('button', { name: /dispatch WP-CA-1234/i }));

    expect(await screen.findByText(/Another dispatcher changed this call or ambulance first/i)).toBeInTheDocument();
  });

  it('lets a Duty Manager assign current crew before dispatching', async () => {
    assignCrew.mockResolvedValue({});
    renderPage();

    fireEvent.click(await screen.findByRole('button', { name: 'WP-CA-1234' }));
    fireEvent.change(await screen.findByLabelText('Ambulance crew staff ID'), {
      target: { value: 'crew-1' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Assign crew member' }));

    await waitFor(() => expect(assignCrew.mock.calls[0]?.[0]).toEqual({
      path: { id: 'amb-1' }, body: { staff_member_id: 'crew-1' },
    }));
  });
});
