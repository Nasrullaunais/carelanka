import type { ReactNode } from 'react';
import { TableBody, TableCell, TableColumn, TableContent, TableHeader, TableRoot, TableRow, TableScrollContainer } from '@heroui/react';
import { QueryError, QuerySkeleton } from './query-state';
import type { ApiProblem } from '../../services/api/errors';
import { PaginationControls } from './pagination-controls';

export interface DataTableColumn<Row> {
  key: string;
  header: ReactNode;
  cell: (row: Row) => ReactNode;
}

export interface DataTablePagination {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

export function DataTable<Row extends object>({ ariaLabel, rows, columns, rowKey, rowText, isLoading, error, onRetry, emptyMessage = 'Nothing to show yet.', onRowAction, pagination }: {
  ariaLabel: string;
  rows: Row[] | undefined;
  columns: Array<DataTableColumn<Row>>;
  rowKey: (row: Row) => string;
  rowText: (row: Row) => string;
  isLoading?: boolean;
  error?: ApiProblem | null;
  onRetry?: () => void;
  emptyMessage?: string;
  onRowAction?: (key: string) => void;
  pagination?: DataTablePagination;
}) {
  const data = rows ?? [];

  if (isLoading) return <QuerySkeleton rows={4} />;
  if (error != null) {
    return <QueryError error={error} context={`Could not load ${ariaLabel.toLowerCase()}.`} onRetry={onRetry ?? (() => {})} />;
  }
  if (data.length === 0) return <div className="table-frame"><p className="empty" role="status">{emptyMessage}</p>{pagination && <div className="table-footer"><PaginationControls label={ariaLabel} {...pagination} /></div>}</div>;

  return (
      <TableRoot className="gx-table">
        <TableScrollContainer>
          <TableContent
            aria-label={ariaLabel}
            selectionMode="none"
            onRowAction={onRowAction ? (key) => onRowAction(String(key)) : undefined}
          >
            <TableHeader columns={columns}>
              {(column) => <TableColumn id={column.key} isRowHeader={column.key === columns[0]?.key}>{column.header}</TableColumn>}
            </TableHeader>
            <TableBody items={data}>
              {(row) => (
                <TableRow id={rowKey(row)} columns={columns} textValue={rowText(row)}>
                  {(column) => <TableCell>{column.cell(row)}</TableCell>}
                </TableRow>
              )}
            </TableBody>
          </TableContent>
        </TableScrollContainer>
        {pagination && <div className="table-footer"><PaginationControls label={ariaLabel} {...pagination} /></div>}
      </TableRoot>
  );
}
