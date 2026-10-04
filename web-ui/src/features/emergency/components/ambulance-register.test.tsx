import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../../test/api-mocks';
import { AmbulanceRegister } from './ambulance-register';

const mocks = vi.hoisted(() => ({ create: vi.fn(), retire: vi.fn(), items: [] as Array<Record<string, unknown>> }));

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('../../../services/api/generated/@tanstack/react-query.gen', () => ({
  listAmbulancesOptions: () => ({ queryKey: ['ambulances'], queryFn: () => Promise.resolve({ items: mocks.items, page: 1, page_size: 100, total_items: mocks.items.length, total_pages: 1 }) }),
  getAmbulanceOptions: ({ path }: { path: { id: string } }) => ({ queryKey: ['ambulance', path.id], queryFn: () => Promise.resolve(undefined) }),
  createAmbulanceMutation: () => ({ mutationFn: mocks.create }),
  updateAmbulanceMutation: () => ({ mutationFn: vi.fn() }),
  retireAmbulanceMutation: () => ({ mutationFn: mocks.retire }),
  reinstateAmbulanceMutation: () => ({ mutationFn: vi.fn() }),
  getCurrentAmbulanceCrewOptions: () => ({ queryKey: ['crew'], queryFn: () => Promise.resolve([]) }),
  assignCurrentAmbulanceCrewMutation: () => ({ mutationFn: vi.fn() }),
  unassignCurrentAmbulanceCrewMutation: () => ({ mutationFn: vi.fn() }),
}));

describe('AmbulanceRegister', () => {
  afterEach(() => { cleanup(); vi.clearAllMocks(); mocks.items = []; });

  it('adds an ambulance with its required registration number', async () => {
    mocks.create.mockResolvedValue({ id: 'amb-1' });
    renderWithProviders(<AmbulanceRegister />);

    await userEvent.click(await screen.findByRole('button', { name: 'Add ambulance' }));
    await userEvent.type(screen.getByLabelText('Registration number'), ' WP-CA-1234 ');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(mocks.create.mock.calls[0]?.[0]).toEqual({ body: { registration_number: 'WP-CA-1234' } }));
  });

  it('retires an ambulance only after a reason is given', async () => {
    mocks.items = [{ id: 'amb-1', registration_number: 'WP-CA-1234', status: 'available', is_active: true, is_divertible: true }];
    mocks.retire.mockResolvedValue(undefined);
    renderWithProviders(<AmbulanceRegister />);

    await userEvent.click(await screen.findByRole('button', { name: 'Retire' }));
    const confirm = screen.getByRole('button', { name: 'Retire ambulance' });
    expect(confirm).toBeDisabled();
    await userEvent.type(screen.getByLabelText('Reason'), 'Written off after an accident');
    await userEvent.click(confirm);

    await waitFor(() => expect(mocks.retire.mock.calls[0]?.[0]).toEqual({ path: { id: 'amb-1' }, body: { reason: 'Written off after an accident' } }));
  });

  it('does not offer Retire while the ambulance is on a run', async () => {
    mocks.items = [{ id: 'amb-1', registration_number: 'WP-CA-1234', status: 'en_route', is_active: true, is_divertible: true, active_dispatch_id: 'd-1' }];
    renderWithProviders(<AmbulanceRegister />);

    expect(await screen.findByText('WP-CA-1234')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Retire' })).not.toBeInTheDocument();
  });
});
