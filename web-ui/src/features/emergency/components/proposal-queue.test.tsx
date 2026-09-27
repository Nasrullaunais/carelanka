import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../../test/api-mocks';
import { ProposalQueue } from './proposal-queue';

const mocks = vi.hoisted(() => ({ confirm: vi.fn(), approve: vi.fn(), reject: vi.fn() }));
const summaries = [
  { id: 'routine', status: 'pending_confirmation', call_priority: 'high', created_at: '2026-09-23T08:00:00Z' },
  { id: 'diversion', status: 'pending_approval', call_priority: 'critical', created_at: '2026-09-23T08:01:00Z' },
  { id: 'failed', status: 'failed', call_priority: 'medium', outcome: 'no_ambulance_available', created_at: '2026-09-23T08:02:00Z' },
];
const details = {
  routine: { ...summaries[0], proposed_ambulance_registration: 'WP-CA-1234', estimated_minutes_to_scene: 8, rationale: 'Closest ready crew.' },
  diversion: {
    ...summaries[1], is_diversion: true, proposed_ambulance_registration: 'WP-CA-5678', estimated_minutes_to_scene: 4, rationale: 'Saves twelve minutes.',
    diversion_impact: {
      source_call_id: 'source-call', source_call_priority: 'low', source_dispatch_status: 'en_route_to_scene',
      source_call_waiting_minutes_so_far: 9, source_call_additional_wait_minutes: 6, replacement_ambulance_registration: null,
      minutes_saved_for_this_call: 12,
    },
  },
  failed: { ...summaries[2], errors: [{ step: 'selection', message: 'No staffed ambulance is currently available.' }] },
};

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('../hooks/use-actionable-proposals', () => ({
  useActionableProposals: () => ({ items: summaries, pendingCount: 2, isPending: false, error: null, refetch: vi.fn() }),
}));
vi.mock('../../../services/api/generated/@tanstack/react-query.gen', () => ({
  getDispatchProposalOptions: ({ path }: { path: { id: keyof typeof details } }) => ({
    queryKey: ['proposal', path.id], queryFn: () => Promise.resolve(details[path.id]),
  }),
  confirmDispatchProposalMutation: () => ({ mutationFn: mocks.confirm }),
  approveDispatchProposalMutation: () => ({ mutationFn: mocks.approve }),
  rejectDispatchProposalMutation: () => ({ mutationFn: mocks.reject }),
}));

describe('ProposalQueue', () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it('confirms a routine recommendation', async () => {
    mocks.confirm.mockResolvedValue({});
    renderWithProviders(<ProposalQueue />);
    await userEvent.click(await screen.findByRole('button', { name: 'Send' }));
    await waitFor(() => expect(mocks.confirm.mock.calls[0]?.[0]).toEqual({ path: { id: 'routine' } }));
  });

  it('shows the full diversion impact and approves the diversion', async () => {
    mocks.approve.mockResolvedValue({});
    renderWithProviders(<ProposalQueue />);
    expect(await screen.findByText('Extra wait imposed')).toBeInTheDocument();
    expect(screen.getByText('None free')).toBeInTheDocument();
    expect(screen.getByText('12 min')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Approve diversion' }));
    await waitFor(() => expect(mocks.approve.mock.calls[0]?.[0]).toEqual({ path: { id: 'diversion' }, body: {} }));
  });

  it('requires a structured reason and notes before rejecting', async () => {
    mocks.reject.mockResolvedValue({});
    renderWithProviders(<ProposalQueue />);
    await userEvent.click((await screen.findAllByRole('button', { name: 'Reject' }))[0]);
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /reason/i }));
    await userEvent.click(await screen.findByRole('option', { name: 'Ambulance unsuitable' }));
    await userEvent.type(within(dialog).getByLabelText('Review notes'), 'Vehicle lacks required equipment.');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Reject recommendation' }));
    await waitFor(() => expect(mocks.reject.mock.calls[0]?.[0]).toEqual({
      path: { id: 'routine' },
      body: { reason: 'ambulance_unsuitable', notes: 'Vehicle lacks required equipment.' },
    }));
  });

  it('renders failed agent errors inline', async () => {
    renderWithProviders(<ProposalQueue />);
    expect(await screen.findByText('No staffed ambulance is currently available.')).toBeInTheDocument();
    expect(screen.getByText('This recommendation cannot be applied as shown.')).toBeInTheDocument();
  });

  it('renders confirm-time validation failures inline', async () => {
    mocks.confirm.mockRejectedValue({ status: 409, failed_checks: ['ambulance_eligible'] });
    renderWithProviders(<ProposalQueue />);
    await userEvent.click(await screen.findByRole('button', { name: 'Send' }));
    expect(await screen.findByText('ambulance_eligible')).toBeInTheDocument();
  });
});
