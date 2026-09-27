import { Table } from './Table';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  listWardPatientsOptions,
  listWardsOptions,
} from '../services/api/generated/@tanstack/react-query.gen';
import type { WardPatient } from '../services/api/generated';
import { AppSelect } from './ui/app-select';

const PAGE_SIZE = 10;

export type WardPatientPickerProps = {
  selectedAdmissionId?: string;
  onSelect: (patient: WardPatient) => void;
  /** Hides anybody holding no bed. Equipment is assigned to a visit that is in a bed. */
  bedOnly?: boolean;
  emptyMessage?: string;
};

// Shared by the laboratory desk and the equipment assign dialog: both need to offer a patient
// to pick rather than an id to type.
export function WardPatientPicker({
  selectedAdmissionId,
  onSelect,
  bedOnly = false,
  emptyMessage,
}: WardPatientPickerProps) {
  const [wardName, setWardName] = useState('');
  const [page, setPage] = useState(1);

  const wards = useQuery(listWardsOptions({ query: { isActive: true } }));

  const patients = useQuery(
    listWardPatientsOptions({
      query: {
        ...(wardName.length > 0 ? { wardName } : {}),
        page,
        pageSize: PAGE_SIZE,
      },
    }),
  );

  const rows = (patients.data?.items ?? []).filter(
    (patient) => !bedOnly || (patient.ward_name !== null && patient.ward_name !== undefined),
  );
  const totalPages = patients.data?.total_pages ?? 1;

  return (
    <>
      <div className="field">
        <AppSelect
          id="ward-patient-ward"
          label="Ward"
          value={wardName}
          onValueChange={(value) => {
            setWardName(value);
            setPage(1);
          }}
          options={[
            { value: '', label: 'Every ward' },
            ...(wards.data ?? []).map((ward) => ({ value: ward.name, label: ward.name })),
          ]}
        />
      </div>

      {patients.isPending && <p className="muted">Loading patients…</p>}

      {patients.isError && (
        <p className="empty">
          Could not load the ward list.{' '}
          <button type="button" className="secondary" onClick={() => void patients.refetch()}>
            Try again
          </button>
        </p>
      )}

      {patients.isSuccess && rows.length === 0 && (
        <p className="empty">
          {emptyMessage ??
            (wardName.length > 0
              ? `Nobody is in ${wardName} at the moment.`
              : 'Nobody is currently admitted.')}
        </p>
      )}

      {rows.length > 0 && (
        <Table>
          <thead>
            <tr>
              <th>Ward</th>
              <th>Bed</th>
              <th>Code</th>
              <th>Name</th>
              <th>Visit</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((patient) => (
              <tr key={patient.admission_id}>
                <td>{patient.ward_name ?? <span className="muted">No bed</span>}</td>
                <td>{patient.bed_number ?? <span className="muted">—</span>}</td>
                <td>{patient.patient_code}</td>
                <td>{patient.full_name}</td>
                <td>{visitLabel(patient)}</td>
                <td>
                  <button type="button" onClick={() => onSelect(patient)}>
                    {selectedAdmissionId === patient.admission_id ? 'Selected' : 'Choose'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      {totalPages > 1 && (
        <div className="pager">
          <button
            type="button"
            className="secondary"
            disabled={page <= 1}
            onClick={() => setPage((current) => current - 1)}
          >
            Previous
          </button>
          <span className="muted">
            Page {page} of {totalPages}
          </span>
          <button
            type="button"
            className="secondary"
            disabled={page >= totalPages}
            onClick={() => setPage((current) => current + 1)}
          >
            Next
          </button>
        </div>
      )}
    </>
  );
}

function visitLabel(patient: WardPatient): string {
  switch (patient.admission_status) {
    case 'admitted':
      return 'In a bed';
    case 'awaiting_bed':
      return 'Waiting for a bed';
    case 'awaiting_approval':
      return 'Bed awaiting approval';
    case 'bed_reserved':
      return 'Bed held';
    case 'ready_for_discharge':
      return 'Going home';
    default:
      return patient.admission_status;
  }
}
