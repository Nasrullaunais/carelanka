import { useState } from 'react';
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Popover, PopoverContent, PopoverDialog } from '@heroui/react';
import { ChevronLeft, ChevronRight, LayoutDashboard, LogOut, UserRound } from 'lucide-react';
import { logoutMutation } from '../services/api/generated/@tanstack/react-query.gen';
import { clearSession, getSession } from '../services/auth/session';
import { useSession } from '../services/auth/useSession';
import { roleLabels } from '../types/permissions';
import { destinationsFor } from '../types/navigation';
import { Brand } from './Brand';
import { ProfileAvatar } from './ProfileAvatar';

export function AppShell() {
  const session = useSession();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [collapsed, setCollapsed] = useState(() => {
    try {
      const saved = localStorage.getItem('carelanka.sidebar.collapsed');
      return saved === null ? window.matchMedia('(max-width: 760px)').matches : saved === 'true';
    } catch { return window.matchMedia('(max-width: 760px)').matches; }
  });
  const [profileOpen, setProfileOpen] = useState(false);
  function finishLogout() {
    clearSession();
    queryClient.clear();
    navigate('/login', { replace: true });
  }
  const logout = useMutation({ ...logoutMutation(), onSettled: finishLogout });
  function signOut() {
    const refreshToken = getSession()?.refreshToken;
    if (refreshToken) logout.mutate({ body: { refresh_token: refreshToken } });
    else finishLogout();
  }
  function toggleSidebar() {
    const next = !collapsed;
    setCollapsed(next);
    try { localStorage.setItem('carelanka.sidebar.collapsed', String(next)); } catch { /* Storage may be disabled. */ }
  }
  function closeMobileNavigation() {
    if (window.matchMedia('(max-width: 760px)').matches) setCollapsed(true);
  }
  const destinations = destinationsFor(session?.principal.role);
  const groups = ['Patient care', 'Hospital', 'Administration'] as const;
  return <div className={`app-shell${collapsed ? ' app-shell--collapsed' : ''}`}>
    <a className="skip-link" href="#workspace">Skip to content</a>
    <aside className="sidebar" aria-label="Workspace sidebar">
      <div className="sidebar-brand"><Link to="/" aria-label="CareLanka dashboard"><Brand /></Link></div>
      <button className="sidebar-toggle" onClick={toggleSidebar} aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'} aria-expanded={!collapsed} aria-controls="workspace-navigation" title={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}>
        {collapsed ? <ChevronRight size={16} /> : <ChevronLeft size={16} />}
      </button>
      <nav id="workspace-navigation" className="sidebar-nav" aria-label="Main navigation">
        <NavLink to="/" end className="sidebar-link" onClick={closeMobileNavigation} title="Dashboard" aria-label="Dashboard"><LayoutDashboard size={19} aria-hidden="true" /><span>Dashboard</span></NavLink>
        {groups.map((group) => {
          const links = destinations.filter((destination) => destination.group === group);
          return links.length > 0 && <div className="sidebar-group" key={group}>
            <p className="sidebar-group-label">{group}</p>
            {links.map(({ to, label, icon: Icon }) => <NavLink key={to} to={to} className="sidebar-link" onClick={closeMobileNavigation} title={label} aria-label={label}><Icon size={19} aria-hidden="true" /><span>{label}</span></NavLink>)}
          </div>;
        })}
      </nav>
      {session && <div className="sidebar-footer">
        <Popover isOpen={profileOpen} onOpenChange={setProfileOpen}>
            <Button variant="ghost" className="profile-trigger" aria-label="Open profile menu">
              <ProfileAvatar name={session.principal.display_name} />
              <span className="profile-summary"><strong>{session.principal.display_name}</strong><span>{roleLabels[session.principal.role]}</span></span>
              <ChevronRight className="profile-chevron" size={16} aria-hidden="true" />
            </Button>
          <PopoverContent placement="top start" className="profile-popover">
            <PopoverDialog aria-label="Account options">
              <p className="profile-menu-name">{session.principal.display_name}</p>
              <p className="muted">{roleLabels[session.principal.role]}</p>
              <Link className="profile-action" to="/profile" onClick={() => setProfileOpen(false)}><UserRound size={18} aria-hidden="true" />My profile</Link>
              <Button variant="ghost" className="profile-action" isDisabled={logout.isPending} onPress={signOut}><LogOut size={18} aria-hidden="true" />{logout.isPending ? 'Signing out…' : 'Sign out'}</Button>
            </PopoverDialog>
          </PopoverContent>
        </Popover>
      </div>}
    </aside>
    <main id="workspace" className="workspace" tabIndex={-1}><Outlet /></main>
  </div>;
}
