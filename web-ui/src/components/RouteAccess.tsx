import { Link, Outlet, useLocation } from 'react-router-dom';
import { ShieldCheck } from 'lucide-react';
import { useSession } from '../services/auth/useSession';
import { destinations } from '../types/navigation';

/** Match whole path segments so nested routes inherit their destination's policy. */
export function RouteAccess() {
  const { pathname } = useLocation();
  const session = useSession();
  const path = pathname === '/billing' ? '/discharge' : pathname;
  const destination = destinations.find(({ to }) => path === to || path.startsWith(`${to}/`));
  if (!destination?.canAccess(session?.principal.role)) {
    return <section className="access-message card">
      <ShieldCheck size={32} aria-hidden="true" />
      <h1>This page isn’t available to your role</h1>
      <p className="muted">Your workspace contains the tools available to you. Contact your administrator if you need access.</p>
      <Link to="/">Return to dashboard</Link>
    </section>;
  }
  return <Outlet />;
}
