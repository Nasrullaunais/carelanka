import { cleanup, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, it, vi } from 'vitest';
import { pagedResult, renderWithProviders } from '../test/api-mocks';
import { destinationsFor } from '../types/navigation';
import { PatientAppAccountsPage } from './PatientAppAccountsPage';

const reset = vi.hoisted(() => vi.fn());

vi.mock('../services/auth/useSession', () => ({ useSession: () => ({ principal: { role: 'general_staff' } }) }));
vi.mock('../services/api/generated/@tanstack/react-query.gen', async (importOriginal) => ({
  ...await importOriginal<object>(),
  listPatientAppAccountsOptions: () => ({
    queryKey: ['patient-app-accounts'],
    queryFn: async () => pagedResult([{
      patient_id: 'patient-1',
      patient_code: 'P7K2X9QM',
      full_name: 'Kamala Perera',
      nic: '199012345678',
      date_of_birth: '1990-04-12',
      username: 'kamala.p',
    }]),
  }),
  resetPatientAppPasswordMutation: () => ({
    mutationFn: async (variables: unknown) => {
      reset(variables);
      return { username: 'kamala.p', temporary_password: 'KQTM-4821' };
    },
  }),
}));

afterEach(cleanup);

it('shows the five fields staff check before resetting a password', async () => {
  renderWithProviders(<PatientAppAccountsPage />);

  const row = (await screen.findByText('Kamala Perera')).closest('tr')!;
  expect(within(row).getByText('199012345678')).toBeInTheDocument();
  expect(within(row).getByText('1990-04-12')).toBeInTheDocument();
  expect(within(row).getByText('P7K2X9QM')).toBeInTheDocument();
  expect(within(row).getByText('kamala.p')).toBeInTheDocument();
});

it('reveals the temporary password only after the reset is confirmed', async () => {
  renderWithProviders(<PatientAppAccountsPage />);

  await userEvent.click(await screen.findByRole('button', { name: 'Generate new password' }));
  expect(await screen.findByText(/Their current password stops working straight away/)).toBeInTheDocument();
  expect(screen.queryByText('KQTM-4821')).not.toBeInTheDocument();

  const dialog = screen.getByRole('alertdialog');
  await userEvent.click(within(dialog).getByRole('button', { name: 'Generate new password' }));

  expect(await screen.findByText('KQTM-4821')).toBeInTheDocument();
  expect(screen.getByText('This is shown once. Give it to the patient now.')).toBeInTheDocument();
  expect(reset).toHaveBeenCalledWith(expect.objectContaining({ path: { patientId: 'patient-1' } }));
});

it('is hidden from the ward nurse and the doctor', () => {
  for (const role of ['ward_nurse', 'doctor'] as const) {
    expect(destinationsFor(role).map(({ to }) => to)).not.toContain('/patient-accounts');
  }
  for (const role of ['general_staff', 'duty_manager', 'hospital_administrator'] as const) {
    expect(destinationsFor(role).map(({ to }) => to)).toContain('/patient-accounts');
  }
});
