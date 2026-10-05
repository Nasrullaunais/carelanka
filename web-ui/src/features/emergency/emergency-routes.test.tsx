import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { PrincipalRole } from '../../services/api/generated';
import { EmergencyRoutes } from './emergency-routes';

const current = vi.hoisted(() => ({ role: undefined as string | undefined }));

vi.mock('../../services/auth/useSession', () => ({
  useSession: () => (current.role ? { principal: { role: current.role } } : null),
}));
vi.mock('./hooks/use-refresh-on-new-notification', () => ({ useRefreshOnNewNotification: () => undefined }));
vi.mock('./components/emergency-desk', () => ({
  EmergencyDesk: ({ selectedCallId }: { selectedCallId?: string }) => <div>Call desk {selectedCallId ?? 'none'}</div>,
}));
vi.mock('./components/fleet-board', () => ({ FleetBoard: () => <div>Fleet board</div> }));
vi.mock('./components/ambulance-register', () => ({ AmbulanceRegister: () => <div>Ambulance register</div> }));
vi.mock('./components/cancellation-queue', () => ({ CancellationQueue: () => <div>Cancellation queue</div> }));
vi.mock('./components/emergency-reports', () => ({ EmergencyReports: () => <div>Emergency reports</div> }));

const deniedMessage = 'Only Duty Managers can view calls, manage crews, or dispatch an ambulance.';

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/emergency/*" element={<EmergencyRoutes />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('EmergencyRoutes', () => {
  afterEach(() => {
    cleanup();
    current.role = undefined;
  });

  const otherRoles: PrincipalRole[] = [
    'ward_nurse', 'doctor', 'ambulance_crew', 'general_staff', 'hospital_administrator',
    'equipment_manager', 'equipment_administrator', 'patient',
  ];

  it.each(otherRoles)('shuts %s out of every emergency page', (role) => {
    current.role = role;

    for (const path of ['/emergency', '/emergency/fleet', '/emergency/register', '/emergency/cancellations', '/emergency/reports']) {
      const { unmount } = renderAt(path);
      expect(screen.getByText(deniedMessage)).toBeInTheDocument();
      expect(screen.queryByRole('tab')).not.toBeInTheDocument();
      expect(screen.queryByText(/Call desk|Fleet board|Ambulance register|Cancellation queue|Emergency reports/)).not.toBeInTheDocument();
      unmount();
    }
  });

  it('shuts out a visitor with no session', () => {
    renderAt('/emergency');

    expect(screen.getByText(deniedMessage)).toBeInTheDocument();
    expect(screen.queryByText(/Call desk/)).not.toBeInTheDocument();
  });

  it.each([
    ['/emergency', 'Call desk none'],
    ['/emergency/calls/call-7', 'Call desk call-7'],
    ['/emergency/fleet', 'Fleet board'],
    ['/emergency/register', 'Ambulance register'],
    ['/emergency/cancellations', 'Cancellation queue'],
    ['/emergency/reports', 'Emergency reports'],
  ])('lets the duty manager open %s', (path, page) => {
    current.role = 'duty_manager';

    renderAt(path);

    expect(screen.getByText(page)).toBeInTheDocument();
    expect(screen.getAllByRole('tab')).toHaveLength(5);
    expect(screen.queryByText(deniedMessage)).not.toBeInTheDocument();
  });

  it('sends an unknown emergency address back to the call desk', () => {
    current.role = 'duty_manager';

    renderAt('/emergency/not-a-page');

    expect(screen.getByText('Call desk none')).toBeInTheDocument();
  });
});
