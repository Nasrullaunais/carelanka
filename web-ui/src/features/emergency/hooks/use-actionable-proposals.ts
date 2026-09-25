import { useInfiniteQuery } from '@tanstack/react-query';
import { listDispatchProposals } from '../../../services/api/generated';
import type { ListDispatchProposalsError, DispatchProposalSummaryPagedResult, DispatchProposalStatus } from '../../../services/api/generated';

const PAGE_SIZE = 25;

function useProposalPages(status: DispatchProposalStatus) {
  return useInfiniteQuery<DispatchProposalSummaryPagedResult, ListDispatchProposalsError, { pages: DispatchProposalSummaryPagedResult[]; pageParams: number[] }, readonly unknown[], number>({
    queryKey: [{ _id: 'listDispatchProposals', infinite: true, status }],
    initialPageParam: 1,
    queryFn: async ({ pageParam, signal }) => {
      const { data } = await listDispatchProposals({ query: { Status: status, Page: pageParam, PageSize: PAGE_SIZE }, signal, throwOnError: true });
      return data;
    },
    getNextPageParam: (lastPage) => lastPage.page < lastPage.total_pages ? lastPage.page + 1 : undefined,
    refetchInterval: 5_000,
  });
}

export function useActionableProposals() {
  const processing = useProposalPages('pending');
  const confirmations = useProposalPages('pending_confirmation');
  const approvals = useProposalPages('pending_approval');
  const failures = useProposalPages('failed');
  const queries = [processing, confirmations, approvals, failures];
  const items = queries.flatMap((query) => query.data?.pages.flatMap((page) => page.items) ?? [])
    .sort((left, right) => Date.parse(right.created_at ?? '') - Date.parse(left.created_at ?? ''));

  return {
    items: [...new Map(items.map((item) => [item.id, item])).values()],
    pendingCount: [processing, confirmations, approvals].reduce((total, query) => total + (query.data?.pages[0]?.total_items ?? 0), 0),
    isPending: queries.some((query) => query.isPending),
    error: queries.find((query) => query.error)?.error,
    hasNextPage: queries.some((query) => query.hasNextPage),
    isFetchingNextPage: queries.some((query) => query.isFetchingNextPage),
    fetchNextPage: () => Promise.all(queries.filter((query) => query.hasNextPage).map((query) => query.fetchNextPage())),
    refetch: () => Promise.all(queries.map((query) => query.refetch())),
  };
}
