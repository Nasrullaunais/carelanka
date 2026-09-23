import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../../test/api-mocks';
import { CrewManagement } from './crew-management';

const mocks = vi.hoisted(() => ({ unassign: vi.fn(), toastError: vi.fn() }));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: mocks.toastError } }));
vi.mock('../../../services/api/generated/@tanstack/react-query.gen', () => ({
  getCurrentAmbulanceCrewOptions: () => ({ queryKey: ['crew'], queryFn: () => Promise.resolve([{ staff_member_id: 'staff-1', full_name: 'N. Silva' }]) }),
  assignCurrentAmbulanceCrewMutation: () => ({ mutationFn: vi.fn() }),
  unassignCurrentAmbulanceCrewMutation: () => ({ mutationFn: mocks.unassign }),
}));

describe('CrewManagement', () => {
  afterEach(() => { cleanup(); vi.clearAllMocks(); });

  it('explains a live-run conflict without leaving the action pending', async () => {
    mocks.unassign.mockRejectedValue({ status: 409 });
    renderWithProviders(<CrewManagement ambulanceId="amb-1" registrationNumber="WP-CA-1234" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Unassign' }));

    await waitFor(() => expect(mocks.toastError).toHaveBeenCalledWith(expect.stringMatching(/locked while this ambulance has a live dispatch/i)));
    expect(screen.getByRole('button', { name: 'Unassign' })).toBeEnabled();
  });
});
