import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../test/api-mocks';
import { RemoveMedicineDialog } from './RemoveMedicineDialog';

const mocks = vi.hoisted(() => ({
  remove: vi.fn(),
}));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('../../services/api/generated/@tanstack/react-query.gen', () => ({
  removePharmacyItemMutation: () => ({ mutationFn: mocks.remove }),
}));

const ITEM = {
  id: 'item-1',
  name: 'Amoxicillin 250mg',
  category_id: 'cat-1',
  category_name: 'Antibiotics',
  unit: 'tablet',
  batch_count: 0,
  quantity_on_hand: 0,
  reorder_threshold: 20,
  is_available: false,
  below_threshold: true,
  created_at: '2026-01-01T00:00:00Z',
  updated_at: '2026-01-01T00:00:00Z',
};

describe('RemoveMedicineDialog', () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it('names the medicine in the dialog title and keeps Remove it disabled until a code is typed', () => {
    // FORM-VALIDATION testing: the submit button stays disabled while
    // `code.trim().length === 0` (line 57), and the dialog title names this item
    // specifically (line 34), not a generic confirmation.
    renderWithProviders(<RemoveMedicineDialog item={ITEM} onClose={vi.fn()} onDone={vi.fn()} />);

    expect(screen.getByText('Remove Amoxicillin 250mg from the register?')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remove it' })).toBeDisabled();
  });

  it('enables Remove it once a code is typed', async () => {
    renderWithProviders(<RemoveMedicineDialog item={ITEM} onClose={vi.fn()} onDone={vi.fn()} />);

    await userEvent.type(screen.getByLabelText('Confirmation code'), 'test-confirmation-code');

    expect(screen.getByRole('button', { name: 'Remove it' })).toBeEnabled();
  });

  it('removes the item with its id and the typed confirmation code', async () => {
    // API-INTEGRATION testing: submitting must call the remove mutation with
    // { path: { id: item.id }, headers: { 'X-Confirmation-Code': code } } (line 44).
    mocks.remove.mockResolvedValue(undefined);
    const onDone = vi.fn();
    renderWithProviders(<RemoveMedicineDialog item={ITEM} onClose={vi.fn()} onDone={onDone} />);

    await userEvent.type(screen.getByLabelText('Confirmation code'), 'test-confirmation-code');
    await userEvent.click(screen.getByRole('button', { name: 'Remove it' }));

    await waitFor(() =>
      expect(mocks.remove.mock.calls[0]?.[0]).toEqual({
        path: { id: 'item-1' },
        headers: { 'X-Confirmation-Code': 'test-confirmation-code' },
      }),
    );
    await waitFor(() => expect(onDone).toHaveBeenCalled());
  });

  it('clears the typed code when the server rejects it, rather than leaving a stale value', async () => {
    // UI-STATE / failure-handling testing: onError resets the code field (line 29), so a
    // wrong code never sits there looking like it might still be valid.
    mocks.remove.mockRejectedValue(new Error('wrong code'));
    renderWithProviders(<RemoveMedicineDialog item={ITEM} onClose={vi.fn()} onDone={vi.fn()} />);

    const input = screen.getByLabelText('Confirmation code') as HTMLInputElement;
    await userEvent.type(input, 'wrong-code');
    await userEvent.click(screen.getByRole('button', { name: 'Remove it' }));

    await waitFor(() => expect(input.value).toBe(''));
  });
});
