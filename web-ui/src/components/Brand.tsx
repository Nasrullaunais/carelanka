import { HeartPulse } from 'lucide-react';

export function Brand({ compact = false }: { compact?: boolean }) {
  return <span className="brand"><span className="brand-mark"><HeartPulse size={24} aria-hidden="true" /></span>{!compact && <span>CareLanka<span className="brand-caption">Hospital workspace</span></span>}</span>;
}
