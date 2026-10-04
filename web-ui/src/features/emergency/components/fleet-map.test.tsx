import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { FleetMap as FleetMapData } from '../../../services/api/generated';
import { FleetMap } from './fleet-map';

vi.mock('react-leaflet', () => ({
  MapContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  TileLayer: () => null,
  Tooltip: ({ children }: { children: React.ReactNode }) => <span>{children}</span>,
  Polyline: ({ positions }: { positions: number[][] }) => <div data-testid="route-line">{positions.map((point) => point.join(',')).join(' → ')}</div>,
  Marker: ({ children, eventHandlers }: { children: React.ReactNode; eventHandlers?: { click?: () => void } }) => <button type="button" onClick={eventHandlers?.click}>{children}</button>,
  useMap: () => ({ setView: vi.fn(), fitBounds: vi.fn(), invalidateSize: vi.fn(), getContainer: () => document.createElement('div') }),
}));

const fleet: FleetMapData = {
  location_max_age_minutes: 5,
  ambulances: [
    { id: 'amb-1', registration_number: 'WP-CA-1234', status: 'en_route', latitude: 6.9, longitude: 79.85, location_updated_at: new Date().toISOString(), location_is_stale: false, active_call_id: 'call-1' },
    { id: 'amb-2', registration_number: 'WP-CB-5678', status: 'available', latitude: 6.95, longitude: 79.9, location_updated_at: '2026-01-01T00:00:00Z', location_is_stale: true },
    { id: 'amb-3', registration_number: 'WP-CC-0001', status: 'available', latitude: null, longitude: null },
  ],
  calls: [
    { id: 'call-1', priority: 'critical', status: 'en_route', address_label: 'Ward Place', latitude: 6.91, longitude: 79.86, assigned_ambulance_id: 'amb-1' },
    { id: 'call-2', priority: 'medium', status: 'received', address_label: 'Galle Road', latitude: 6.88, longitude: 79.85, waiting_minutes: 7 },
  ],
};

describe('FleetMap', () => {
  afterEach(cleanup);

  it('selects an ambulance and opens a call from their markers', () => {
    const select = vi.fn();
    const open = vi.fn();
    render(<FleetMap map={fleet} onSelectAmbulance={select} onOpenCall={open} />);

    fireEvent.click(screen.getByRole('button', { name: /WP-CA-1234/ }));
    fireEvent.click(screen.getByRole('button', { name: /Galle Road/ }));

    expect(select).toHaveBeenCalledWith('amb-1');
    expect(open).toHaveBeenCalledWith('call-2');
  });

  it('draws a line only from an ambulance to the call it is on', () => {
    render(<FleetMap map={fleet} onSelectAmbulance={vi.fn()} onOpenCall={vi.fn()} />);

    const lines = screen.getAllByTestId('route-line');
    expect(lines).toHaveLength(1);
    expect(lines[0]).toHaveTextContent('6.9,79.85 → 6.91,79.86');
  });

  it('marks old positions and leaves out ambulances with no position', () => {
    render(<FleetMap map={fleet} onSelectAmbulance={vi.fn()} onOpenCall={vi.fn()} />);

    expect(screen.getByRole('button', { name: /WP-CB-5678.*may be out of date/ })).toBeInTheDocument();
    expect(screen.queryByText(/WP-CC-0001/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Galle Road · waiting 7 min/ })).toBeInTheDocument();
  });
});
