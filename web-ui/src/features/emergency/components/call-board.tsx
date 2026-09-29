import { useMemo } from 'react';
import { Button } from '@heroui/react';
import type { EmergencyCallSummary, ListEmergencyCallsError } from '../../../services/api/generated';
import { DataTable, type DataTableColumn } from '../../../components/ui/data-table';
import { StatusChip } from '../../../components/ui/status-chip';
import { callStatusLabels, callStatusTones, compareByUrgency, formatWaiting, priorityLabels, priorityTones, recommendationLine } from '../domain';

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
      header: 'Caller',
      cell: (call) => (
        <span className={selectedId === call.id ? 'font-semibold' : undefined}>
          {call.caller_name ?? 'Unnamed caller'}
        </span>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      cell: (call) => {
        const status = call.status ?? 'received';
        return <StatusChip tone={callStatusTones[status]}>{callStatusLabels[status]}</StatusChip>;
      },
    },
    {
      key: 'recommendation',
      header: 'Recommendation',
      cell: (call) => {
        const line = recommendationLine(call);
        return line ? <StatusChip tone={line.tone}>{line.label}</StatusChip> : null;
      },
    },
    { key: 'location', header: 'Location', cell: (call) => call.address_label ?? 'Address resolving' },
    { key: 'waiting', header: 'Waiting', cell: (call) => formatWaiting(call.waiting_minutes) },
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
        pagination={{ page, totalPages, onPageChange }}
      />
    </div>
  );
}
