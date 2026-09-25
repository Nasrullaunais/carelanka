import { Button, Card, CardContent, CardTitle } from '@heroui/react';
import type { AmbulanceSummary, ListAmbulancesError } from '../../../services/api/generated';
import { QueryError, QuerySkeleton } from '../../../components/ui/query-state';
import { blockReasonLabels, formatDriveMinutes, formatKilometres } from '../domain';

export function EligibleAmbulanceList({ ambulances, isLoading, error, dispatchingId, isDisabled = false, onDispatch, onRetry }: {
  ambulances: AmbulanceSummary[] | undefined;
  isLoading: boolean;
  error: ListAmbulancesError | null;
  dispatchingId?: string;
  isDisabled?: boolean;
  onDispatch: (id: string) => void;
  onRetry: () => void;
}) {
  if (isLoading) return <QuerySkeleton />;
  if (error != null) return <QueryError error={error} context="Could not check ambulance eligibility." onRetry={onRetry} />;
  if (!ambulances?.length) return <p className="muted">No ambulances are registered.</p>;

  return (
    <div className="flex flex-col gap-2">
      {ambulances.map((ambulance) => (
        <Card key={ambulance.id}>
          <CardContent className="flex flex-row items-center justify-between gap-4">
            <div>
              <CardTitle>{ambulance.registration_number}</CardTitle>
              <p className="text-sm text-muted">
                Crew {ambulance.current_crew_count ?? 0}/{ambulance.required_crew_count ?? 2}
                {ambulance.distance_km != null && ` · ${formatKilometres(ambulance.distance_km)}`}
                {ambulance.drive_minutes != null
                  ? ` · ${formatDriveMinutes(ambulance.drive_minutes)}`
                  : ambulance.distance_km != null && ambulance.is_straight_line_distance
                    ? ' · straight-line estimate; road time unavailable'
                    : ''}
              </p>
              {!ambulance.is_eligible && (
                <p className="text-sm text-danger">
                  {(ambulance.eligibility_block_reasons ?? []).map((reason) => blockReasonLabels[reason]).join(' · ') || 'Not eligible'}
                </p>
              )}
            </div>
            <Button
              isDisabled={isDisabled || !ambulance.is_eligible || dispatchingId !== undefined}
              onPress={() => onDispatch(ambulance.id)}
            >
              {dispatchingId === ambulance.id ? 'Dispatching…' : 'Dispatch'}
            </Button>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
