import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { toast } from 'sonner';
import { renderWithProviders } from '../test/api-mocks';
import { WarningsPage } from './WarningsPage';

const mocks = vi.hoisted(() => ({
  sweep: vi.fn(),
  acknowledge: vi.fn(),
  clear: vi.fn(),
  warnings: { items: [] as unknown[], page: 1, page_size: 15, total_items: 0, total_pages: 1 },
  role: 'equipment_manager' as string | undefined,
}));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('../services/auth/useSession', () => ({
  useSession: () => (mocks.role ? { principal: { role: mocks.role } } : null),
}));
vi.mock('../services/api/generated/@tanstack/react-query.gen', () => ({
  listWarningsOptions: () => ({
    queryKey: ['warnings'],
    queryFn: () => Promise.resolve(mocks.warnings),
  }),
  runWarningSweepMutation: () => ({ mutationFn: mocks.sweep }),
  acknowledgeWarningMutation: () => ({ mutationFn: mocks.acknowledge }),
  clearWarningMutation: () => ({ mutationFn: mocks.clear }),
}));

const OPEN_WARNING = {
  id: 'warn-1',
  type: 'low_stock',
  severity: 'critical',
  related_entity_type: 'pharmacy_item',
  related_entity_id: 'item-1',
  related_entity_label: 'Amoxicillin 250mg',
  recommended_action: 'Amoxicillin 250mg is out of stock. Reorder now.',
  status: 'open',
  raised_by: 'system',
  created_at: '2026-01-05T00:00:00Z',
  updated_at: '2026-01-05T00:00:00Z',
};

describe('WarningsPage', () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    mocks.warnings = { items: [], page: 1, page_size: 15, total_items: 0, total_pages: 1 };
    mocks.role = 'equipment_manager';
  });

  it('is closed to a role that is not the equipment manager or the hospital administrator', () => {
    // AUTHORIZATION / UI-STATE testing: canReadWarnings (types/permissions.ts) only allows
    // equipment_manager and hospital_administrator. Every other role sees the closed message,
    // never the Run check button or the list.
    mocks.role = 'ward_nurse';
    renderWithProviders(<WarningsPage />);

    expect(
      screen.getByText('Warnings are for the equipment manager and the hospital administrator.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Run check' })).not.toBeInTheDocument();
  });

  it('hides the list until Run check is pressed', () => {
    // UI-STATE testing: hasChecked starts false, so the query stays disabled and the page
    // shows the "Press Run check" prompt instead of a loading or empty table state.
    renderWithProviders(<WarningsPage />);

    expect(screen.getByText('Press Run check to look for warnings.')).toBeInTheDocument();
  });

  it('runs the sweep and shows the open warnings once it completes', async () => {
    // API-INTEGRATION + UI-STATE testing: clicking "Run check" calls the sweep mutation,
    // and once it resolves the list query becomes enabled and the rows render.
    mocks.sweep.mockResolvedValue({ raised: 1, updated: 0, resolved: 0 });
    mocks.warnings = { items: [OPEN_WARNING], page: 1, page_size: 15, total_items: 1, total_pages: 1 };
    renderWithProviders(<WarningsPage />);

    await userEvent.click(screen.getByRole('button', { name: 'Run check' }));

    expect(await screen.findByText('Amoxicillin 250mg')).toBeInTheDocument();
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Check done: 1 new.'));
  });

  it('reports no change when the sweep finds nothing new', async () => {
    // UI-STATE testing: when raised/updated/resolved are all zero, the sweep's success
    // handler (lines 51-61) falls back to the "nothing has changed" message rather than
    // an empty "Check done: ." sentence.
    mocks.sweep.mockResolvedValue({ raised: 0, updated: 0, resolved: 0 });
    renderWithProviders(<WarningsPage />);

    await userEvent.click(screen.getByRole('button', { name: 'Run check' }));

    await waitFor(() =>
      expect(toast.success).toHaveBeenCalledWith('Check done. Nothing has changed since the last one.'),
    );
  });

  it('acknowledges an open warning by its id', async () => {
    // API-INTEGRATION testing: the Acknowledge button on an open-status row calls the
    // mutation with { path: { id: warning.id } } (line 220).
    mocks.sweep.mockResolvedValue({ raised: 1, updated: 0, resolved: 0 });
    mocks.warnings = { items: [OPEN_WARNING], page: 1, page_size: 15, total_items: 1, total_pages: 1 };
    mocks.acknowledge.mockResolvedValue(undefined);
    renderWithProviders(<WarningsPage />);

    await userEvent.click(screen.getByRole('button', { name: 'Run check' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Acknowledge' }));

    await waitFor(() =>
      expect(mocks.acknowledge.mock.calls[0]?.[0]).toEqual({ path: { id: 'warn-1' } }),
    );
  });

  it('marks a resolved warning done by its id', async () => {
    // API-INTEGRATION testing: on the "Resolved" tab (action_taken status), the Done
    // button calls the clear mutation with { path: { id: warning.id } } (line 232).
    mocks.sweep.mockResolvedValue({ raised: 0, updated: 0, resolved: 0 });
    mocks.warnings = {
      items: [{ ...OPEN_WARNING, status: 'action_taken' }],
      page: 1,
      page_size: 15,
      total_items: 1,
      total_pages: 1,
    };
    mocks.clear.mockResolvedValue(undefined);
    renderWithProviders(<WarningsPage />);

    await userEvent.click(screen.getByRole('button', { name: 'Run check' }));
    await userEvent.click(screen.getByRole('button', { name: 'Resolved' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Done' }));

    await waitFor(() => expect(mocks.clear.mock.calls[0]?.[0]).toEqual({ path: { id: 'warn-1' } }));
  });
});
