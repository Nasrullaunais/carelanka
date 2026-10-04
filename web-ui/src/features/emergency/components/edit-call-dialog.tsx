import { useState, type FormEvent } from 'react';
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
import type { EmergencyCallDetail, UpdateEmergencyCallRequest } from '../../../services/api/generated';
import { sceneAccuracy, sceneCoordinates, SceneLocationFields, type SceneLocation } from './scene-location-fields';

interface EditForm {
  callerName: string;
  callerPhone: string;
  details: string;
  scene: SceneLocation;
}

export function EditCallDialog({ call, isOpen, isPending, onOpenChange, onSave }: {
  call: EmergencyCallDetail;
  isOpen: boolean;
  isPending: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: (request: UpdateEmergencyCallRequest) => void;
}) {
  // Filled once when mounted, so a background refresh does not wipe what is being typed.
  const [form, setForm] = useState<EditForm>(() => formFor(call));

  const changes = changesFrom(call, form);
  const sceneInvalid = sceneCoordinates(form.scene) === undefined || sceneAccuracy(form.scene) === undefined;

  function submit(event: FormEvent) {
    event.preventDefault();
    if (changes && !sceneInvalid) onSave(changes);
  }

  return (
    <Modal isOpen={isOpen} onOpenChange={onOpenChange}>
      <ModalBackdrop>
        <ModalContainer size="lg">
          <ModalDialog>
            <form onSubmit={submit}>
              <ModalHeader><ModalHeading>Edit call</ModalHeading></ModalHeader>
              <ModalBody className="flex flex-col gap-3">
                <TextField value={form.callerName} onChange={(callerName) => setForm({ ...form, callerName })}>
                  <Label>Caller name</Label>
                  <InputGroup><InputGroupInput maxLength={200} /></InputGroup>
                </TextField>
                <TextField value={form.callerPhone} onChange={(callerPhone) => setForm({ ...form, callerPhone })}>
                  <Label>Caller phone</Label>
                  <InputGroup><InputGroupInput maxLength={20} /></InputGroup>
                </TextField>
                <TextField value={form.details} onChange={(details) => setForm({ ...form, details })}>
                  <Label>Emergency details</Label>
                  <InputGroup><InputGroupTextArea rows={3} maxLength={1000} /></InputGroup>
                </TextField>
                <SceneLocationFields value={form.scene} onChange={(scene) => setForm((current) => ({ ...current, scene }))} />
              </ModalBody>
              <ModalFooter>
                <Button type="button" variant="outline" isDisabled={isPending} onPress={() => onOpenChange(false)}>Cancel</Button>
                <Button type="submit" isDisabled={isPending || !changes || sceneInvalid}>{isPending ? 'Saving…' : 'Save changes'}</Button>
              </ModalFooter>
            </form>
          </ModalDialog>
        </ModalContainer>
      </ModalBackdrop>
    </Modal>
  );
}

function formFor(call: EmergencyCallDetail): EditForm {
  return {
    callerName: call.caller_name ?? '',
    callerPhone: call.caller_phone ?? '',
    details: call.details ?? '',
    scene: {
      latitude: call.latitude == null ? '' : String(call.latitude),
      longitude: call.longitude == null ? '' : String(call.longitude),
      accuracy: call.location_accuracy_metres == null ? '' : String(call.location_accuracy_metres),
    },
  };
}

// Sends only what changed; an empty text box clears that field on the server.
function changesFrom(call: EmergencyCallDetail, form: EditForm): UpdateEmergencyCallRequest | undefined {
  const request: UpdateEmergencyCallRequest = {};
  if (form.callerName.trim() !== (call.caller_name ?? '')) request.caller_name = form.callerName.trim();
  if (form.callerPhone.trim() !== (call.caller_phone ?? '')) request.caller_phone = form.callerPhone.trim();
  if (form.details.trim() !== (call.details ?? '')) request.details = form.details.trim();
  const point = sceneCoordinates(form.scene);
  const accuracy = sceneAccuracy(form.scene);
  if (point && accuracy !== undefined
    && (point.latitude !== call.latitude || point.longitude !== call.longitude || accuracy !== call.location_accuracy_metres)) {
    request.latitude = point.latitude;
    request.longitude = point.longitude;
    request.location_accuracy_metres = accuracy;
  }
  return Object.keys(request).length > 0 ? request : undefined;
}
