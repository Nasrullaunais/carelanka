import type { InboxNotification } from '../api/generated';

export type FetchUnread = () => Promise<InboxNotification[]>;

// The server sends the same "inbox changed" signal for a new notification and for a
// mark-read, so only ids never seen before count as new.
export function createNewNotificationTracker(fetchUnread: FetchUnread) {
  let seen = new Set<string>();
  let baseline: Promise<boolean> | null = null;

  function remember(items: InboxNotification[]): InboxNotification[] {
    const fresh = items.filter((item) => item.id && !seen.has(item.id));
    for (const item of fresh) seen.add(item.id!);
    return fresh;
  }

  async function recordExisting(): Promise<boolean> {
    try {
      remember(await fetchUnread());
      return true;
    } catch {
      return false;
    }
  }

  return {
    start(): void {
      baseline ??= recordExisting();
    },

    async takeNew(): Promise<InboxNotification[]> {
      // Without a baseline every old unread item would pop up at once, so a failed one is retried
      // here instead, at the cost of not announcing this one change.
      if (!(await (baseline ??= recordExisting()))) {
        baseline = recordExisting();
        return [];
      }
      return remember(await fetchUnread());
    },

    reset(): void {
      seen = new Set();
      baseline = null;
    },
  };
}
