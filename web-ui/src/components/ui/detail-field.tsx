import type { ReactNode } from 'react';

export function DetailField({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs font-medium text-muted">{label}</span>
      <div className="text-sm text-foreground">{children}</div>
    </div>
  );
}
