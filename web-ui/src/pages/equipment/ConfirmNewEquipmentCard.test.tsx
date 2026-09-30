import { cleanup, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/api-mocks';
import { ConfirmNewEquipmentCard } from './ConfirmNewEquipmentCard';

const mocks = vi.hoisted(() => ({
  confirm: vi.fn(),
  reject: vi.fn(),
  items: [] as unknown[],
  role: undefined as string | undefined,
}));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('../../services/auth/useSession', () => ({
  useSession: () => (mocks.role ? { principal: { role: mocks.role } } : null),
}));
vi.mock('../../services/api/generated/@tanstack/react-query.gen', () => ({
  listEquipmentItemsAwaitingConfirmationOptions: () => ({
    queryKey: ['equipment-pending-confirmation'],
    queryFn: () => Promise.resolve(mocks.items),
  }),
  confirmEquipmentItemMutation: () => ({ mutationFn: mocks.confirm }),
  rejectEquipmentItemMutation: () => ({ mutationFn: mocks.reject }),
}));

const PENDING_ITEM = {
  id: 'item-1',
  created_at: '2026-01-05T00:00:00Z',
  name: 'Ventilator',
  asset_tag: 'EQ-0001',
  category_name: 'Respiratory',
  manufacturer: 'Acme Medical',
  model: 'V-100',
  serial_number: 'SN-123',
  ward_name: 'Ward 4',
  purchase_date: '2026-01-05',
};

async function unlockWith(code: string) {
  await userEvent.type(screen.getByLabelText('Confirmation code'), code);
  await userEvent.click(screen.getByRole('button', { name: 'Unlock' }));
}

describe('ConfirmNewEquipmentCard', () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    mocks.items = [];
    mocks.role = undefined;
  });

  it('keeps the Unlock button disabled until a code is typed', async () => {
    // This is FORM-VALIDATION testing: ConfirmNewEquipmentCard.tsx disables the
    // Unlock button while `entered.trim().length === 0` (line 107). A blank or
    // whitespace-only code should never be submittable.
    renderWithProviders(<ConfirmNewEquipmentCard />);

    const unlockButton = screen.getByRole('button', { name: 'Unlock' });
    expect(unlockButton).toBeDisabled();

    await userEvent.type(screen.getByLabelText('Confirmation code'), 'test-confirmation-code');

    expect(unlockButton).toBeEnabled();
  });

  it('shows the empty-queue message once unlocked with no pending items', async () => {
    // EQ-WEB-05, UI-STATE testing: when the code is accepted and the list resolves
    // to an empty array, ConfirmNewEquipmentCard.tsx (lines 139-141) shows the
    // "Nothing is waiting" message instead of a table.
    mocks.items = [];
    renderWithProviders(<ConfirmNewEquipmentCard />);

    await unlockWith('test-confirmation-code');

    expect(
      await screen.findByText('Nothing is waiting. Every registered item has been dealt with.'),
    ).toBeInTheDocument();
  });

  it('confirms a pending item with the item id and the confirmation code header', async () => {
    // EQ-WEB-06, API-INTEGRATION testing: clicking "Confirm" on a row must call the
    // confirm mutation with the exact item id and the code the user typed, per
    // ConfirmNewEquipmentCard.tsx line 177: confirm.mutate({ path: { id: item.id }, headers }).
    mocks.items = [PENDING_ITEM];
    mocks.confirm.mockResolvedValue(PENDING_ITEM);
    renderWithProviders(<ConfirmNewEquipmentCard />);

    await unlockWith('test-confirmation-code');

    await userEvent.click(await screen.findByRole('button', { name: 'Confirm' }));

    await waitFor(() =>
      expect(mocks.confirm.mock.calls[0]?.[0]).toEqual({
        path: { id: 'item-1' },
        headers: { 'X-Confirmation-Code': 'test-confirmation-code' },
      }),
    );
  });

  it('opens a reject dialog naming the item, and rejects with the item id and code', async () => {
    // EQ-WEB-07, Component + API-INTEGRATION testing: clicking "Reject" on a row must
    // open RejectDialog showing that item's name (ConfirmNewEquipmentCard.tsx line 226,
    // title={`Reject ${item.name}?`}), and confirming it must call the reject mutation
    // with { path: { id: item.id }, headers: { [CODE_HEADER]: code } } (line 236).
    mocks.items = [PENDING_ITEM];
    mocks.reject.mockResolvedValue(undefined);
    renderWithProviders(<ConfirmNewEquipmentCard />);

    await unlockWith('test-confirmation-code');

    await userEvent.click(await screen.findByRole('button', { name: 'Reject' }));

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Reject Ventilator?')).toBeInTheDocument();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Reject' }));

    await waitFor(() =>
      expect(mocks.reject.mock.calls[0]?.[0]).toEqual({
        path: { id: 'item-1' },
        headers: { 'X-Confirmation-Code': 'test-confirmation-code' },
      }),
    );
  });

  it('shows the queue immediately for an equipment administrator, with no code-entry form', async () => {
    // The equipment administrator is a role created only for this confirming work, so the API
    // skips the confirmation-code check for them (EquipmentItemService.EnsureConfirmationCode).
    // The card should never show the "enter the confirmation code" form for this role.
    mocks.role = 'equipment_administrator';
    mocks.items = [PENDING_ITEM];
    renderWithProviders(<ConfirmNewEquipmentCard />);

    expect(screen.queryByLabelText('Confirmation code')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Unlock' })).not.toBeInTheDocument();
    expect(await screen.findByText('Ventilator')).toBeInTheDocument();
  });

  it('an equipment administrator confirms with an empty confirmation-code header', async () => {
    mocks.role = 'equipment_administrator';
    mocks.items = [PENDING_ITEM];
    mocks.confirm.mockResolvedValue(PENDING_ITEM);
    renderWithProviders(<ConfirmNewEquipmentCard />);

    await userEvent.click(await screen.findByRole('button', { name: 'Confirm' }));

    await waitFor(() =>
      expect(mocks.confirm.mock.calls[0]?.[0]).toEqual({
        path: { id: 'item-1' },
        headers: { 'X-Confirmation-Code': '' },
      }),
    );
  });
});
