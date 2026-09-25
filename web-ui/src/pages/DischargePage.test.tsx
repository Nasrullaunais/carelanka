import { cleanup, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../test/api-mocks';
import { DischargePage } from './DischargePage';

vi.mock('../services/auth/useSession', () => ({ useSession: () => ({ principal: { role: 'duty_manager' } }) }));
vi.mock('../services/api/generated/@tanstack/react-query.gen', async (importOriginal) => ({
  ...await importOriginal<object>(),
  listDischargeCandidatesOptions: ({ query }: { query: { page: number } }) => ({
    queryKey: ['discharges', query.page],
    queryFn: async () => ({
      items: [{ admission_id: `admission-${query.page}`, patient: { full_name: `Patient ${query.page}`, patient_code: 'P001' },
        admission_category: 'general', is_discharged: false, days_in_bed: 3, outstanding_items: ['clinical_clearance', 'billing_settled'] }],
      page: query.page, total_pages: 2, total_items: 26,
    }),
  }),
}));

afterEach(cleanup);

it('labels outstanding checks as pending and makes later admissions reachable inside the table wrapper', async () => {
  renderWithProviders(<DischargePage />);
  expect(await screen.findByText('Patient 1')).toBeInTheDocument();
  expect(screen.getByText('Awaiting doctor clearance')).toBeInTheDocument();
  expect(screen.getByText('Awaiting payment')).toBeInTheDocument();
  const next = screen.getByRole('button', { name: 'Next' });
  expect(next.closest('.table-frame')).not.toBeNull();
  await userEvent.click(next);
  expect(await screen.findByText('Patient 2')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
});
