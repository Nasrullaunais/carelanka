import { useEffect, useState, type FormEvent } from 'react';
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
import type { CallPriority, CreateEmergencyCallRequest } from '../../../services/api/generated';
import { AppSelect } from '../../../components/ui/app-select';
import { priorityLabels } from '../domain';
import { emptySceneLocation, sceneAccuracy, sceneCoordinates, SceneLocationFields, type SceneLocation } from './scene-location-fields';

interface CallForm {
  callerName: string;
  callerPhone: string;
  patientIsCaller: boolean;
  details: string;
  priority: CallPriority;
  scene: SceneLocation;
}

const emptyForm: CallForm = {
  callerName: '',
  callerPhone: '',
  patientIsCaller: true,
  details: '',
  priority: 'high',
  scene: emptySceneLocation,
};

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
    const point = sceneCoordinates(form.scene);
    const accuracy = sceneAccuracy(form.scene);
    if (!point || accuracy === undefined) return;

    onSubmit({
      caller_name: optional(form.callerName),
      caller_phone: optional(form.callerPhone),
      patient_is_caller: form.patientIsCaller,
      details: optional(form.details),
      priority: form.priority,
      latitude: point.latitude,
      longitude: point.longitude,
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
                <AppSelect
                  label="Priority"
                  value={form.priority}
                  onValueChange={(priority) => setForm({ ...form, priority: priority as CallPriority })}
                  options={Object.entries(priorityLabels).map(([value, label]) => ({ value, label }))}
                />
                <SceneLocationFields value={form.scene} onChange={(scene) => setForm((current) => ({ ...current, scene }))} />
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

function FormField({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <TextField value={value} onChange={onChange}>
      <Label>{label}</Label>
      <InputGroup><InputGroupInput /></InputGroup>
    </TextField>
  );
}

function optional(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}
