import { divIcon, type DivIcon } from 'leaflet';
import type { AmbulanceStatus, CallPriority } from '../../../services/api/generated';

// Grey, not red, for out of service: red on this map means a patient waiting.
export const ambulanceMarkerColors: Record<AmbulanceStatus, string> = {
  available: '#16a34a',
  dispatched: '#d97706',
  en_route: '#2563eb',
  at_scene: '#7c3aed',
  transporting: '#0891b2',
  out_of_service: '#6b7280',
};

export const callMarkerColors: Record<CallPriority, string> = {
  critical: '#be123c',
  high: '#ea580c',
  medium: '#ca8a04',
  low: '#64748b',
};

export function circleMarker(color: string): DivIcon {
  return divIcon({
    className: '',
    html: `<span style="display:block;width:1rem;height:1rem;border-radius:9999px;background:${color};border:2px solid white;box-shadow:0 1px 4px rgb(0 0 0 / .55)"></span>`,
    iconSize: [16, 16],
    iconAnchor: [8, 8],
  });
}

export function ambulanceMarker(color: string, { stale, selected }: { stale: boolean; selected: boolean }): DivIcon {
  const ring = selected ? '0 0 0 3px #111827,' : '';
  return divIcon({
    className: '',
    html: `<span style="display:flex;align-items:center;justify-content:center;width:1.5rem;height:1.5rem;border-radius:.35rem;background:${color};color:white;font:700 1rem/1 sans-serif;border:2px ${stale ? 'dashed' : 'solid'} white;opacity:${stale ? 0.45 : 1};box-shadow:${ring}0 1px 4px rgb(0 0 0 / .55)">+</span>`,
    iconSize: [24, 24],
    iconAnchor: [12, 12],
  });
}

export function callMarker(color: string): DivIcon {
  return divIcon({
    className: '',
    html: `<span style="display:block;width:1.4rem;height:1.4rem;border-radius:50% 50% 50% 0;transform:rotate(-45deg);background:${color};border:2px solid white;box-shadow:0 1px 4px rgb(0 0 0 / .55)"></span>`,
    iconSize: [22, 22],
    iconAnchor: [11, 22],
  });
}
