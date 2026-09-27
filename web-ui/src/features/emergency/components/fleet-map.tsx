import 'leaflet/dist/leaflet.css';
import { MapContainer, Marker, Popup, TileLayer } from 'react-leaflet';
import type { AmbulanceSummary, EmergencyCallSummary } from '../../../services/api/generated';
import { ambulanceStatusLabels, formatTimestamp, priorityLabels } from '../domain';
import { ambulanceMarkerColors, circleMarker } from './map-marker';

const COLOMBO: [number, number] = [6.9271, 79.8612];

export function FleetMap({ ambulances, calls = [], selectedId, onSelect }: {
  ambulances: AmbulanceSummary[];
  calls?: EmergencyCallSummary[];
  selectedId?: string;
  onSelect: (ambulanceId: string) => void;
}) {
  const located = ambulances.filter((item) => item.current_latitude != null && item.current_longitude != null);
  const center: [number, number] = located.length > 0
    ? [located[0].current_latitude!, located[0].current_longitude!]
    : COLOMBO;

  return (
    <div className="flex flex-col gap-2">
      <div className="h-[28rem] overflow-hidden rounded-xl border border-default-200" aria-label="Fleet and open-call map">
        <MapContainer center={center} zoom={12} className="h-full w-full">
          <TileLayer attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
          {located.map((ambulance) => (
            <Marker
              key={ambulance.id}
              position={[ambulance.current_latitude!, ambulance.current_longitude!]}
              icon={circleMarker(selectedId === ambulance.id ? '#111827' : ambulanceMarkerColors[ambulance.status])}
              eventHandlers={{ click: () => onSelect(ambulance.id) }}
            >
              <Popup>
                <strong>{ambulance.registration_number}</strong><br />
                {ambulanceStatusLabels[ambulance.status]} · crew {ambulance.current_crew_count ?? 0}/{ambulance.required_crew_count ?? 2}<br />
                Location updated {formatTimestamp(ambulance.location_updated_at)}
              </Popup>
            </Marker>
          ))}
          {calls.filter((call) => call.latitude != null && call.longitude != null).map((call) => (
            <Marker key={call.id} position={[call.latitude!, call.longitude!]} icon={circleMarker('#e11d48')}>
              <Popup><strong>{priorityLabels[call.priority ?? 'high']} call</strong><br />{call.address_label ?? 'Scene location'}</Popup>
            </Marker>
          ))}
        </MapContainer>
      </div>
      <div className="flex flex-wrap gap-3 text-sm" aria-label="Map legend">
        {Object.entries(ambulanceMarkerColors).map(([status, color]) => (
          <span key={status} className="flex items-center gap-1"><span className="inline-block size-3 rounded-full" style={{ backgroundColor: color }} />{ambulanceStatusLabels[status as keyof typeof ambulanceStatusLabels]}</span>
        ))}
        <span className="flex items-center gap-1"><span className="inline-block size-3 rounded-full bg-rose-600" />Open call</span>
      </div>
    </div>
  );
}
