import type { ReactNode } from 'react';
import { Card, CardContent, CardDescription, CardTitle } from '@heroui/react';

export function SummaryCard({ title, value, hint }: { title: string; value: ReactNode; hint?: string }) {
  return (
    <Card>
      <CardContent>
        <CardDescription>{title}</CardDescription>
        <CardTitle className="text-2xl">{value}</CardTitle>
        {hint && <p className="text-xs text-muted">{hint}</p>}
      </CardContent>
    </Card>
  );
}
