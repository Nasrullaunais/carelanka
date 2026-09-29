import { useEffect } from 'react';
import { hashKey, useQueryClient } from '@tanstack/react-query';
import type { UnreadNotificationCount } from '../../../services/api/generated';
import { getMyUnreadNotificationCountQueryKey } from '../../../services/api/generated/@tanstack/react-query.gen';
import { invalidateEmergencyQueries } from '../query-invalidation';

// The bell's live connection refreshes the unread count; a rise means something new
// happened, so the desk refreshes now instead of on its next poll.
export function useRefreshOnNewNotification(): void {
  const queryClient = useQueryClient();

  useEffect(() => {
    const unreadKey = hashKey(getMyUnreadNotificationCountQueryKey());
    let lastSeen = queryClient.getQueryData<UnreadNotificationCount>(getMyUnreadNotificationCountQueryKey())?.count;

    return queryClient.getQueryCache().subscribe((event) => {
      if (event.type !== 'updated' || event.action.type !== 'success' || event.query.queryHash !== unreadKey) return;
      const count = (event.query.state.data as UnreadNotificationCount | undefined)?.count;
      if (lastSeen !== undefined && count !== undefined && count > lastSeen) void invalidateEmergencyQueries(queryClient);
      lastSeen = count;
    });
  }, [queryClient]);
}

