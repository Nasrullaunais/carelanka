import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { PrincipalRole } from '../services/api/generated';
import { destinationsFor } from '../types/navigation';
import { RouteAccess } from './RouteAccess';

const current = vi.hoisted(() => ({ role: undefined as PrincipalRole | undefined }));
vi.mock('../services/auth/useSession', () => ({ useSession: () => current.role ? { principal: { role: current.role } } : null }));
afterEach(cleanup);

const shared = ['/capacity', '/wards', '/equipment', '/pharmacy'];
// Explicit expectations keep navigation and its guard from silently drifting together.
const allowed: Record<PrincipalRole, string[]> = {
  general_staff: [...shared, '/intake', '/patients', '/appointments', '/discharge'],
  ward_nurse: [...shared, '/intake', '/patients', '/appointments', '/discharge', '/care-recommendations', '/laboratory'],
  doctor: [...shared, '/patients', '/discharge', '/care-recommendations', '/laboratory'],
  duty_manager: [...shared, '/emergency', '/intake', '/patients', '/appointments', '/discharge', '/care-recommendations', '/laboratory'],
  hospital_administrator: [...shared, '/patients', '/appointments', '/discharge', '/billing-settings', '/maintenance-unit', '/warnings'],
  equipment_manager: [...shared, '/patients', '/warnings', '/laboratory'],
  ambulance_crew: shared,
  patient: [],
};
const paths = [...new Set(Object.values(allowed).flat()), '/emergency/calls', '/billing', '/unregistered'];

describe('role-aware navigation and route access', () => {
  for (const [role, expected] of Object.entries(allowed)) {
    it(`${role} sees only its permitted destinations`, () => {
      expect(destinationsFor(role as PrincipalRole).map(({ to }) => to).sort()).toEqual([...expected].sort());
    });
    for (const path of paths) {
      it(`${role}: guards ${path} before mounting protected content`, () => {
        current.role = role as PrincipalRole;
        const content = vi.fn(() => <p>Protected content</p>);
        const Content = content;
        render(<MemoryRouter initialEntries={[path]}><Routes><Route element={<RouteAccess />}><Route path="*" element={<Content />} /></Route></Routes></MemoryRouter>);
        const destination = path === '/billing' ? '/discharge' : path === '/emergency/calls' ? '/emergency' : path;
        if (expected.includes(destination)) {
          expect(screen.getByText('Protected content')).toBeInTheDocument();
        } else {
          expect(screen.getByRole('heading', { name: 'This page isn’t available to your role' })).toBeInTheDocument();
          expect(content).not.toHaveBeenCalled();
        }
      });
    }
  }
  it('denies a missing session', () => {
    current.role = undefined;
    render(<MemoryRouter initialEntries={['/wards']}><RouteAccess /></MemoryRouter>);
    expect(screen.getByRole('heading', { name: 'This page isn’t available to your role' })).toBeInTheDocument();
    expect(destinationsFor(undefined)).toEqual([]);
  });
});
