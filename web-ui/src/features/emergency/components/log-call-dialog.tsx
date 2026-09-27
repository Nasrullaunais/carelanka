import { lazy, Suspense, useEffect, useState, type FormEvent } from 'react';
import {
  Button,
  Checkbox,
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
import type { CreateEmergencyCallRequest } from '../../../services/api/generated';

interface CallForm {
  callerName: string;
  callerPhone: string;
  patientIsCaller: boolean;
  details: string;
  latitude: string;
  longitude: string;
  accuracy: string;
}

const emptyForm: CallForm = {
  callerName: '',
  callerPhone: '',
  patientIsCaller: true,
  details: '',
  latitude: '',
  longitude: '',
  accuracy: '',
};

const LocationPicker = lazy(() => import('./location-picker').then((module) => ({ default: module.LocationPicker })));

export function LogCallDialog({ isOpen, isPending, onOpenChange, onSubmit }: {
  isOpen: boolean;
  isPending: boolean;
  onOpenChange: (open: boolean) => void;
  onSubmit: (request: CreateEmergencyCallRequest) => void;
}) {
  const [form, setForm] = useState<CallForm>(emptyForm);
  const [idempotencyKey, setIdempotencyKey] = useState('');

  useEffect(() => {
    if (isOpen) {
      setForm(emptyForm);
      setIdempotencyKey(crypto.randomUUID());
    }
  }, [isOpen]);

  function submit(event: FormEvent) {
    event.preventDefault();
    const latitude = Number(form.latitude);
    const longitude = Number(form.longitude);
    const accuracy = Number(form.accuracy);
    if (!Number.isFinite(latitude) || latitude < -90 || latitude > 90
      || !Number.isFinite(longitude) || longitude < -180 || longitude > 180
      || !Number.isFinite(accuracy) || accuracy < 0) return;

    onSubmit({
      caller_name: optional(form.callerName),
      caller_phone: optional(form.callerPhone),
      patient_is_caller: form.patientIsCaller,
      details: optional(form.details),
      latitude,
      longitude,
      location_accuracy_metres: accuracy,
      location_captured_at: new Date().toISOString(),
      idempotency_key: idempotencyKey,
    });
  }

  return (
    <Modal isOpen={isOpen} onOpenChange={onOpenChange}>
      <ModalBackdrop>
        <ModalContainer>
          <ModalDialog>
            <form onSubmit={submit}>
              <ModalHeader><ModalHeading>Log emergency call</ModalHeading></ModalHeader>
              <ModalBody className="flex flex-col gap-3">
                <FormField label="Caller name" value={form.callerName} onChange={(callerName) => setForm({ ...form, callerName })} />
                <FormField label="Caller phone" value={form.callerPhone} onChange={(callerPhone) => setForm({ ...form, callerPhone })} />
                <Checkbox isSelected={form.patientIsCaller} onChange={(selected) => setForm({ ...form, patientIsCaller: selected })}>
                  <Checkbox.Control><Checkbox.Indicator>✓</Checkbox.Indicator></Checkbox.Control>
                  <Checkbox.Content>The caller is the patient</Checkbox.Content>
                </Checkbox>
                <TextField value={form.details} onChange={(details) => setForm({ ...form, details })}>
                  <Label>Emergency details</Label>
                  <InputGroup><InputGroupTextArea rows={3} required /></InputGroup>
                </TextField>
                <Suspense fallback={<div className="h-64 animate-pulse rounded-xl bg-default-100" aria-label="Loading location map" />}>
                  <LocationPicker
                    value={coordinates(form)}
                    onChange={({ latitude, longitude }) => setForm({ ...form, latitude: String(latitude), longitude: String(longitude) })}
                  />
                </Suspense>
                <p className="text-sm text-muted">Choose the scene on the map, or enter coordinates below when map tiles are unavailable.</p>
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                  <FormField label="Latitude" type="number" min={-90} max={90} value={form.latitude} onChange={(latitude) => setForm({ ...form, latitude })} required />
                  <FormField label="Longitude" type="number" min={-180} max={180} value={form.longitude} onChange={(longitude) => setForm({ ...form, longitude })} required />
                  <FormField label="Accuracy (m)" type="number" min={0} value={form.accuracy} onChange={(accuracy) => setForm({ ...form, accuracy })} required />
                </div>
              </ModalBody>
              <ModalFooter>
                <Button type="button" variant="outline" isDisabled={isPending} onPress={() => onOpenChange(false)}>Cancel</Button>
                <Button type="submit" isDisabled={isPending || idempotencyKey === ''}>{isPending ? 'Logging…' : 'Log call'}</Button>
              </ModalFooter>
            </form>
          </ModalDialog>
        </ModalContainer>
      </ModalBackdrop>
    </Modal>
  );
}

function FormField({ label, value, type = 'text', min, max, required, onChange }: {
  label: string;
  value: string;
  type?: 'text' | 'number';
  min?: number;
  max?: number;
  required?: boolean;
  onChange: (value: string) => void;
}) {
  return (
    <TextField value={value} onChange={onChange}>
      <Label>{label}</Label>
      <InputGroup><InputGroupInput type={type} step={type === 'number' ? 'any' : undefined} min={min} max={max} required={required} /></InputGroup>
    </TextField>
  );
}

function optional(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}

function coordinates(form: CallForm) {
  const latitude = Number(form.latitude);
  const longitude = Number(form.longitude);
  return Number.isFinite(latitude) && Number.isFinite(longitude) && form.latitude !== '' && form.longitude !== ''
    ? { latitude, longitude }
    : undefined;
}
