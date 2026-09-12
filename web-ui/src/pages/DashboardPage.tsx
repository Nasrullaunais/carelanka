import { Link } from 'react-router-dom';
import { useSession } from '../services/auth/useSession';
import { roleLabels } from '../types/permissions';
import { destinationsFor } from '../types/navigation';

// The landing page. Seven roles can sign in and no two of them do the same job, so rather
// than one navigation bar carrying every screen and greying out most of it, each person is
// shown the ones they can actually open.

export function DashboardPage() {
  const session = useSession();
  const role = session?.principal.role;
  const tiles = destinationsFor(role);

  return (
    <>
      <h1>{session?.principal.display_name ?? 'CareLanka'}</h1>
      <p className="muted">
        {role ? roleLabels[role] : 'Staff'} — {tiles.length} screen
        {tiles.length === 1 ? '' : 's'} available to your role.
      </p>

      {tiles.length === 0 ? (
        <div className="card">
          <p className="empty">
            Your role has no screens in this app yet. This is a gap in the build rather than a
            permissions problem — report it to the owner of your component.
          </p>
        </div>
      ) : (
        <div className="tiles">
          {tiles.map((tile) => (
            <Link key={tile.to} to={tile.to} className="tile">
              <strong>{tile.label}</strong>
              <span>{tile.description}</span>
            </Link>
          ))}
        </div>
      )}
    </>
  );
}
