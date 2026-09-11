import { Navigate, Route, Routes } from 'react-router-dom';
import { AppShell } from './components/AppShell';
import { AppointmentsPage } from './pages/AppointmentsPage';
import { BillingSettingsPage } from './pages/BillingSettingsPage';
import { CapacityPage } from './pages/CapacityPage';
import { DashboardPage } from './pages/DashboardPage';
import { DischargePage } from './pages/DischargePage';
import { EquipmentPage } from './pages/EquipmentPage';
import { IntakePage } from './pages/IntakePage';
import { LoginPage } from './pages/LoginPage';
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

  // There is no patient sign-in here, so the only way to hold a patient token is a session
  // left over from an earlier build. Say so plainly rather than dropping them into a staff
  // navigation tree with nothing in it.
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
        {/* Billing is not a screen of its own any more - it is part of a discharge. Kept as a
            redirect rather than deleted so an old bookmark or a link in someone's notes still
            lands somewhere useful. */}
        <Route path="/billing" element={<Navigate to="/discharge" replace />} />
        <Route path="/billing-settings" element={<BillingSettingsPage />} />
        <Route path="/capacity" element={<CapacityPage />} />
        <Route path="/wards" element={<WardsPage />} />
        <Route path="/equipment" element={<EquipmentPage />} />
        <Route path="/pharmacy" element={<PharmacyPage />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
