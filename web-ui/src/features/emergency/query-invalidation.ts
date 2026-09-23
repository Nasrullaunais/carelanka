import type { QueryClient } from '@tanstack/react-query';

const operationalQueryIds = new Set([
  'listEmergencyCalls',
  'getEmergencyCall',
  'listAmbulances',
  'getCurrentAmbulanceCrew',
  'listDispatchProposals',
  'getDispatchProposal',
  'listEmergencyCancellationRequests',
]);

export function invalidateEmergencyQueries(queryClient: QueryClient) {
  return queryClient.invalidateQueries({
    predicate: (query) => {
      const id = (query.queryKey[0] as { _id?: string } | undefined)?._id;
      return id !== undefined && operationalQueryIds.has(id);
    },
  });
}
