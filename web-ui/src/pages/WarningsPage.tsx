import { PaginationControls } from '../components/ui/pagination-controls';
import { Table } from '../components/Table';
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  acknowledgeWarningMutation,
  listWarningsOptions,
  runWarningSweepMutation,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { Warning, WarningStatus, WarningType } from '../services/api/generated';
import { useSession } from '../services/auth/useSession';
import { canConfirmEquipment, canReadWarnings } from '../types/permissions';
import { localDateTime } from '../types/datetime';
import {
  raisedByLabels,
  warningSeverityLabels,
  warningStatuses,
  warningStatusLabels,
  warningTypeLabels,
  warningTypes,
} from '../types/warnings';
import { ClearWarningDialog } from './warnings/ClearWarningDialog';
import { AppSelect } from '../components/ui/app-select';

const PAGE_SIZE = 15;

export function WarningsPage() {
  const queryClient = useQueryClient();
  const session = useSession();
  const allowed = canReadWarnings(session?.principal.role);
  // Done is the hospital administrator's, with the confirmation code.
  const canClear = canConfirmEquipment(session?.principal.role);

  const [status, setStatus] = useState<WarningStatus>('open');
  const [type, setType] = useState<WarningType | ''>('');
  const [page, setPage] = useState(1);
  const [clearing, setClearing] = useState<Warning | null>(null);
  // The list stays hidden until the user presses Run check themselves.
  const [hasChecked, setHasChecked] = useState(false);

  const warnings = useQuery({
    ...listWarningsOptions({
      query: { status, type: type || undefined, page, pageSize: PAGE_SIZE },
    }),
    enabled: allowed && hasChecked,
  });

  const sweep = useMutation({
    ...runWarningSweepMutation(),
    onSuccess: (result) => {
      setHasChecked(true);

      const changes = [
        result.raised > 0 ? `${result.raised} new` : null,
        result.updated > 0 ? `${result.updated} updated` : null,
        result.resolved > 0 ? `${result.resolved} resolved` : null,
      ].filter(Boolean);

      toast.success(
        changes.length > 0
          ? `Check done: ${changes.join(', ')}.`
          : 'Check done. Nothing has changed since the last one.',
      );
      queryClient.invalidateQueries();
    },
  });

  const acknowledge = useMutation({
    ...acknowledgeWarningMutation(),
    onSuccess: () => {
      toast.success('Acknowledged. It stays on the list until the problem is fixed.');
      queryClient.invalidateQueries();
    },
  });

  if (!allowed) {
    return (
      <>
        <h1>Warnings</h1>
        <p className="empty">
          Warnings are for the equipment manager and the hospital administrator.
        </p>
      </>
    );
  }

  const rows = warnings.data?.items ?? [];
  const totalPages = warnings.data?.total_pages ?? 1;

  return (
    <>
      <h1>Warnings</h1>
      <p className="muted">
        An automatic check looks for medicine at or below its reorder level, batches expiring
        within 30 days, and machines overdue for service. It runs every hour, or now with{' '}
        <strong>Run check</strong>. It uses fixed rules, not AI. A warning closes by itself once
        the problem is gone: stock delivered, the batch used up, or the service booked.
      </p>

      <div className="table-section">
        <div className="actions">
          <button type="button" disabled={sweep.isPending} onClick={() => sweep.mutate({})}>
            {sweep.isPending ? 'Checking…' : 'Run check'}
          </button>
        </div>

        <div className="row">
          <div>
            <AppSelect
              id="warning-type"
              label="Kind"
              value={type}
              onValueChange={(value) => {
                setType(value as WarningType | '');
                setPage(1);
              }}
              options={[
                { value: '', label: 'Every kind' },
                ...warningTypes.map((value) => ({ value, label: warningTypeLabels[value] })),
              ]}
            />
          </div>
        </div>

        <div className="tabs">
          {warningStatuses.map((value) => (
            <button
              key={value}
              type="button"
              aria-pressed={status === value}
              onClick={() => {
                setStatus(value);
                setPage(1);
              }}
            >
              {warningStatusLabels[value]}
            </button>
          ))}
        </div>

        {!hasChecked && (
          <p className="empty">Press Run check to look for warnings.</p>
        )}

        {hasChecked && warnings.isPending && <p className="empty">Loading warnings…</p>}

        {hasChecked && warnings.isError && (
          <p className="empty">
            Warnings could not be loaded.{' '}
            <button type="button" className="secondary" onClick={() => warnings.refetch()}>
              Try again
            </button>
          </p>
        )}

        {hasChecked && warnings.isSuccess && rows.length === 0 && (
          <p className="empty">
            {status === 'open'
              ? 'Nothing needs attention. Run check to look again.'
              : `No ${warningStatusLabels[status].toLowerCase()} warnings.`}
          </p>
        )}

        {hasChecked && rows.length > 0 && (
          <Table footer={<PaginationControls label="Warnings" page={page} totalPages={totalPages} onPageChange={setPage} />}>
            <thead>
              <tr>
                <th>Severity</th>
                <th>Kind</th>
                <th>About</th>
                <th>What to do</th>
                <th>Raised</th>
                {(status === 'open' ||
                  status === 'acknowledged' ||
                  (status === 'action_taken' && canClear)) && <th />}
              </tr>
            </thead>
            <tbody>
              {rows.map((warning) => (
                <tr key={warning.id}>
                  <td>
                    <span className={`badge severity-${warning.severity}`}>
                      {warningSeverityLabels[warning.severity]}
                    </span>
                  </td>
                  <td>{warningTypeLabels[warning.type]}</td>
                  <td>{warning.related_entity_label ?? <span className="muted">Unknown</span>}</td>
                  <td>{warning.recommended_action}</td>
                  <td>
                    {localDateTime(warning.created_at)}
                    <br />
                    <span className="muted">{raisedByLabels[warning.raised_by]}</span>
                    {warning.acknowledged_at && (
                      <>
                        <br />
                        <span className="muted">
                          Seen {localDateTime(warning.acknowledged_at)}
                        </span>
                      </>
                    )}
                    {warning.resolved_at && (
                      <>
                        <br />
                        <span className="muted">
                          Closed {localDateTime(warning.resolved_at)}
                        </span>
                      </>
                    )}
                  </td>
                  {status === 'open' && (
                    <td>
                      <button
                        type="button"
                        className="secondary"
                        disabled={acknowledge.isPending}
                        onClick={() => acknowledge.mutate({ path: { id: warning.id } })}
                      >
                        Acknowledge
                      </button>
                    </td>
                  )}
                  {status === 'acknowledged' && <td />}
                  {status === 'action_taken' && canClear && (
                    <td>
                      <button type="button" onClick={() => setClearing(warning)}>
                        Done
                      </button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </div>

      {clearing && (
        <ClearWarningDialog
          warning={clearing}
          onClose={() => setClearing(null)}
          onDone={() => {
            setClearing(null);
            queryClient.invalidateQueries();
          }}
        />
      )}
    </>
  );
}
