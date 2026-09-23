import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { listDispatchProposalsOptions } from '../../../services/api/generated/@tanstack/react-query.gen';

const PAGE_SIZE = 100;
const REFRESH_MS = 5_000;

function proposalOptions(status: 'pending_confirmation' | 'pending_approval' | 'failed') {
  return {
    ...listDispatchProposalsOptions({ query: { Status: status, Page: 1, PageSize: PAGE_SIZE } }),
    refetchInterval: REFRESH_MS,
  };
}

export function useActionableProposals() {
  const confirmations = useQuery(proposalOptions('pending_confirmation'));
  const approvals = useQuery(proposalOptions('pending_approval'));
  const failures = useQuery(proposalOptions('failed'));

  const items = useMemo(() => [
    ...(confirmations.data?.items ?? []),
    ...(approvals.data?.items ?? []),
    ...(failures.data?.items ?? []),
  ].sort((left, right) => Date.parse(right.created_at ?? '') - Date.parse(left.created_at ?? '')), [
    confirmations.data,
    approvals.data,
    failures.data,
  ]);

  return {
    items,
    pendingCount: (confirmations.data?.total_items ?? 0) + (approvals.data?.total_items ?? 0),
    isPending: confirmations.isPending || approvals.isPending || failures.isPending,
    error: confirmations.error ?? approvals.error ?? failures.error,
    refetch: async () => {
      await Promise.all([confirmations.refetch(), approvals.refetch(), failures.refetch()]);
    },
  };
}
