import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr';
import type { QueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import {
  getMyUnreadNotificationCountQueryKey,
  listMyNotificationsOptions,
  listMyNotificationsQueryKey,
} from '../api/generated/@tanstack/react-query.gen';
import { ensureFreshSession, getAccessToken, getSession, subscribe } from '../auth/session';

let connection: HubConnection | null = null;
let queryClient: QueryClient | null = null;

function invalidateInbox(): void {
  queryClient?.invalidateQueries({ queryKey: listMyNotificationsQueryKey() });
  queryClient?.invalidateQueries({ queryKey: getMyUnreadNotificationCountQueryKey() });
}

async function announceLatestUnread(): Promise<void> {
  if (!queryClient) return;

  try {
    const page = await queryClient.fetchQuery(
      listMyNotificationsOptions({ query: { unreadOnly: true, page: 1, pageSize: 1 } }),
    );
    const title = page.items[0]?.title;
    if (title) toast.info(title);
  } catch {
    // Best effort - the badge and list already refresh from invalidateInbox above.
  }
}

async function start(): Promise<void> {
  if (connection) return;

  const hub = new HubConnectionBuilder()
    .withUrl('/api/hubs/notifications', {
      accessTokenFactory: async () => {
        await ensureFreshSession();
        return getAccessToken() ?? '';
      },
    })
    .withAutomaticReconnect()
    .build();

  hub.on('inboxChanged', () => {
    invalidateInbox();
    void announceLatestUnread();
  });

  // The inbox is the source of truth, so whatever happened while disconnected
  // is only missing from the cache, not from the server - a re-read catches it up.
  hub.onreconnected(invalidateInbox);

  connection = hub;

  try {
    await hub.start();
  } catch {
    if (connection === hub) connection = null;
  }
}

async function stop(): Promise<void> {
  const current = connection;
  connection = null;
  if (current) await current.stop();
}

// The web app is staff-only (App.tsx sends a patient session straight to the mobile app
// message), so a patient session never needs its own live connection here.
function isStaffSession(): boolean {
  return getSession()?.principal.principal_type === 'staff';
}

export function initNotificationsRealtime(client: QueryClient): void {
  queryClient = client;

  subscribe((session) => {
    if (session && session.principal.principal_type === 'staff') void start();
    else void stop();
  });

  if (isStaffSession()) void start();
}
