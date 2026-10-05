import { useMemo } from 'react';
import { Button } from '@heroui/react';
import type { EmergencyCallSummary, ListEmergencyCallsError } from '../../../services/api/generated';
import { DataTable, type DataTableColumn } from '../../../components/ui/data-table';
import { StatusChip } from '../../../components/ui/status-chip';
import { callStatusLabels, callStatusTones, compareByUrgency, formatWaiting, priorityLabels, priorityTones, recommendationLine, shortAddress } from '../domain';

export function CallBoard({ calls, selectedId, isLoading, error, onRetry, onSelect, page, totalPages, onPageChange, filter, onFilterChange }: {
  filter: 'received' | 'all';
  onFilterChange: (filter: 'received' | 'all') => void;
  calls: EmergencyCallSummary[] | undefined;
  selectedId?: string;
  isLoading: boolean;
  error: ListEmergencyCallsError | null;
  onRetry: () => void;
  onSelect: (id: string) => void;
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}) {
  const rows = useMemo(() => [...(calls ?? [])].sort(compareByUrgency), [calls]);

  const columns: Array<DataTableColumn<EmergencyCallSummary>> = [
    {
      key: 'priority',
      header: 'Priority',
      cell: (call) => {
        const priority = call.priority ?? 'high';
        return <StatusChip tone={priorityTones[priority]}>{priorityLabels[priority]}</StatusChip>;
      },
    },
    {
      key: 'caller',
      header: 'Caller and location',
      cell: (call) => (
        <div className="flex min-w-36 flex-col">
          <span>{call.caller_name ?? 'Unnamed caller'}</span>
          <span className="line-clamp-2 text-xs text-muted" title={call.address_label ?? undefined}>
            {call.address_label ? shortAddress(call.address_label) : 'Address resolving'}
          </span>
        </div>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      cell: (call) => {
        const status = call.status ?? 'received';
        return (
          <div className="flex flex-col items-start gap-1">
            <StatusChip tone={callStatusTones[status]}>{callStatusLabels[status]}</StatusChip>
            {call.waiting_minutes != null && <span className="whitespace-nowrap text-xs text-muted">Waiting {formatWaiting(call.waiting_minutes)}</span>}
          </div>
        );
      },
    },
    {
      key: 'recommendation',
      header: 'Recommendation',
      cell: (call) => {
        const line = recommendationLine(call);
        return line ? <span className="inline-flex [&_*]:whitespace-nowrap"><StatusChip tone={line.tone}>{line.label}</StatusChip></span> : null;
      },
    },
  ];

  return (
    <div className="flex flex-col gap-3">
      <div className="flex gap-2" aria-label="Call status filter">
        <Button size="sm" aria-pressed={filter === 'received'} variant={filter === 'received' ? 'primary' : 'outline'} onPress={() => onFilterChange('received')}>Awaiting dispatch</Button>
        <Button size="sm" aria-pressed={filter === 'all'} variant={filter === 'all' ? 'primary' : 'outline'} onPress={() => onFilterChange('all')}>All calls</Button>
      </div>
      <DataTable
        ariaLabel="Emergency calls"
        rows={rows}
        columns={columns}
        rowKey={(call) => call.id ?? ''}
        rowText={(call) => `${call.caller_name ?? 'Unnamed caller'} ${call.priority ?? 'high'}`}
        isLoading={isLoading}
        error={error}
        onRetry={onRetry}
        emptyMessage={filter === 'received' ? 'No calls are awaiting dispatch.' : 'No emergency calls were found.'}
        onRowAction={onSelect}
        currentRowKey={selectedId}
        pagination={{ page, totalPages, onPageChange }}
      />
    </div>
  );
}
