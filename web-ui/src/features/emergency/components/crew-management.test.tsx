import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../../test/api-mocks';
import { CrewManagement } from './crew-management';

const mocks = vi.hoisted(() => ({ assign: vi.fn(), unassign: vi.fn(), toastError: vi.fn() }));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: mocks.toastError } }));
vi.mock('../../../services/api/generated/@tanstack/react-query.gen', () => ({
  getCurrentAmbulanceCrewOptions: () => ({ queryKey: ['crew'], queryFn: () => Promise.resolve([{ staff_member_id: 'staff-1', full_name: 'N. Silva' }]) }),
  searchAvailableCrewOptions: () => ({ queryKey: ['candidates'], queryFn: () => Promise.resolve([
    { staff_member_id: 'staff-2', full_name: 'A. Perera' },
    { staff_member_id: 'staff-3', full_name: 'M. Fernando' },
  ]) }),
  assignCurrentAmbulanceCrewMutation: () => ({ mutationFn: mocks.assign }),
  unassignCurrentAmbulanceCrewMutation: () => ({ mutationFn: mocks.unassign }),
}));

describe('CrewManagement', () => {
  afterEach(() => { cleanup(); vi.clearAllMocks(); });

  it('selects multiple crew members by name and assigns each one', async () => {
    mocks.assign.mockResolvedValue({});
    renderWithProviders(<CrewManagement ambulanceId="amb-1" registrationNumber="WP-CA-1234" />);

    expect(screen.queryByPlaceholderText('Staff UUID')).not.toBeInTheDocument();
    await userEvent.click(await screen.findByRole('button', { name: 'A. Perera' }));
    await userEvent.click(screen.getByRole('button', { name: 'M. Fernando' }));
    expect(screen.getByRole('button', { name: 'Remove A. Perera' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Assign 2 crew members' }));

    await waitFor(() => expect(mocks.assign).toHaveBeenCalledTimes(2));
    expect(mocks.assign.mock.calls.map(([request]) => request.body.staff_member_id)).toEqual(['staff-2', 'staff-3']);
  });

  it('keeps failed selections for retry after a partial assignment', async () => {
    mocks.assign.mockResolvedValueOnce({}).mockRejectedValueOnce({ status: 409 });
    renderWithProviders(<CrewManagement ambulanceId="amb-1" registrationNumber="WP-CA-1234" />);

    await userEvent.click(await screen.findByRole('button', { name: 'A. Perera' }));
    await userEvent.click(screen.getByRole('button', { name: 'M. Fernando' }));
    await userEvent.click(screen.getByRole('button', { name: 'Assign 2 crew members' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Failed selections remain selected for retry');
    expect(screen.queryByRole('button', { name: 'Remove A. Perera' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Remove M. Fernando' })).toBeInTheDocument();
  });

  it('explains a live-run conflict without leaving the action pending', async () => {
    mocks.unassign.mockRejectedValue({ status: 409 });
    renderWithProviders(<CrewManagement ambulanceId="amb-1" registrationNumber="WP-CA-1234" />);

    await userEvent.click(await screen.findByRole('button', { name: 'Unassign' }));

    await waitFor(() => expect(mocks.toastError).toHaveBeenCalledWith(expect.stringMatching(/locked while this ambulance has a live dispatch/i)));
    expect(screen.getByRole('button', { name: 'Unassign' })).toBeEnabled();
  });
});
