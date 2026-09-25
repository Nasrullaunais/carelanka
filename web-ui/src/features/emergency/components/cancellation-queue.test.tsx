import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { pagedResult, renderWithProviders } from '../../../test/api-mocks';
import { CancellationQueue } from './cancellation-queue';

const mocks = vi.hoisted(() => ({ approve: vi.fn(), reject: vi.fn() }));
const requests = [
  { emergency_call_id: 'reviewed-call', status: 'rejected', reason: 'No longer needed', requested_at: '2026-09-23T08:01:00Z', review_notes: 'Response must continue.', caller_name: 'A. Perera', address_label: 'Colombo Fort', call_priority: 'medium', call_status: 'dispatched', call_created_at: '2026-09-23T07:50:00Z', active_ambulance_registration: 'WP-CA-1234' },
  { emergency_call_id: 'pending-call', status: 'pending', reason: 'Taking private transport', requested_at: '2026-09-23T08:00:00Z', caller_name: 'N. Silva', address_label: 'Colombo Fort', call_priority: 'critical', call_status: 'dispatched', call_created_at: '2026-09-23T07:50:00Z', active_ambulance_registration: 'WP-CA-1234' },
];

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('../../../services/api/generated/@tanstack/react-query.gen', () => ({
  listEmergencyCancellationRequestsOptions: () => ({ queryKey: ['cancellations'], queryFn: () => Promise.resolve(pagedResult(requests)) }),
  listEmergencyCancellationRequestsQueryKey: () => ['cancellations'],
  approveEmergencyCancellationRequestMutation: () => ({ mutationFn: mocks.approve }),
  rejectEmergencyCancellationRequestMutation: () => ({ mutationFn: mocks.reject }),
}));

describe('CancellationQueue', () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it('shows pending requests first and approves only after recall confirmation', async () => {
    mocks.approve.mockResolvedValue({});
    renderWithProviders(<CancellationQueue />);

    const cards = await screen.findAllByText(/N\. Silva|A\. Perera/);
    expect(cards[0]).toHaveTextContent('N. Silva');
    expect(screen.getAllByText('WP-CA-1234')).toHaveLength(2);
    expect(screen.getByText('Taking private transport')).toBeInTheDocument();
    expect(screen.getByText(/approving cancels the emergency call/i)).toBeInTheDocument();
    expect(screen.getAllByText('Why the caller wants to cancel')).toHaveLength(2);
    expect(screen.getByText('Response continued')).toBeInTheDocument();
    expect(screen.getByText('Keep emergency response active')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Approve cancellation' }));
    expect(mocks.approve).not.toHaveBeenCalled();
    const dialog = await screen.findByRole('alertdialog');
    expect(within(dialog).getByText(/recalls the ambulance/i)).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Approve and recall' }));

    await waitFor(() => expect(mocks.approve.mock.calls[0]?.[0]).toEqual({ path: { id: 'pending-call' }, body: {} }));
  });

  it('requires review notes when rejecting a request', async () => {
    mocks.reject.mockResolvedValue({});
    renderWithProviders(<CancellationQueue />);

    await userEvent.click(await screen.findByRole('button', { name: 'Reject request' }));
    const dialog = await screen.findByRole('dialog');
    const rejectButton = within(dialog).getByRole('button', { name: 'Reject request' });
    expect(rejectButton).toBeDisabled();
    await userEvent.type(within(dialog).getByLabelText('Review notes'), 'Crew must assess the patient before standing down.');
    expect(rejectButton).toBeEnabled();
    await userEvent.click(rejectButton);

    await waitFor(() => expect(mocks.reject.mock.calls[0]?.[0]).toEqual({
      path: { id: 'pending-call' },
      body: { notes: 'Crew must assess the patient before standing down.' },
    }));
  });
});
