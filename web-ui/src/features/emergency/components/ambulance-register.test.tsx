import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../../test/api-mocks';
import { AmbulanceRegister } from './ambulance-register';

const mocks = vi.hoisted(() => ({ create: vi.fn() }));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('../../../services/api/generated/@tanstack/react-query.gen', () => ({
  listAmbulancesOptions: () => ({ queryKey: ['ambulances'], queryFn: () => Promise.resolve({ items: [], page: 1, page_size: 100, total_items: 0, total_pages: 1 }) }),
  getAmbulanceOptions: ({ path }: { path: { id: string } }) => ({ queryKey: ['ambulance', path.id], queryFn: () => Promise.resolve(undefined) }),
  createAmbulanceMutation: () => ({ mutationFn: mocks.create }),
  updateAmbulanceMutation: () => ({ mutationFn: vi.fn() }),
  retireAmbulanceMutation: () => ({ mutationFn: vi.fn() }),
  reinstateAmbulanceMutation: () => ({ mutationFn: vi.fn() }),
  getCurrentAmbulanceCrewOptions: () => ({ queryKey: ['crew'], queryFn: () => Promise.resolve([]) }),
  assignCurrentAmbulanceCrewMutation: () => ({ mutationFn: vi.fn() }),
  unassignCurrentAmbulanceCrewMutation: () => ({ mutationFn: vi.fn() }),
}));

describe('AmbulanceRegister', () => {
  afterEach(() => { cleanup(); vi.clearAllMocks(); });

  it('adds an ambulance with its required registration number', async () => {
    mocks.create.mockResolvedValue({ id: 'amb-1' });
    renderWithProviders(<AmbulanceRegister />);

    await userEvent.click(await screen.findByRole('button', { name: 'Add ambulance' }));
    await userEvent.type(screen.getByLabelText('Registration number'), ' WP-CA-1234 ');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(mocks.create.mock.calls[0]?.[0]).toEqual({ body: { registration_number: 'WP-CA-1234' } }));
  });
});
