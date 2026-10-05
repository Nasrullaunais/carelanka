import { act, cleanup, fireEvent, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { InboxNotification } from '../services/api/generated';
import { renderWithProviders } from '../test/api-mocks';
import { NotificationPopups } from './NotificationPopups';

const mocks = vi.hoisted(() => ({
  listener: undefined as ((items: InboxNotification[]) => void) | undefined,
  markRead: vi.fn(),
}));

vi.mock('../services/realtime/notifications', () => ({
  onNewNotifications: (listener: (items: InboxNotification[]) => void) => {
    mocks.listener = listener;
    return () => { mocks.listener = undefined; };
  },
}));
vi.mock('../services/auth/useSession', () => ({
  useSession: () => ({ principal: { role: 'duty_manager' } }),
}));
vi.mock('../services/api/generated/@tanstack/react-query.gen', () => ({
  getMyUnreadNotificationCountQueryKey: () => ['unread'],
  listMyNotificationsQueryKey: () => ['inbox'],
  markNotificationReadMutation: () => ({ mutationFn: mocks.markRead }),
  getEmergencyCallOptions: () => ({
    queryKey: ['call'],
    queryFn: () => Promise.resolve({ priority: 'critical', patient_name: 'N. Silva', address_label: 'Galle Face, Colombo', details: 'Not breathing' }),
  }),
}));

const newCall: InboxNotification = {
  id: 'n1',
  type: 'emergency_call_received',
  title: 'New emergency call',
  body: 'A new emergency call has come in.',
  entity_type: 'emergency_call',
  entity_id: 'call-1',
};

const quiet: InboxNotification = { id: 'n2', type: 'lab_report_ready', title: 'Lab report ready', body: 'A report is in.' };

function Where() {
  return <p data-testid="where">{useLocation().pathname}</p>;
}

function renderPopups() {
  renderWithProviders(<MemoryRouter><NotificationPopups /><Where /></MemoryRouter>);
}

function announce(...items: InboxNotification[]) {
  act(() => mocks.listener?.(items));
}

describe('NotificationPopups', () => {
  beforeEach(() => mocks.markRead.mockResolvedValue({}));
  afterEach(() => {
    cleanup();
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('shows a new call with who, where and what, and opens it', async () => {
    renderPopups();
    announce(newCall);

    expect(screen.getByRole('alert', { name: 'New emergency call' })).toBeInTheDocument();
    expect(await screen.findByText('N. Silva')).toBeInTheDocument();
    expect(screen.getByText('Critical')).toBeInTheDocument();
    expect(screen.getByText('“Not breathing”')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Open call' }));

    expect(screen.getByTestId('where')).toHaveTextContent('/emergency/calls/call-1');
    await waitFor(() => expect(mocks.markRead.mock.calls[0]?.[0]).toEqual({ path: { id: 'n1' } }));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('keeps an urgent popup until someone deals with it, but lets a routine one fade', () => {
    vi.useFakeTimers();
    renderPopups();
    announce(quiet, newCall);

    act(() => vi.advanceTimersByTime(10_000));

    expect(screen.queryByText('Lab report ready')).not.toBeInTheDocument();
    expect(screen.getByRole('alert', { name: 'New emergency call' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Later' }));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(mocks.markRead).not.toHaveBeenCalled();
  });

  it('pauses the fade while the pointer is over the popup', () => {
    vi.useFakeTimers();
    renderPopups();
    announce(quiet);

    fireEvent.mouseEnter(screen.getByRole('status', { name: 'Lab report ready' }));
    act(() => vi.advanceTimersByTime(10_000));

    expect(screen.getByText('Lab report ready')).toBeInTheDocument();
  });

  it('collapses a burst into three popups and a count of the rest', () => {
    renderPopups();
    announce(...['a', 'b', 'c', 'd', 'e'].map((id) => ({ ...quiet, id, title: `Item ${id}` })));

    expect(screen.getAllByRole('status')).toHaveLength(3);
    expect(screen.getByText('2 more new notifications')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Dismiss all' }));
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });
});
