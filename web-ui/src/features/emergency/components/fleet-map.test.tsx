import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { FleetMap } from './fleet-map';

vi.mock('react-leaflet', () => ({
  MapContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  TileLayer: () => null,
  Popup: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  Marker: ({ children, eventHandlers }: { children: React.ReactNode; eventHandlers?: { click?: () => void } }) => <button type="button" onClick={eventHandlers?.click}>{children}</button>,
}));

describe('FleetMap', () => {
  afterEach(cleanup);

  it('selects the ambulance represented by a marker', () => {
    const select = vi.fn();
    render(<FleetMap ambulances={[{
      id: 'amb-1', registration_number: 'WP-CA-1234', status: 'available', current_latitude: 6.927, current_longitude: 79.861, is_divertible: false,
    }]} onSelect={select} />);

    fireEvent.click(screen.getByRole('button'));
    expect(select).toHaveBeenCalledWith('amb-1');
    expect(screen.getByText('WP-CA-1234')).toBeInTheDocument();
  });
});
