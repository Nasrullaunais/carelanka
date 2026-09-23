import { useEffect, useState } from 'react';
import {
  Button,
  InputGroup,
  InputGroupTextArea,
  Label,
  ListBox,
  ListBoxItem,
  Modal,
  ModalBackdrop,
  ModalBody,
  ModalContainer,
  ModalDialog,
  ModalFooter,
  ModalHeader,
  ModalHeading,
  Select,
  SelectIndicator,
  SelectPopover,
  SelectTrigger,
  SelectValue,
  TextField,
} from '@heroui/react';

export interface ReasonDialogResult {
  option?: string;
  notes?: string;
}

export function ReasonDialog({ isOpen, onOpenChange, title, description, options, optionLabel = 'Reason', notesLabel, notesPlaceholder, notesRequired, confirmLabel, isPending, onConfirm }: {
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  options?: Array<{ value: string; label: string }>;
  optionLabel?: string;
  notesLabel?: string;
  notesPlaceholder?: string;
  notesRequired?: boolean;
  confirmLabel: string;
  isPending?: boolean;
  onConfirm: (result: ReasonDialogResult) => void;
}) {
  const [option, setOption] = useState<string>();
  const [notes, setNotes] = useState('');

  useEffect(() => {
    if (isOpen) {
      setOption(undefined);
      setNotes('');
    }
  }, [isOpen]);

  const optionMissing = options !== undefined && option === undefined;
  const notesMissing = notesRequired && notes.trim() === '';

  return (
    <Modal isOpen={isOpen} onOpenChange={onOpenChange}>
      <ModalBackdrop>
        <ModalContainer>
          <ModalDialog>
            <ModalHeader>
              <ModalHeading>{title}</ModalHeading>
            </ModalHeader>
            <ModalBody className="flex flex-col gap-4">
              {description && <p className="muted">{description}</p>}
              {options && (
                <Select
                  selectedKey={option}
                  onSelectionChange={(key) => key !== undefined && setOption(String(key))}
                  isInvalid={optionMissing}
                >
                  <Label>{optionLabel}</Label>
                  <SelectTrigger>
                    <SelectValue />
                    <SelectIndicator />
                  </SelectTrigger>
                  <SelectPopover>
                    <ListBox>
                      {options.map((item) => (
                        <ListBoxItem key={item.value} id={item.value}>{item.label}</ListBoxItem>
                      ))}
                    </ListBox>
                  </SelectPopover>
                </Select>
              )}
              {notesLabel && (
                <TextField value={notes} onChange={setNotes} isInvalid={notesMissing || undefined}>
                  <Label>{notesLabel}</Label>
                  <InputGroup>
                    <InputGroupTextArea placeholder={notesPlaceholder} rows={3} />
                  </InputGroup>
                </TextField>
              )}
            </ModalBody>
            <ModalFooter>
              <Button variant="outline" isDisabled={isPending} onPress={() => onOpenChange(false)}>
                Cancel
              </Button>
              <Button
                isDisabled={isPending || optionMissing || notesMissing}
                onPress={() => onConfirm({ option, notes: notes.trim() === '' ? undefined : notes })}
              >
                {confirmLabel}
              </Button>
            </ModalFooter>
          </ModalDialog>
        </ModalContainer>
      </ModalBackdrop>
    </Modal>
  );
}
