import 'leaflet/dist/leaflet.css';
import { useEffect, useRef } from 'react';
import { MapContainer, Marker, Polyline, TileLayer, Tooltip, useMap } from 'react-leaflet';
import type { FleetMap as FleetMapData, FleetMapAmbulance, FleetMapCall } from '../../../services/api/generated';
import { ambulanceStatusLabels, formatAge, priorityLabels } from '../domain';
import { ambulanceMarker, ambulanceMarkerColors, callMarker, callMarkerColors } from './map-marker';

const COLOMBO: [number, number] = [6.9271, 79.8612];

type Point = [number, number];

export function FleetMap({ map, selectedAmbulanceId, onSelectAmbulance, onOpenCall }: {
  map: FleetMapData;
  selectedAmbulanceId?: string;
  onSelectAmbulance: (ambulanceId: string) => void;
  onOpenCall: (callId: string) => void;
}) {
  const located = (map.ambulances ?? []).filter(hasPosition);
  const calls = map.calls ?? [];
  const callsById = new Map(calls.map((call) => [call.id, call]));
  const points: Point[] = [
    ...located.map((ambulance): Point => [ambulance.latitude!, ambulance.longitude!]),
    ...calls.map((call): Point => [call.latitude ?? 0, call.longitude ?? 0]),
  ];

  return (
    <div className="flex flex-col gap-2">
      <div className="h-[32rem] overflow-hidden rounded-xl border border-default-200" aria-label="Fleet and open-call map">
        <MapContainer center={COLOMBO} zoom={12} className="h-full w-full">
          <TileLayer attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
          <FitOnFirstLoad points={points} />
          {located.map((ambulance) => {
            const call = ambulance.active_call_id ? callsById.get(ambulance.active_call_id) : undefined;
            return call && (
              <Polyline
                key={`route-${ambulance.id}`}
                positions={[[ambulance.latitude!, ambulance.longitude!], [call.latitude ?? 0, call.longitude ?? 0]]}
                pathOptions={{ color: ambulanceMarkerColors[ambulance.status ?? 'dispatched'], weight: 3, dashArray: '6 6' }}
              />
            );
          })}
          {calls.map((call) => (
            <Marker
              key={call.id}
              position={[call.latitude ?? 0, call.longitude ?? 0]}
              icon={callMarker(callMarkerColors[call.priority ?? 'high'])}
              eventHandlers={{ click: () => call.id && onOpenCall(call.id) }}
            >
              <Tooltip>{callLabel(call)}</Tooltip>
            </Marker>
          ))}
          {located.map((ambulance) => (
            <Marker
              key={ambulance.id}
              position={[ambulance.latitude!, ambulance.longitude!]}
              icon={ambulanceMarker(ambulanceMarkerColors[ambulance.status ?? 'available'], {
                stale: ambulance.location_is_stale ?? false,
                selected: ambulance.id === selectedAmbulanceId,
              })}
              eventHandlers={{ click: () => ambulance.id && onSelectAmbulance(ambulance.id) }}
            >
              <Tooltip>{ambulanceLabel(ambulance)}</Tooltip>
            </Marker>
          ))}
        </MapContainer>
      </div>
      <Legend />
    </div>
  );
}

function Legend() {
  return (
    <div className="flex flex-wrap gap-x-4 gap-y-2 text-sm" aria-label="Map legend">
      {Object.entries(ambulanceMarkerColors).map(([status, color]) => (
        <span key={status} className="flex items-center gap-1">
          <span className="inline-block size-3 rounded-sm" style={{ backgroundColor: color }} />
          {ambulanceStatusLabels[status as keyof typeof ambulanceStatusLabels]}
        </span>
      ))}
      <span className="flex items-center gap-1"><span className="inline-block size-3 rounded-sm border border-dashed border-default-400 bg-default-300 opacity-50" />Old position</span>
      {Object.entries(callMarkerColors).map(([priority, color]) => (
        <span key={priority} className="flex items-center gap-1">
          <span className="inline-block size-3 rotate-45 rounded-full rounded-bl-none" style={{ backgroundColor: color }} />
          {priorityLabels[priority as keyof typeof priorityLabels]} call
        </span>
      ))}
    </div>
  );
}

function FitOnFirstLoad({ points }: { points: Point[] }) {
  const map = useMap();
  const fitted = useRef(false);
  useEffect(() => {
    if (fitted.current || points.length === 0) return;
    fitted.current = true;
    if (points.length === 1) map.setView(points[0], 14);
    else map.fitBounds(points, { padding: [32, 32], maxZoom: 14 });
  }, [map, points]);
  return null;
}

function hasPosition(ambulance: FleetMapAmbulance): boolean {
  return ambulance.latitude != null && ambulance.longitude != null;
}

function ambulanceLabel(ambulance: FleetMapAmbulance): string {
  const status = ambulanceStatusLabels[ambulance.status ?? 'available'];
  const age = formatAge(ambulance.location_updated_at);
  return `${ambulance.registration_number ?? 'Ambulance'} · ${status} · updated ${age}${ambulance.location_is_stale ? ' (may be out of date)' : ''}`;
}

function callLabel(call: FleetMapCall): string {
  const where = call.address_label ?? 'Scene location';
  const waiting = call.assigned_ambulance_id ? 'ambulance assigned' : `waiting ${call.waiting_minutes ?? 0} min`;
  return `${priorityLabels[call.priority ?? 'high']} call · ${where} · ${waiting}`;
}
