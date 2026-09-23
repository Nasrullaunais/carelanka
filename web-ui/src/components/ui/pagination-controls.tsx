import { Button } from '@heroui/react';

export function PaginationControls({ label, page, totalPages, onPageChange }: {
  label: string;
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}) {
  if (totalPages <= 1) return null;
  return (
    <nav aria-label={`${label} pagination`} className="flex items-center justify-end gap-2">
      <Button size="sm" variant="outline" isDisabled={page <= 1} onPress={() => onPageChange(page - 1)}>Previous</Button>
      <span className="text-sm text-muted">Page {page} of {totalPages}</span>
      <Button size="sm" variant="outline" isDisabled={page >= totalPages} onPress={() => onPageChange(page + 1)}>Next</Button>
    </nav>
  );
}
