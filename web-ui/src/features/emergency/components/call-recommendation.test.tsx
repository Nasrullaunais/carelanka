import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { DispatchProposalSummary } from '../../../services/api/generated';
import { renderWithProviders } from '../../../test/api-mocks';
import { CallRecommendation } from './call-recommendation';

const mocks = vi.hoisted(() => ({ confirm: vi.fn(), approve: vi.fn(), reject: vi.fn(), recheck: vi.fn() }));
const details = {
  routine: { id: 'routine', status: 'pending_confirmation', proposed_ambulance_registration: 'WP-CA-1234', estimated_minutes_to_scene: 8, rationale: 'Closest ready crew.', proposed_ambulance_current_crew_count: 2, proposed_ambulance_required_crew_count: 2 },
  diversion: {
    id: 'diversion', status: 'pending_approval', is_diversion: true, proposed_ambulance_registration: 'WP-CA-5678', estimated_minutes_to_scene: 4, rationale: 'Saves twelve minutes.',
    diversion_impact: {
      source_call_id: 'source-call', source_call_priority: 'low', source_dispatch_status: 'en_route_to_scene',
      source_call_waiting_minutes_so_far: 9, source_call_additional_wait_minutes: 6, replacement_ambulance_registration: null,
      minutes_saved_for_this_call: 12,
    },
  },
  failed: { id: 'failed', status: 'failed', outcome: 'no_ambulance_available', errors: [{ step: 'run', message: 'No staffed ambulance is currently available.' }] },
  rejected: { id: 'rejected', status: 'rejected', rejection_reason: 'handled_another_way' },
};

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('../../../services/api/generated/@tanstack/react-query.gen', () => ({
  getDispatchProposalOptions: ({ path }: { path: { id: keyof typeof details } }) => ({
    queryKey: ['proposal', path.id], queryFn: () => Promise.resolve(details[path.id]),
  }),
  confirmDispatchProposalMutation: () => ({ mutationFn: mocks.confirm }),
  approveDispatchProposalMutation: () => ({ mutationFn: mocks.approve }),
  rejectDispatchProposalMutation: () => ({ mutationFn: mocks.reject }),
  createDispatchProposalMutation: () => ({ mutationFn: mocks.recheck }),
}));

function summary(id: keyof typeof details): DispatchProposalSummary {
  return details[id] as DispatchProposalSummary;
}

describe('CallRecommendation', () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it('says the agent is still checking', () => {
    renderWithProviders(<CallRecommendation callId="call-1" latest={{ id: 'p', status: 'pending' }} onSent={vi.fn()} />);
    expect(screen.getByText('Checking available ambulances…')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Send' })).not.toBeInTheDocument();
  });

  it('sends a routine recommendation in one tap and moves on', async () => {
    mocks.confirm.mockResolvedValue({});
    const onSent = vi.fn();
    renderWithProviders(<CallRecommendation callId="call-1" latest={summary('routine')} onSent={onSent} />);

    expect(await screen.findByText('WP-CA-1234')).toBeInTheDocument();
    expect(screen.getByText('8 min to scene · Crew 2/2')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Send' }));

    await waitFor(() => expect(mocks.confirm.mock.calls[0]?.[0]).toEqual({ path: { id: 'routine' } }));
    await waitFor(() => expect(onSent).toHaveBeenCalledTimes(1));
  });

  it('shows the full diversion impact and approves the diversion', async () => {
    mocks.approve.mockResolvedValue({});
    renderWithProviders(<CallRecommendation callId="call-1" latest={summary('diversion')} onSent={vi.fn()} />);

    expect(await screen.findByText('Extra wait imposed')).toBeInTheDocument();
    expect(screen.getByText('None free')).toBeInTheDocument();
    expect(screen.getByText('12 min')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Send' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Approve diversion' }));

    await waitFor(() => expect(mocks.approve.mock.calls[0]?.[0]).toEqual({ path: { id: 'diversion' }, body: {} }));
  });

  it('requires a structured reason and notes before rejecting', async () => {
    mocks.reject.mockResolvedValue({});
    renderWithProviders(<CallRecommendation callId="call-1" latest={summary('routine')} onSent={vi.fn()} />);

    await userEvent.click(await screen.findByRole('button', { name: 'Reject' }));
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

  it('renders confirm-time validation failures inline', async () => {
    mocks.confirm.mockRejectedValue({ status: 409, failed_checks: ['ambulance_eligible'] });
    const onSent = vi.fn();
    renderWithProviders(<CallRecommendation callId="call-1" latest={summary('routine')} onSent={onSent} />);

    await userEvent.click(await screen.findByRole('button', { name: 'Send' }));

    expect(await screen.findByText('ambulance_eligible')).toBeInTheDocument();
    expect(screen.getByText('This recommendation cannot be applied as shown.')).toBeInTheDocument();
    expect(onSent).not.toHaveBeenCalled();
  });

  it('explains a failed recommendation and offers a re-check', async () => {
    mocks.recheck.mockResolvedValue({});
    renderWithProviders(<CallRecommendation callId="call-1" latest={summary('failed')} onSent={vi.fn()} />);

    expect(screen.getByText('No recommendation — pick an ambulance below.')).toBeInTheDocument();
    expect(screen.getByText('No ambulance available')).toBeInTheDocument();
    expect(await screen.findByText('No staffed ambulance is currently available.')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Re-check' }));

    await waitFor(() => expect(mocks.recheck.mock.calls[0]?.[0]).toEqual({ body: { emergency_call_id: 'call-1', allow_diversion: true } }));
  });

  it('stops saying no ambulance is free once one is', () => {
    renderWithProviders(<CallRecommendation callId="call-1" latest={summary('failed')} ambulanceFree onSent={vi.fn()} />);

    expect(screen.getByText('An ambulance has come free since then. Re-check for a recommendation.')).toBeInTheDocument();
    expect(screen.queryByText('No ambulance available')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Re-check' })).toBeInTheDocument();
  });

  it('says why there is no recommendation after a rejection', async () => {
    renderWithProviders(<CallRecommendation callId="call-1" latest={summary('rejected')} onSent={vi.fn()} />);
    expect(await screen.findByText('Last one rejected: Handled another way.')).toBeInTheDocument();
  });
});
