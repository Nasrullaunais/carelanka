import type { ReactNode } from 'react';
import { Chip, ChipLabel } from '@heroui/react';
import type { StatusTone } from '../../features/emergency/domain';

export function StatusChip({ tone, children }: { tone: StatusTone; children: ReactNode }) {
  return (
    <Chip color={tone} size="sm">
      <ChipLabel>{children}</ChipLabel>
    </Chip>
  );
}
