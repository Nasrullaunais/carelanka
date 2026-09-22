import { Navigate, Route, Routes } from 'react-router-dom';
import { AppShell } from './components/AppShell';
import { AppointmentsPage } from './pages/AppointmentsPage';
import { BillingSettingsPage } from './pages/BillingSettingsPage';
import { CapacityPage } from './pages/CapacityPage';
import { CareRecommendationsPage } from './pages/CareRecommendationsPage';
import { DashboardPage } from './pages/DashboardPage';
import { DischargePage } from './pages/DischargePage';
import { EquipmentPage } from './pages/EquipmentPage';
import { EmergencyPage } from './pages/EmergencyPage';
import { IntakePage } from './pages/IntakePage';
import { LaboratoryPage } from './pages/LaboratoryPage';
import { LoginPage } from './pages/LoginPage';
import { MaintenanceUnitPage } from './pages/MaintenanceUnitPage';
import { WarningsPage } from './pages/WarningsPage';
import { PatientsPage } from './pages/PatientsPage';
import { PharmacyPage } from './pages/PharmacyPage';
import { WardsPage } from './pages/WardsPage';
import { clearSession } from './services/auth/session';
import { useSession } from './services/auth/useSession';

export function App() {
  const session = useSession();

  if (!session) {
    return (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    );
  }

  if (session.principal.principal_type === 'patient') {
    return (
      <main className="login-page">
        <div className="card">
          <h1>CareLanka</h1>
          <p className="muted">
            This is the staff web app. Patients use the CareLanka mobile app — your
            admission, your ward and bed, and your appointments all live there.
          </p>
          <button type="button" style={{ marginTop: '1rem' }} onClick={clearSession}>
            Sign out
          </button>
        </div>
      </main>
    );
  }

  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/intake" element={<IntakePage />} />
        <Route path="/patients" element={<PatientsPage />} />
        <Route path="/appointments" element={<AppointmentsPage />} />
        <Route path="/discharge" element={<DischargePage />} />
        <Route path="/care-recommendations" element={<CareRecommendationsPage />} />

        <Route path="/billing" element={<Navigate to="/discharge" replace />} />
        <Route path="/billing-settings" element={<BillingSettingsPage />} />
        <Route path="/capacity" element={<CapacityPage />} />
        <Route path="/wards" element={<WardsPage />} />
        <Route path="/equipment" element={<EquipmentPage />} />
        <Route path="/emergency" element={<EmergencyPage />} />
        <Route path="/maintenance-unit" element={<MaintenanceUnitPage />} />
        <Route path="/warnings" element={<WarningsPage />} />
        <Route path="/laboratory" element={<LaboratoryPage />} />
        <Route path="/pharmacy" element={<PharmacyPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
