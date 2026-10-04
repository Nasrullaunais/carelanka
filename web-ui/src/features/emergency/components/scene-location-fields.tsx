import { lazy, Suspense } from 'react';
import { InputGroup, InputGroupInput, Label, TextField } from '@heroui/react';
import { SceneAddressSearch } from './scene-address-search';

// A pin dropped from a caller's description is close, not exact.
const MAP_PICK_ACCURACY_METRES = 50;

const LocationPicker = lazy(() => import('./location-picker').then((module) => ({ default: module.LocationPicker })));

export interface SceneLocation {
  latitude: string;
  longitude: string;
  accuracy: string;
}

export const emptySceneLocation: SceneLocation = { latitude: '', longitude: '', accuracy: '' };

export function SceneLocationFields({ value, onChange }: { value: SceneLocation; onChange: (value: SceneLocation) => void }) {
  return (
    <div className="flex flex-col gap-3">
      <SceneAddressSearch
        onPick={(place) => onChange({
          latitude: String(place.latitude),
          longitude: String(place.longitude),
          accuracy: String(place.approximate_accuracy_metres ?? MAP_PICK_ACCURACY_METRES),
        })}
      />
      <Suspense fallback={<div className="h-64 animate-pulse rounded-xl bg-default-100" aria-label="Loading location map" />}>
        <LocationPicker
          value={sceneCoordinates(value)}
          onChange={({ latitude, longitude }) => onChange({
            latitude: String(latitude),
            longitude: String(longitude),
            accuracy: String(MAP_PICK_ACCURACY_METRES),
          })}
        />
      </Suspense>
      <p className="text-sm text-muted">Search, click the map or drag the pin. You can also type coordinates if the map does not load.</p>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <NumberField label="Latitude" min={-90} max={90} value={value.latitude} onChange={(latitude) => onChange({ ...value, latitude })} />
        <NumberField label="Longitude" min={-180} max={180} value={value.longitude} onChange={(longitude) => onChange({ ...value, longitude })} />
        <NumberField label="Accuracy (m)" min={0} value={value.accuracy} onChange={(accuracy) => onChange({ ...value, accuracy })} />
      </div>
    </div>
  );
}

export function sceneCoordinates(value: SceneLocation): { latitude: number; longitude: number } | undefined {
  const latitude = Number(value.latitude);
  const longitude = Number(value.longitude);
  return value.latitude !== '' && value.longitude !== ''
    && Number.isFinite(latitude) && latitude >= -90 && latitude <= 90
    && Number.isFinite(longitude) && longitude >= -180 && longitude <= 180
    ? { latitude, longitude }
    : undefined;
}

export function sceneAccuracy(value: SceneLocation): number | undefined {
  const accuracy = Number(value.accuracy);
  return value.accuracy !== '' && Number.isFinite(accuracy) && accuracy >= 0 ? accuracy : undefined;
}

function NumberField({ label, value, min, max, onChange }: {
  label: string;
  value: string;
  min?: number;
  max?: number;
  onChange: (value: string) => void;
}) {
  return (
    <TextField value={value} onChange={onChange}>
      <Label>{label}</Label>
      <InputGroup><InputGroupInput type="number" step="any" min={min} max={max} required /></InputGroup>
    </TextField>
  );
}
