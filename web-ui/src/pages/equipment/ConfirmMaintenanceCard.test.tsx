import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/api-mocks';
import { ConfirmMaintenanceCard } from './ConfirmMaintenanceCard';

const mocks = vi.hoisted(() => ({
  confirm: vi.fn(),
  jobs: [] as unknown[],
  role: undefined as string | undefined,
}));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('../../services/auth/useSession', () => ({
  useSession: () => (mocks.role ? { principal: { role: mocks.role } } : null),
}));
vi.mock('../../services/api/generated/@tanstack/react-query.gen', () => ({
  listMaintenanceSchedulesAwaitingConfirmationOptions: () => ({
    queryKey: ['maintenance-awaiting-confirmation'],
    queryFn: () => Promise.resolve(mocks.jobs),
  }),
  confirmMaintenanceScheduleMutation: () => ({ mutationFn: mocks.confirm }),
}));

const OPEN_JOB = {
  id: 'job-1',
  asset_type: 'equipment_item',
  asset_id: 'item-1',
  asset_label: 'Ventilator (EQ-0001)',
  schedule_type: 'repair',
  scheduled_date: '2026-01-10',
  status: 'scheduled',
  notes: 'Alarm will not stop beeping.',
  created_by: 'user',
  created_at: '2026-01-05T00:00:00Z',
  updated_at: '2026-01-05T00:00:00Z',
};

async function unlockWith(code: string) {
  await userEvent.type(screen.getByLabelText('Confirmation code'), code);
  await userEvent.click(screen.getByRole('button', { name: 'Unlock' }));
}

describe('ConfirmMaintenanceCard', () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    mocks.jobs = [];
    mocks.role = undefined;
  });

  it('keeps the Unlock button disabled until a code is typed', async () => {
    // FORM-VALIDATION testing: ConfirmMaintenanceCard.tsx disables the Unlock button
    // while `entered.trim().length === 0` (line 91), same guard as the equipment card.
    renderWithProviders(<ConfirmMaintenanceCard />);

    const unlockButton = screen.getByRole('button', { name: 'Unlock' });
    expect(unlockButton).toBeDisabled();

    await userEvent.type(screen.getByLabelText('Confirmation code'), 'test-confirmation-code');

    expect(unlockButton).toBeEnabled();
  });

  it('shows the empty-queue message once unlocked with no open jobs', async () => {
    // UI-STATE testing: with the code accepted and an empty list, the card shows
    // "Nothing is open" (line 130) instead of a table.
    mocks.jobs = [];
    renderWithProviders(<ConfirmMaintenanceCard />);

    await unlockWith('test-confirmation-code');

    expect(
      await screen.findByText('Nothing is open. Every fault and service has been confirmed.'),
    ).toBeInTheDocument();
  });

  it('opens a confirm dialog naming the asset, and confirms with the job id and code', async () => {
    // Component + API-INTEGRATION testing: clicking "Confirm done" on a row must open
    // ConfirmDoneDialog naming that job's asset (line 194, title={`Confirm ${job.asset_label}
    // is done?`}), and confirming it must call the mutation with { path: { id: job.id },
    // headers: { [CODE_HEADER]: code } } (line 203).
    mocks.jobs = [OPEN_JOB];
    mocks.confirm.mockResolvedValue(OPEN_JOB);
    renderWithProviders(<ConfirmMaintenanceCard />);

    await unlockWith('test-confirmation-code');

    await userEvent.click(await screen.findByRole('button', { name: 'Confirm done' }));

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Confirm Ventilator (EQ-0001) is done?')).toBeInTheDocument();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Confirm done' }));

    await waitFor(() =>
      expect(mocks.confirm.mock.calls[0]?.[0]).toEqual({
        path: { id: 'job-1' },
        headers: { 'X-Confirmation-Code': 'test-confirmation-code' },
      }),
    );
  });

  it('shows the queue immediately for an equipment administrator, with no code-entry form', async () => {
    // Same role carve-out as the equipment card: MaintenanceService.EnsureConfirmationCode
    // skips the check for this role, so the card skips asking for a code too.
    mocks.role = 'equipment_administrator';
    mocks.jobs = [OPEN_JOB];
    renderWithProviders(<ConfirmMaintenanceCard />);

    expect(screen.queryByLabelText('Confirmation code')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Unlock' })).not.toBeInTheDocument();
    expect(await screen.findByText('Ventilator (EQ-0001)')).toBeInTheDocument();
  });

  it('an equipment administrator confirms with an empty confirmation-code header', async () => {
    mocks.role = 'equipment_administrator';
    mocks.jobs = [OPEN_JOB];
    mocks.confirm.mockResolvedValue(OPEN_JOB);
    renderWithProviders(<ConfirmMaintenanceCard />);

    await userEvent.click(await screen.findByRole('button', { name: 'Confirm done' }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Confirm done' }));

    await waitFor(() =>
      expect(mocks.confirm.mock.calls[0]?.[0]).toEqual({
        path: { id: 'job-1' },
        headers: { 'X-Confirmation-Code': '' },
      }),
    );
  });
});
