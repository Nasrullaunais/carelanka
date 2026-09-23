import { useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  getReorderSuggestionWorkflowOptions,
  submitReorderSuggestionMutation,
  updateReorderThresholdMutation,
} from '../../services/api/generated/@tanstack/react-query.gen';
import type { PharmacyItem } from '../../services/api/generated';
import { AgentProgress } from '../../components/AgentProgress';

const POLL_INTERVAL_MS = 800;

const STEP_CAPTIONS: Record<string, string> = {
  gather_item: 'Reading the medicine',
  gather_dispensing_history: 'Checking recent dispensing',
  draft_suggestion: 'Working out a threshold',
  validate_deterministically: 'Double-checking the suggestion',
  pause_for_review: 'Wrapping up',
};

/**
 * One medicine's reorder-threshold advisor, opened from its row on the Pharmacy page. Same
 * submit-then-poll shape as `BedSuggestionPanel`. Applying the number is a separate call the
 * reviewer makes here, never something the agent does itself.
 */
export function ReorderSuggestionPanel({
  item,
  onClose,
  onChanged,
}: {
  item: PharmacyItem;
  onClose: () => void;
  onChanged: () => void;
}) {
  const [workflowId, setWorkflowId] = useState<string | null>(null);

  const start = useMutation({
    ...submitReorderSuggestionMutation(),
    onSuccess: (accepted) => {
      if (accepted.workflow_id) {
        setWorkflowId(accepted.workflow_id);
      }
    },
  });

  const workflow = useQuery({
    ...getReorderSuggestionWorkflowOptions({ path: { workflowId: workflowId ?? '' } }),
    enabled: workflowId !== null,
    refetchInterval: (query) => (query.state.data?.status === 'running' ? POLL_INTERVAL_MS : false),
  });

  const apply = useMutation({
    ...updateReorderThresholdMutation(),
    onSuccess: () => {
      toast.success(`${item.name}'s reorder threshold is updated.`);
      onChanged();
      onClose();
    },
  });

  const running = workflowId !== null && (workflow.data === undefined || workflow.data.status === 'running');
  const suggested = workflow.data?.suggested_threshold;

  return (
    <div className="drawer-body">
      <h3>Suggest a reorder threshold</h3>

      {workflowId === null && (
        <>
          <p className="muted">
            Reads {item.name}&rsquo;s recent dispensing and suggests a new reorder threshold,
            with a reason. Nothing changes until you apply it.
          </p>

          <div style={{ display: 'flex', gap: '0.5rem' }}>
            <button
              type="button"
              disabled={start.isPending}
              onClick={() => start.mutate({ path: { id: item.id } })}
            >
              {start.isPending ? 'Starting…' : 'Suggest'}
            </button>
            <button type="button" className="secondary" onClick={onClose}>
              Close
            </button>
          </div>
        </>
      )}

      {workflowId !== null && (
        <>
          {(workflow.isLoading || running) && (
            <AgentProgress plan={workflow.data?.plan} steps={workflow.data?.steps} captions={STEP_CAPTIONS} />
          )}

          {workflow.isError && (
            <div className="empty">
              <p>Could not load the suggestion run.</p>
              <button type="button" className="secondary" onClick={() => void workflow.refetch()}>
                Try again
              </button>
            </div>
          )}

          {workflow.data && !running && workflow.data.status === 'failed' && (
            <p className="empty">
              The agent could not complete this run. Try again, or edit the threshold by hand.
            </p>
          )}

          {workflow.data && !running && workflow.data.status === 'completed' && (
            <div className="card" style={{ marginTop: '0.75rem', padding: '0.75rem' }}>
              <p className="muted" style={{ margin: 0, fontSize: '0.85rem' }}>
                Current threshold: {workflow.data.current_threshold} {item.unit} · On hand:{' '}
                {workflow.data.current_quantity_on_hand} {item.unit}
              </p>
              <p style={{ margin: '0.35rem 0 0', fontSize: '1.05rem' }}>
                <strong>
                  Suggested: {suggested} {item.unit}
                </strong>
                {workflow.data.source && workflow.data.source !== 'model' && (
                  <span className="muted"> — estimated, the model was unavailable</span>
                )}
              </p>

              {workflow.data.reasoning && <p style={{ marginTop: '0.35rem' }}>{workflow.data.reasoning}</p>}

              <button
                type="button"
                style={{ marginTop: '0.5rem' }}
                disabled={apply.isPending || suggested == null}
                onClick={() =>
                  suggested != null &&
                  apply.mutate({ path: { id: item.id }, body: { reorder_threshold: suggested } })
                }
              >
                {apply.isPending ? 'Applying…' : 'Apply'}
              </button>
            </div>
          )}

          <button type="button" className="secondary" style={{ marginTop: '0.9rem' }} onClick={onClose}>
            Close
          </button>
        </>
      )}
    </div>
  );
}
