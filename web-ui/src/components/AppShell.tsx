import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { logoutMutation } from '../services/api/generated/@tanstack/react-query.gen';
import { clearSession, getSession } from '../services/auth/session';
import { useSession } from '../services/auth/useSession';
import { canReadWards, roleLabels } from '../types/permissions';

export function AppShell() {
  const session = useSession();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const logout = useMutation({
    ...logoutMutation(),
    // Whether or not the server accepted it, the local session ends. Logging out twice is a
    // 204, and a failed logout that left you signed in would be the worse outcome.
    onSettled: () => {
      clearSession();
      queryClient.clear();
      navigate('/login');
    },
  });

  const role = session?.principal.role;

  return (
    <>
      <header className="shell-header">
        <span className="brand">CareLanka</span>

        <nav className="shell-nav">
          {canReadWards(role) && <NavLink to="/wards">Wards</NavLink>}
        </nav>

        {session && (
          <>
            <div className="whoami">
              <strong>{session.principal.display_name}</strong>
              {roleLabels[session.principal.role]}
            </div>
            <button
              type="button"
              className="secondary"
              disabled={logout.isPending}
              onClick={() => {
                const refreshToken = getSession()?.refreshToken;

                if (refreshToken) {
                  logout.mutate({ body: { refresh_token: refreshToken } });
                } else {
                  clearSession();
                  navigate('/login');
                }
              }}
            >
              Sign out
            </button>
          </>
        )}
      </header>

      <main>
        <Outlet />
      </main>
    </>
  );
}
