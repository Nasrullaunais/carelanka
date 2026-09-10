import { Navigate, Route, Routes } from 'react-router-dom';
import { AppShell } from './components/AppShell';
import { EquipmentPage } from './pages/EquipmentPage';
import { LoginPage } from './pages/LoginPage';
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
        <Route path="/wards" element={<WardsPage />} />
        <Route path="/equipment" element={<EquipmentPage />} />
        <Route path="*" element={<Navigate to="/wards" replace />} />
      </Route>
    </Routes>
  );
}
