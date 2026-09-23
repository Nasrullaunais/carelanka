import { divIcon, type DivIcon } from 'leaflet';
import type { AmbulanceStatus } from '../../../services/api/generated';

export const ambulanceMarkerColors: Record<AmbulanceStatus, string> = {
  available: '#16a34a',
  dispatched: '#d97706',
  en_route: '#2563eb',
  at_scene: '#2563eb',
  transporting: '#2563eb',
  out_of_service: '#dc2626',
};

export function circleMarker(color: string): DivIcon {
  return divIcon({
    className: '',
    html: `<span style="display:block;width:1rem;height:1rem;border-radius:9999px;background:${color};border:2px solid white;box-shadow:0 1px 4px rgb(0 0 0 / .55)"></span>`,
    iconSize: [16, 16],
    iconAnchor: [8, 8],
  });
}
