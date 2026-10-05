import { HubConnectionBuilder, type HubConnection } from '@microsoft/signalr';
import type { QueryClient } from '@tanstack/react-query';
import type { InboxNotification } from '../api/generated';
import {
  getMyUnreadNotificationCountQueryKey,
  listMyNotificationsOptions,
  listMyNotificationsQueryKey,
} from '../api/generated/@tanstack/react-query.gen';
import { ensureFreshSession, getAccessToken, getSession, subscribe } from '../auth/session';
import { createNewNotificationTracker } from './new-notifications';

// Wider than one burst of calls, so several arriving together all get a popup.
const NEW_NOTIFICATION_WINDOW = 20;

type NewNotificationsListener = (notifications: InboxNotification[]) => void;

let connection: HubConnection | null = null;
let queryClient: QueryClient | null = null;
const listeners = new Set<NewNotificationsListener>();

const tracker = createNewNotificationTracker(async () => {
  if (!queryClient) return [];
  const page = await queryClient.fetchQuery({
    ...listMyNotificationsOptions({ query: { unreadOnly: true, page: 1, pageSize: NEW_NOTIFICATION_WINDOW } }),
    staleTime: 0,
  });
  return page.items;
});

/** Newest first. Each notification is passed once per session. */
export function onNewNotifications(listener: NewNotificationsListener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function invalidateInbox(): void {
  queryClient?.invalidateQueries({ queryKey: listMyNotificationsQueryKey() });
  queryClient?.invalidateQueries({ queryKey: getMyUnreadNotificationCountQueryKey() });
}

async function announceNew(): Promise<void> {
  try {
    const fresh = await tracker.takeNew();
    if (fresh.length > 0) for (const listener of listeners) listener(fresh);
  } catch {
    // Best effort - the badge and list already refresh from invalidateInbox.
  }
}

async function start(): Promise<void> {
  if (connection) return;
  tracker.start();

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
    void announceNew();
  });

  // The inbox is the source of truth, so whatever happened while disconnected
  // is only missing from the cache, not from the server - a re-read catches it up.
  hub.onreconnected(() => {
    invalidateInbox();
    void announceNew();
  });

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
  tracker.reset();
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
