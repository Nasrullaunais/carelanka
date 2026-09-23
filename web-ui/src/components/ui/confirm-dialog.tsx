import type { ReactNode } from 'react';
import {
  AlertDialog,
  AlertDialogBackdrop,
  AlertDialogBody,
  AlertDialogContainer,
  AlertDialogDialog,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogHeading,
  Button,
} from '@heroui/react';

export function ConfirmDialog({ isOpen, onOpenChange, title, description, confirmLabel = 'Confirm', cancelLabel = 'Cancel', tone = 'danger', isPending, onConfirm }: {
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  tone?: 'danger' | 'accent';
  isPending?: boolean;
  onConfirm: () => void;
}) {
  return (
    <AlertDialog isOpen={isOpen} onOpenChange={onOpenChange}>
      <AlertDialogBackdrop>
        <AlertDialogContainer>
          <AlertDialogDialog>
            <AlertDialogHeader>
              <AlertDialogHeading>{title}</AlertDialogHeading>
            </AlertDialogHeader>
            {description && <AlertDialogBody>{description}</AlertDialogBody>}
            <AlertDialogFooter>
              <Button variant="outline" isDisabled={isPending} onPress={() => onOpenChange(false)}>
                {cancelLabel}
              </Button>
              <Button
                variant={tone === 'danger' ? 'danger' : 'primary'}
                isDisabled={isPending}
                onPress={onConfirm}
              >
                {confirmLabel}
              </Button>
            </AlertDialogFooter>
          </AlertDialogDialog>
        </AlertDialogContainer>
      </AlertDialogBackdrop>
    </AlertDialog>
  );
}
