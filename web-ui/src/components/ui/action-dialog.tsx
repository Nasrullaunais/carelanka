import { memo, type ReactNode } from 'react';
import {
  Button,
  Modal,
  ModalBackdrop,
  ModalBody,
  ModalContainer,
  ModalDialog,
  ModalHeader,
  ModalHeading,
} from '@heroui/react';

interface DialogContentProps {
  title: string;
  onClose: () => void;
  children: ReactNode;
  size: 'compact' | 'wide';
  isClosing: boolean;
}

// Selection-driven callers clear their title and children when closing. Keep the last
// open render until React Aria unmounts the overlay after its exit animation completes.
const DialogContent = memo(function DialogContent({ title, onClose, children, size }: DialogContentProps) {
  return (
    <ModalDialog className={`action-dialog${size === 'compact' ? ' action-dialog--compact' : ''}`}>
      <ModalHeader className="action-dialog-header">
        <ModalHeading>{title}</ModalHeading>
        <Button size="sm" variant="outline" onPress={onClose} aria-label={`Close ${title}`}>
          Close
        </Button>
      </ModalHeader>
      <ModalBody className="action-dialog-body">{children}</ModalBody>
    </ModalDialog>
  );
}, (_previous, next) => next.isClosing);

export function ActionDialog({ title, isOpen, onClose, children, size = 'wide' }: {
  title: string;
  isOpen: boolean;
  onClose: () => void;
  children: ReactNode;
  size?: 'compact' | 'wide';
}) {
  return (
    <Modal isOpen={isOpen} onOpenChange={(open) => { if (!open) onClose(); }}>
      <ModalBackdrop>
        <ModalContainer placement="center">
          <DialogContent title={title} onClose={onClose} size={size} isClosing={!isOpen}>{children}</DialogContent>
        </ModalContainer>
      </ModalBackdrop>
    </Modal>
  );
}
