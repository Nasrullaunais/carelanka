import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/api-mocks';
import { ReorderSuggestionPanel } from './ReorderSuggestionPanel';

const mocks = vi.hoisted(() => ({
  start: vi.fn(),
  apply: vi.fn(),
  workflow: undefined as Record<string, unknown> | undefined,
}));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('../../services/api/generated/@tanstack/react-query.gen', () => ({
  submitReorderSuggestionMutation: () => ({ mutationFn: mocks.start }),
  getReorderSuggestionWorkflowOptions: () => ({
    queryKey: ['reorder-suggestion-workflow'],
    queryFn: () => Promise.resolve(mocks.workflow),
  }),
  updateReorderThresholdMutation: () => ({ mutationFn: mocks.apply }),
}));

const ITEM = {
  id: 'item-1',
  name: 'Amoxicillin 250mg',
  category_id: 'cat-1',
  category_name: 'Antibiotics',
  unit: 'tablet',
  batch_count: 2,
  quantity_on_hand: 10,
  reorder_threshold: 20,
  is_available: true,
  below_threshold: true,
  created_at: '2026-01-01T00:00:00Z',
  updated_at: '2026-01-01T00:00:00Z',
};

const COMPLETED_MODEL_WORKFLOW = {
  workflow_id: 'wf-1',
  status: 'completed',
  plan: ['gather_item', 'gather_dispensing_history', 'draft_suggestion', 'validate_deterministically', 'pause_for_review'],
  steps: [],
  current_threshold: 20,
  current_quantity_on_hand: 10,
  suggested_threshold: 42,
  reasoning: 'Usage has been steady, so 42 tablets covers the lead time.',
  source: 'model',
};

describe('ReorderSuggestionPanel', () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    mocks.workflow = undefined;
  });

  it('starts a suggestion run for this item when Suggest is clicked', async () => {
    // API-INTEGRATION testing: clicking "Suggest" must call the submit mutation with
    // { path: { id: item.id } } (line 81), starting the Reorder-Threshold agent for
    // exactly this medicine.
    mocks.start.mockResolvedValue({ workflow_id: 'wf-1' });
    mocks.workflow = COMPLETED_MODEL_WORKFLOW;
    renderWithProviders(<ReorderSuggestionPanel item={ITEM} onClose={vi.fn()} onChanged={vi.fn()} />);

    await userEvent.click(screen.getByRole('button', { name: 'Suggest' }));

    await waitFor(() => expect(mocks.start.mock.calls[0]?.[0]).toEqual({ path: { id: 'item-1' } }));
  });

  it('shows the suggested threshold and reasoning once the run completes', async () => {
    // UI-STATE testing: once workflow.status is 'completed' (line 121), the panel shows the
    // current/on-hand figures, the suggested threshold, and the agent's reasoning text.
    mocks.start.mockResolvedValue({ workflow_id: 'wf-1' });
    mocks.workflow = COMPLETED_MODEL_WORKFLOW;
    renderWithProviders(<ReorderSuggestionPanel item={ITEM} onClose={vi.fn()} onChanged={vi.fn()} />);

    await userEvent.click(screen.getByRole('button', { name: 'Suggest' }));

    expect(await screen.findByText(/Suggested: 42 tablet/)).toBeInTheDocument();
    expect(
      screen.getByText('Usage has been steady, so 42 tablets covers the lead time.'),
    ).toBeInTheDocument();
    expect(screen.getByText(/Current threshold: 20 tablet/)).toBeInTheDocument();
  });

  it('marks a formula-based suggestion as estimated, not from the model', async () => {
    // UI-STATE testing: when source is anything other than 'model' - here
    // 'model_unavailable', the deterministic fallback the backend agent uses when the
    // model can't be reached or RT1 rejects its draft - the panel adds the "estimated"
    // note (line 142) so the reviewer knows which number they are looking at.
    mocks.start.mockResolvedValue({ workflow_id: 'wf-1' });
    mocks.workflow = { ...COMPLETED_MODEL_WORKFLOW, source: 'model_unavailable' };
    renderWithProviders(<ReorderSuggestionPanel item={ITEM} onClose={vi.fn()} onChanged={vi.fn()} />);

    await userEvent.click(screen.getByRole('button', { name: 'Suggest' }));

    expect(await screen.findByText(/estimated, the model was unavailable/)).toBeInTheDocument();
  });

  it('applies the suggested threshold with the item id and the suggested number', async () => {
    // Component + API-INTEGRATION testing: clicking "Apply" must call the update mutation
    // with { path: { id: item.id }, body: { reorder_threshold: suggested } } (line 132).
    mocks.start.mockResolvedValue({ workflow_id: 'wf-1' });
    mocks.workflow = COMPLETED_MODEL_WORKFLOW;
    mocks.apply.mockResolvedValue(undefined);
    const onChanged = vi.fn();
    const onClose = vi.fn();
    renderWithProviders(<ReorderSuggestionPanel item={ITEM} onClose={onClose} onChanged={onChanged} />);

    await userEvent.click(screen.getByRole('button', { name: 'Suggest' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Apply' }));

    await waitFor(() =>
      expect(mocks.apply.mock.calls[0]?.[0]).toEqual({
        path: { id: 'item-1' },
        body: { reorder_threshold: 42 },
      }),
    );
    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it('offers to retry rather than apply anything when the run fails', async () => {
    // Safe-failure testing on the UI side: when the agent exhausts its retries and the run
    // ends as 'failed' (ReorderAgent.SafeFailure on the backend), the panel never shows an
    // Apply button for a number that doesn't exist - it tells the reviewer to try again or
    // edit the threshold by hand (line 109).
    mocks.start.mockResolvedValue({ workflow_id: 'wf-1' });
    mocks.workflow = { workflow_id: 'wf-1', status: 'failed' };
    renderWithProviders(<ReorderSuggestionPanel item={ITEM} onClose={vi.fn()} onChanged={vi.fn()} />);

    await userEvent.click(screen.getByRole('button', { name: 'Suggest' }));

    expect(
      await screen.findByText(
        'The agent could not complete this run. Try again, or edit the threshold by hand.',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Apply' })).not.toBeInTheDocument();
  });
});
