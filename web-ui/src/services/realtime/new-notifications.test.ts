import { describe, expect, it, vi } from 'vitest';
import type { InboxNotification } from '../api/generated';
import { createNewNotificationTracker } from './new-notifications';

const item = (id: string): InboxNotification => ({ id, type: 'emergency_call_received', title: id });

describe('createNewNotificationTracker', () => {
  it('does not announce what was already unread when the session started', async () => {
    let unread = [item('old')];
    const tracker = createNewNotificationTracker(() => Promise.resolve(unread));
    tracker.start();

    unread = [item('new'), item('old')];
    expect(await tracker.takeNew()).toEqual([item('new')]);
  });

  it('announces nothing when the change was only something being marked read', async () => {
    let unread = [item('a'), item('b')];
    const tracker = createNewNotificationTracker(() => Promise.resolve(unread));
    tracker.start();

    unread = [item('b')];
    expect(await tracker.takeNew()).toEqual([]);
  });

  it('announces each notification once, even after it stays unread', async () => {
    let unread: InboxNotification[] = [];
    const tracker = createNewNotificationTracker(() => Promise.resolve(unread));
    tracker.start();

    unread = [item('a')];
    expect(await tracker.takeNew()).toEqual([item('a')]);
    unread = [item('b'), item('a')];
    expect(await tracker.takeNew()).toEqual([item('b')]);
  });

  it('does not flood old items when the first read failed', async () => {
    const fetchUnread = vi.fn<() => Promise<InboxNotification[]>>()
      .mockRejectedValueOnce(new Error('offline'))
      .mockResolvedValue([item('old')]);
    const tracker = createNewNotificationTracker(fetchUnread);
    tracker.start();

    expect(await tracker.takeNew()).toEqual([]);
    fetchUnread.mockResolvedValue([item('new'), item('old')]);
    expect(await tracker.takeNew()).toEqual([item('new')]);
  });

  it('starts over for the next person after a sign-out', async () => {
    let unread = [item('mine')];
    const tracker = createNewNotificationTracker(() => Promise.resolve(unread));
    tracker.start();
    expect(await tracker.takeNew()).toEqual([]);

    tracker.reset();
    unread = [item('theirs-old')];
    tracker.start();
    unread = [item('theirs-new'), item('theirs-old')];
    expect(await tracker.takeNew()).toEqual([item('theirs-new')]);
  });
});
