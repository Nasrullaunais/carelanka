import { Button } from '@heroui/react';

export function PaginationControls({ label, page, totalPages, onPageChange, totalItems }: {
  label: string;
  totalItems?: number;
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}) {
  return (
    <nav aria-label={`${label} pagination`} className="table-pagination">
      <span className="muted table-page-count">{totalItems != null && `${totalItems} items · `}Page {page} of {Math.max(1, totalPages)}</span>
      <Button size="sm" variant="outline" isDisabled={page <= 1} onPress={() => onPageChange(page - 1)}>Previous</Button>
      <Button size="sm" variant="outline" isDisabled={page >= totalPages} onPress={() => onPageChange(page + 1)}>Next</Button>
    </nav>
  );
}
