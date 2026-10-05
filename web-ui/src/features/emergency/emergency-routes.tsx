import { Navigate, Route, Routes, useLocation, useNavigate, useParams } from 'react-router-dom';
import { Tab, TabIndicator, TabList, TabListContainer, Tabs } from '@heroui/react';
import { useSession } from '../../services/auth/useSession';
import { canManageEmergency } from '../../types/permissions';
import { EmergencyDesk } from './components/emergency-desk';
import { FleetBoard } from './components/fleet-board';
import { AmbulanceRegister } from './components/ambulance-register';
import { CancellationQueue } from './components/cancellation-queue';
import { EmergencyReports } from './components/emergency-reports';
import { emergencyCallPath } from './domain';
import { useRefreshOnNewNotification } from './hooks/use-refresh-on-new-notification';

export interface EmergencyTabDefinition {
  id: string;
  path: string;
  label: string;
}

export const emergencyTabs: EmergencyTabDefinition[] = [
  { id: 'calls', path: '', label: 'Calls' },
  { id: 'fleet', path: 'fleet', label: 'Fleet' },
  { id: 'register', path: 'register', label: 'Register' },
  { id: 'cancellations', path: 'cancellations', label: 'Cancellations' },
  { id: 'reports', path: 'reports', label: 'Reports' },
];

const tabDescriptions: Record<string, string> = {
  calls: 'Live calls, ready ambulances, and current response crews.',
  fleet: 'Where every ambulance is and who is on board.',
  register: 'Add, edit, or retire the ambulances the hospital runs.',
  cancellations: 'Review requests to stop an emergency response.',
  reports: 'How quickly calls are answered and how the fleet is used.',
};

export function EmergencyRoutes() {
  const session = useSession();
  const navigate = useNavigate();
  const location = useLocation();
  useRefreshOnNewNotification();

  if (!canManageEmergency(session?.principal.role)) {
    return <AccessDenied />;
  }

  const activeTab = location.pathname === '/emergency'
    ? 'calls'
    : location.pathname.split('/')[2] ?? 'calls';
  const pageDescription = tabDescriptions[activeTab] ?? tabDescriptions.calls;

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
                {tab.label}
              </Tab>
            ))}
          </TabList>
        </TabListContainer>
      </Tabs>
      <Routes>
        <Route index element={<DeskRoute />} />
        <Route path="calls" element={<Navigate to="/emergency" replace />} />
        <Route path="calls/:callId" element={<DeskRoute />} />
        <Route path="*" element={<Navigate to="/emergency" replace />} />
        <Route path="fleet" element={<FleetBoard />} />
        <Route path="register" element={<AmbulanceRegister />} />
        <Route path="cancellations" element={<CancellationQueue />} />
        <Route path="reports" element={<EmergencyReports />} />
      </Routes>
    </div>
  );
}

function DeskRoute() {
  const { callId } = useParams();
  const navigate = useNavigate();
  return (
    <EmergencyDesk
      selectedCallId={callId}
      onSelectCall={(id) => navigate(emergencyCallPath(id))}
      onCloseCall={() => navigate('/emergency')}
    />
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
