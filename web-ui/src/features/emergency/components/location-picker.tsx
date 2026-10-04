import 'leaflet/dist/leaflet.css';
import { useEffect } from 'react';
import { MapContainer, Marker, TileLayer, useMap, useMapEvents } from 'react-leaflet';
import { FollowContainerSize } from './follow-container-size';
import { circleMarker } from './map-marker';

const COLOMBO: [number, number] = [6.9271, 79.8612];

export interface MapLocation {
  latitude: number;
  longitude: number;
}

export function LocationPicker({ value, onChange, readOnly = false }: { value?: MapLocation; onChange: (location: MapLocation) => void; readOnly?: boolean }) {
  const position: [number, number] = value ? [value.latitude, value.longitude] : COLOMBO;
  return (
    <div className="h-64 overflow-hidden rounded-xl border border-default-200" aria-label="Emergency location picker">
      <MapContainer center={position} zoom={13} className="h-full w-full">
        <FollowContainerSize />
        <TileLayer attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" />
        <PickerMarker position={position} onChange={onChange} readOnly={readOnly} />
        <KeepInView latitude={position[0]} longitude={position[1]} />
      </MapContainer>
    </div>
  );
}

function PickerMarker({ position, onChange, readOnly }: { position: [number, number]; onChange: (location: MapLocation) => void; readOnly: boolean }) {
  useMapEvents({ click: (event) => { if (!readOnly) onChange({ latitude: event.latlng.lat, longitude: event.latlng.lng }); } });
  return (
    <Marker
      draggable={!readOnly}
      position={position}
      icon={circleMarker('#e11d48')}
      eventHandlers={{ dragend: (event) => {
        const point = event.target.getLatLng();
        onChange({ latitude: point.lat, longitude: point.lng });
      } }}
    />
  );
}

function KeepInView({ latitude, longitude }: { latitude: number; longitude: number }) {
  const map = useMap();
  useEffect(() => {
    if (!map.getBounds().contains([latitude, longitude])) map.setView([latitude, longitude], Math.max(map.getZoom(), 15));
  }, [map, latitude, longitude]);
  return null;
}
