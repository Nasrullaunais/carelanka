import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '@heroui/react';
import { logoutMutation } from '../services/api/generated/@tanstack/react-query.gen';
import { clearSession, getSession } from '../services/auth/session';
import { useSession } from '../services/auth/useSession';
import { roleLabels } from '../types/permissions';
import { canManageEmergency } from '../types/permissions';

export function AppShell() {
  const session = useSession();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const logout = useMutation({
    ...logoutMutation(),
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

        <nav className="shell-nav">
          <NavLink to="/" end>
            Dashboard
          </NavLink>
          {canManageEmergency(session?.principal.role) && <NavLink to="/emergency">Emergency</NavLink>}
        </nav>

        {session && (
          <>
            <div className="whoami">
              <strong>{session.principal.display_name}</strong>
              {roleLabels[session.principal.role]}
            </div>
            <Button
              variant="secondary"
              size="sm"
              isDisabled={logout.isPending}
              onPress={() => {
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
            </Button>
          </>
        )}
      </header>

      <main>
        <Outlet />
      </main>
    </>
  );
}
