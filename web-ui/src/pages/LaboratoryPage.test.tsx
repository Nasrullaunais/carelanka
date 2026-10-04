import { cleanup, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../test/api-mocks';
import { LaboratoryPage } from './LaboratoryPage';

const mocks = vi.hoisted(() => ({
  upload: vi.fn(),
  patients: [] as unknown[],
  reports: [] as unknown[],
  role: 'equipment_manager' as string | undefined,
}));

const EMPTY_PAGED = { items: [], page: 1, page_size: 10, total_items: 0, total_pages: 1 };

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('../services/auth/useSession', () => ({
  useSession: () => (mocks.role ? { principal: { role: mocks.role } } : null),
}));
vi.mock('../services/api/generated/@tanstack/react-query.gen', () => ({
  // The ward browser at the top of the page - not under test here, kept empty.
  listWardPatientsOptions: () => ({ queryKey: ['ward-patients'], queryFn: () => Promise.resolve(EMPTY_PAGED) }),
  listWardsOptions: () => ({ queryKey: ['wards'], queryFn: () => Promise.resolve([]) }),
  listPatientsOptions: () => ({
    queryKey: ['patients'],
    queryFn: () =>
      Promise.resolve({ items: mocks.patients, page: 1, page_size: 10, total_items: mocks.patients.length, total_pages: 1 }),
  }),
  listLabReportsOptions: () => ({
    queryKey: ['lab-reports'],
    queryFn: () =>
      Promise.resolve({ items: mocks.reports, page: 1, page_size: 10, total_items: mocks.reports.length, total_pages: 1 }),
  }),
  uploadLabReportMutation: () => ({ mutationFn: mocks.upload }),
}));

const PATIENT = {
  id: 'patient-1',
  patient_code: 'P7K2X9QM',
  full_name: 'Kasun Mendiz',
  nic: '927654321V',
  gender: 'male',
  has_account: true,
};

function smallFile(name = 'result.pdf') {
  return new File(['x'.repeat(10)], name, { type: 'application/pdf' });
}

function tooBigFile() {
  // 10 MB is the limit (MAX_BYTES); one byte over it must be rejected client-side.
  return new File([new Uint8Array(10 * 1024 * 1024 + 1)], 'result.pdf', { type: 'application/pdf' });
}

async function selectPatientBySearch() {
  await userEvent.type(screen.getByLabelText('Patient code, name or NIC'), 'p7');
  await userEvent.click(await screen.findByRole('button', { name: 'Open' }));
}

describe('LaboratoryPage', () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
    mocks.patients = [];
    mocks.reports = [];
    mocks.role = 'equipment_manager';
  });

  it('finds a patient by search and opens their report list and upload card', async () => {
    // API-INTEGRATION + UI-STATE testing: typing 2+ characters enables the patient search
        // (line 89, `enabled: search.trim().length >= 2`), and selecting a result shows that
    // patient's results and, for a role that may file, the upload card.
    mocks.patients = [PATIENT];
    renderWithProviders(<LaboratoryPage />);

    await selectPatientBySearch();

    expect(await screen.findByText('Results for Kasun Mendiz (P7K2X9QM)')).toBeInTheDocument();
    expect(screen.getByText('File a result for Kasun Mendiz')).toBeInTheDocument();
  });

  it('keeps the upload card hidden for a role that may not file a report', async () => {
    // AUTHORIZATION testing: canFileLabReport (types/permissions.ts) only allows the
    // equipment manager - every other role can read results but not add one.
    mocks.role = 'doctor';
    mocks.patients = [PATIENT];
    renderWithProviders(<LaboratoryPage />);

    await selectPatientBySearch();

    expect(await screen.findByText('Results for Kasun Mendiz (P7K2X9QM)')).toBeInTheDocument();
    expect(screen.queryByText('File a result for Kasun Mendiz')).not.toBeInTheDocument();
  });

  it('keeps File the result disabled until a test name and a file are both given', async () => {
    // FORM-VALIDATION testing: `ready` (line 285) requires testName.trim().length >= 2 AND
    // a chosen file AND the file not being too big.
    mocks.patients = [PATIENT];
    renderWithProviders(<LaboratoryPage />);
    await selectPatientBySearch();
    await screen.findByText('File a result for Kasun Mendiz');

    const submit = screen.getByRole('button', { name: 'File the result' });
    expect(submit).toBeDisabled();

    await userEvent.type(screen.getByLabelText('What was tested'), 'Full blood count');
    expect(submit).toBeDisabled();

    await userEvent.upload(screen.getByLabelText(/The report/), smallFile());
    expect(submit).toBeEnabled();
  });

  it('refuses a file over 10 MB and keeps the submit button disabled', async () => {
    // BOUNDARY / FORM-VALIDATION testing: MAX_BYTES is enforced client-side (line 338)
    // before the request is ever sent, so a slow upload is not how the limit is found.
    mocks.patients = [PATIENT];
    renderWithProviders(<LaboratoryPage />);
    await selectPatientBySearch();
    await screen.findByText('File a result for Kasun Mendiz');

    await userEvent.type(screen.getByLabelText('What was tested'), 'Full blood count');
    await userEvent.upload(screen.getByLabelText(/The report/), tooBigFile());

    expect(
      screen.getByText('That file is larger than 10 MB. Scan it again at a lower resolution.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'File the result' })).toBeDisabled();
  });

  it('uploads the result with the patient id, test name, summary and file', async () => {
    // API-INTEGRATION testing: submitting calls the mutation with a body of exactly
    // { PatientId, TestName, Summary, File } (lines 301-308) - Summary omitted when blank.
    mocks.patients = [PATIENT];
    mocks.upload.mockResolvedValue(undefined);
    renderWithProviders(<LaboratoryPage />);
    await selectPatientBySearch();
    await screen.findByText('File a result for Kasun Mendiz');

    await userEvent.type(screen.getByLabelText('What was tested'), 'Full blood count');
    await userEvent.type(screen.getByLabelText('Summary (optional)'), 'Haemoglobin low at 9.1 g/dL.');
    const file = smallFile();
    await userEvent.upload(screen.getByLabelText(/The report/), file);

    await userEvent.click(screen.getByRole('button', { name: 'File the result' }));

    await waitFor(() => {
      const body = mocks.upload.mock.calls[0]?.[0]?.body;
      expect(body.PatientId).toBe('patient-1');
      expect(body.TestName).toBe('Full blood count');
      expect(body.Summary).toBe('Haemoglobin low at 9.1 g/dL.');
      expect(body.File).toBe(file);
    });
  });
});
