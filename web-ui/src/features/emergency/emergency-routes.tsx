import { Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { Chip, ChipLabel, Tab, TabIndicator, TabList, TabListContainer, Tabs } from '@heroui/react';
import { useSession } from '../../services/auth/useSession';
import { canManageEmergency } from '../../types/permissions';
import { EmergencyDesk } from './components/emergency-desk';
import { ProposalQueue } from './components/proposal-queue';
import { FleetBoard } from './components/fleet-board';
import { AmbulanceRegister } from './components/ambulance-register';
import { CancellationQueue } from './components/cancellation-queue';
import { EmergencyReports } from './components/emergency-reports';
import { useActionableProposals } from './hooks/use-actionable-proposals';

export interface EmergencyTabDefinition {
  id: string;
  path: string;
  label: string;
}

export const emergencyTabs: EmergencyTabDefinition[] = [
  { id: 'calls', path: '', label: 'Calls' },
  { id: 'proposals', path: 'proposals', label: 'Proposals' },
  { id: 'fleet', path: 'fleet', label: 'Fleet' },
  { id: 'register', path: 'register', label: 'Register' },
  { id: 'cancellations', path: 'cancellations', label: 'Cancellations' },
  { id: 'reports', path: 'reports', label: 'Reports' },
];

export function EmergencyRoutes() {
  const session = useSession();
  const navigate = useNavigate();
  const location = useLocation();
  const proposals = useActionableProposals();

  if (!canManageEmergency(session?.principal.role)) {
    return <AccessDenied />;
  }

  const activeTab = location.pathname === '/emergency'
    ? 'calls'
    : location.pathname.split('/')[2] ?? 'calls';
  const pageDescription = activeTab === 'cancellations'
    ? 'Review requests to stop an emergency response.'
    : 'Live calls, ready ambulances, and current response crews.';

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1>Emergency dispatch</h1>
        <p className="muted">{pageDescription}</p>
      </div>
      <Tabs
        selectedKey={activeTab}
        onSelectionChange={(key) => {
          const tab = emergencyTabs.find((item) => item.id === key);
          if (tab) navigate(tab.path === '' ? '/emergency' : `/emergency/${tab.path}`);
        }}
      >
        <TabListContainer className="emergency-tabs">
          <TabList>
            {emergencyTabs.map((tab) => (
              <Tab key={tab.id} id={tab.id} className={activeTab === tab.id ? 'emergency-tab-active' : undefined}>
                <TabIndicator />
                <span className="flex items-center gap-2">
                  {tab.label}
                  {tab.id === 'proposals' && proposals.pendingCount > 0 && (
                    <Chip size="sm" color="warning"><ChipLabel>{proposals.pendingCount}</ChipLabel></Chip>
                  )}
                </span>
              </Tab>
            ))}
          </TabList>
        </TabListContainer>
      </Tabs>
      <Routes>
        <Route index element={<EmergencyDesk />} />
        <Route path="proposals" element={<ProposalQueue />} />
        <Route path="fleet" element={<FleetBoard />} />
        <Route path="register" element={<AmbulanceRegister />} />
        <Route path="cancellations" element={<CancellationQueue />} />
        <Route path="reports" element={<EmergencyReports />} />
      </Routes>
    </div>
  );
}

function AccessDenied() {
  return (
    <section className="card">
      <h1>Emergency dispatch</h1>
      <p className="muted">Only Duty Managers can view calls, manage crews, or dispatch an ambulance.</p>
    </section>
  );
}
