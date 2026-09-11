import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { logoutMutation } from '../services/api/generated/@tanstack/react-query.gen';
import { clearSession, getSession } from '../services/auth/session';
import { useSession } from '../services/auth/useSession';
import { roleLabels } from '../types/permissions';

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

  return (
    <>
      <header className="shell-header">
        <Link to="/" className="brand">
          CareLanka
        </Link>

        {/* One link, not one per screen. Seven roles reach different sets of pages, and a bar
            carrying all of them is mostly other people's work. The dashboard is where a role
            sees its own. */}
        <nav className="shell-nav">
          <NavLink to="/" end>
            Dashboard
          </NavLink>
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
