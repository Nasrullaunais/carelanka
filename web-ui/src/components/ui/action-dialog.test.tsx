import { useState } from 'react';
import { cleanup, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';
import { renderWithProviders } from '../../test/api-mocks';
import { ActionDialog } from './action-dialog';

function Example() {
  const [open, setOpen] = useState(false);
  return <>
    <button onClick={() => setOpen(true)}>Manage crew</button>
    <ActionDialog title="Manage crew" isOpen={open} onClose={() => setOpen(false)}>
      <button>Assign crew member</button>
    </ActionDialog>
  </>;
}

describe('ActionDialog', () => {
  afterEach(cleanup);

  it('opens the action in a dialog and closes with Escape', async () => {
    const user = userEvent.setup();
    renderWithProviders(<Example />);

    await user.click(screen.getByRole('button', { name: 'Manage crew' }));
    expect(screen.getByRole('dialog', { name: 'Manage crew' })).toBeVisible();
    expect(screen.getByRole('button', { name: 'Assign crew member' })).toBeVisible();

    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog', { name: 'Manage crew' })).not.toBeInTheDocument();
  });
});
