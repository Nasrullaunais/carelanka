import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../../test/api-mocks';
import { useActionableProposals } from './use-actionable-proposals';

vi.mock('../../../services/api/generated', () => ({
  listDispatchProposals: vi.fn(async ({ query }) => ({ data: {
    items: query.Status === 'pending' ? [{ id: `processing-${query.Page}`, status: 'pending', created_at: '2026-09-25T00:00:00Z' }] : [],
    page: query.Page, total_pages: query.Status === 'pending' ? 2 : 1,
    total_items: query.Status === 'pending' ? 2 : 0, page_size: 1,
  } })),
}));

function Queue() {
  const queue = useActionableProposals();
  return <div><span>Pending: {queue.pendingCount}</span>{queue.items.map((item) => <p key={item.id}>{item.id}</p>)}{queue.hasNextPage && <button onClick={() => void queue.fetchNextPage()}>More</button>}</div>;
}

afterEach(cleanup);

it('shows processing proposals and makes every result page reachable', async () => {
  renderWithProviders(<Queue />);
  expect(await screen.findByText('processing-1')).toBeInTheDocument();
  expect(screen.getByText('Pending: 2')).toBeInTheDocument();
  await userEvent.click(screen.getByRole('button', { name: 'More' }));
  expect(await screen.findByText('processing-2')).toBeInTheDocument();
  await waitFor(() => expect(screen.queryByRole('button', { name: 'More' })).not.toBeInTheDocument());
});
