import type { ReactNode } from 'react';
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
        <ModalContainer>
          <ModalDialog className={`action-dialog${size === 'compact' ? ' action-dialog--compact' : ''}`}>
            <ModalHeader className="action-dialog-header">
              <ModalHeading>{title}</ModalHeading>
              <Button size="sm" variant="outline" onPress={onClose} aria-label={`Close ${title}`}>
                Close
              </Button>
            </ModalHeader>
            <ModalBody className="action-dialog-body">{children}</ModalBody>
          </ModalDialog>
        </ModalContainer>
      </ModalBackdrop>
    </Modal>
  );
}
