import { useEffect, useState, type FormEvent } from 'react';
import {
  Button,
  InputGroup,
  InputGroupInput,
  InputGroupTextArea,
  Label,
  Modal,
  ModalBackdrop,
  ModalBody,
  ModalContainer,
  ModalDialog,
  ModalFooter,
  ModalHeader,
  ModalHeading,
  TextField,
} from '@heroui/react';
import type { AmbulanceDetail, AmbulanceStatus, CreateAmbulanceRequest, UpdateAmbulanceRequest } from '../../../services/api/generated';
import { ambulanceStatusLabels } from '../domain';

const editableStatuses: AmbulanceStatus[] = ['available', 'out_of_service'];

export function AmbulanceDialog({ isOpen, ambulance, isPending, onOpenChange, onCreate, onUpdate }: {
  isOpen: boolean;
  ambulance?: AmbulanceDetail;
  isPending: boolean;
  onOpenChange: (open: boolean) => void;
  onCreate: (body: CreateAmbulanceRequest) => void;
  onUpdate: (body: UpdateAmbulanceRequest) => void;
}) {
  const [registrationNumber, setRegistrationNumber] = useState('');
  const [status, setStatus] = useState<AmbulanceStatus>('available');
  const [reason, setReason] = useState('');

  useEffect(() => {
    if (!isOpen) return;
    setRegistrationNumber(ambulance?.registration_number ?? '');
    setStatus(ambulance?.status ?? 'available');
    setReason(ambulance?.out_of_service_reason ?? '');
  }, [ambulance, isOpen]);

  function submit(event: FormEvent) {
    event.preventDefault();
    const registration = registrationNumber.trim();
    if (!registration || (status === 'out_of_service' && !reason.trim())) return;
    if (ambulance) {
      onUpdate({
        registration_number: registration,
        status: status === ambulance.status ? undefined : status,
        out_of_service_reason: status === 'out_of_service' ? reason.trim() : null,
      });
    } else {
      onCreate({ registration_number: registration });
    }
  }

  return (
    <Modal isOpen={isOpen} onOpenChange={onOpenChange}>
      <ModalBackdrop><ModalContainer><ModalDialog><form onSubmit={submit}>
        <ModalHeader><ModalHeading>{ambulance ? 'Edit ambulance' : 'Add ambulance'}</ModalHeading></ModalHeader>
        <ModalBody className="flex flex-col gap-3">
          <TextField value={registrationNumber} onChange={setRegistrationNumber}>
            <Label>Registration number</Label>
            <InputGroup><InputGroupInput required /></InputGroup>
          </TextField>
          {ambulance && (
            <label className="flex flex-col gap-1 text-sm">
              Status
              <select className="rounded-lg border border-default-200 bg-background px-3 py-2" value={status} onChange={(event) => setStatus(event.target.value as AmbulanceStatus)}>
                {!editableStatuses.includes(ambulance.status) && <option value={ambulance.status}>{ambulanceStatusLabels[ambulance.status]} (current)</option>}
                {editableStatuses.map((value) => <option key={value} value={value}>{ambulanceStatusLabels[value]}</option>)}
              </select>
            </label>
          )}
          {ambulance && status === 'out_of_service' && (
            <TextField value={reason} onChange={setReason}>
              <Label>Out-of-service reason</Label>
              <InputGroup><InputGroupTextArea required rows={3} /></InputGroup>
            </TextField>
          )}
        </ModalBody>
        <ModalFooter>
          <Button type="button" variant="outline" isDisabled={isPending} onPress={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" isDisabled={isPending}>{isPending ? 'Saving…' : 'Save'}</Button>
        </ModalFooter>
      </form></ModalDialog></ModalContainer></ModalBackdrop>
    </Modal>
  );
}
