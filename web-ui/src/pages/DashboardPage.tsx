import { ArrowUpRight } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { listCareRecommendationsOptions } from '../services/api/generated/@tanstack/react-query.gen';
import { useSession } from '../services/auth/useSession';
import { canReadCareQueue, roleLabels } from '../types/permissions';
import { destinationsFor } from '../types/navigation';

export function DashboardPage() {
  const session = useSession();
  const role = session?.principal.role;
  const tiles = destinationsFor(role);
  const waiting = useQuery({
    ...listCareRecommendationsOptions({ query: { status: 'pending_review', page: 1, pageSize: 1 } }),
    enabled: canReadCareQueue(role),
    refetchInterval: 30_000,
  });
  const waitingCount = waiting.data?.total_items ?? 0;

  return (
    <>
      <p className="eyebrow">Your workspace</p>
      <h1>Welcome, {session?.principal.display_name ?? 'CareLanka'}</h1>
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
              <div className="tile-heading">
                <tile.icon size={22} aria-hidden="true" />
                {tile.to === '/care-recommendations' && waitingCount > 0 && (
                  <span className="badge severity-high" aria-label={`${waitingCount} waiting for a reply`}>
                    {waitingCount}
                  </span>
                )}
                <ArrowUpRight size={17} aria-hidden="true" />
              </div>
              <strong>{tile.label}</strong>
              <span>{tile.description}</span>
            </Link>
          ))}
        </div>
      )}
    </>
  );
}
