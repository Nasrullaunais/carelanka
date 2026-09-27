import { cleanup, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithProviders } from '../../../test/api-mocks';
import { EmergencyReports } from './emergency-reports';

vi.mock('../../../services/api/generated/@tanstack/react-query.gen', () => ({
  getEmergencyResponseTimeReportOptions: () => ({ queryKey: ['response-report'], queryFn: () => Promise.resolve({
    rows: [{ priority: 'high', call_count: 2, median_minutes_to_dispatch: 4, median_minutes_to_arrival: 12, slowest_minutes_to_arrival: 18 }],
    totals: { call_count: 2, median_minutes_to_dispatch: 4, median_minutes_to_arrival: 12 },
  }) }),
  getFleetUtilisationReportOptions: () => ({ queryKey: ['fleet-report'], queryFn: () => Promise.resolve({
    rows: [{ ambulance_id: 'amb-1', registration_number: 'WP-CA-1234', run_count: 3, hours_committed: 5.5, idle_share: 0.8, out_of_service_hours: 0 }],
  }) }),
  getEmergencyAgentPerformanceReportOptions: () => ({ queryKey: ['agent-report'], queryFn: () => Promise.resolve({
    proposals_raised: 4, confirmed: 3, confirmed_without_change_rate: 0.75, validation_failure_rate: 0.25,
  }) }),
}));

describe('EmergencyReports', () => {
  afterEach(cleanup);

  it('shows report summaries and each tabular report', async () => {
    renderWithProviders(<EmergencyReports />);

    expect((await screen.findAllByText('4.0 min')).length).toBeGreaterThan(0);
    expect(screen.getByText('75.0%')).toBeInTheDocument();
    expect(screen.getByRole('grid', { name: 'Response times by priority' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('tab', { name: 'Fleet utilisation' }));
    expect(screen.getByRole('grid', { name: 'Fleet utilisation per vehicle' })).toBeInTheDocument();
    expect(screen.getByText('WP-CA-1234')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('tab', { name: 'Agent performance' }));
    expect(screen.getByText('Validation failure rate')).toBeInTheDocument();
    expect(screen.getByText('25.0%')).toBeInTheDocument();
  });
});
