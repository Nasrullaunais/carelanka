import { useQuery } from '@tanstack/react-query';
import { getEmergencyCallOptions } from '../../../services/api/generated/@tanstack/react-query.gen';
import { StatusChip } from '../../../components/ui/status-chip';
import { priorityLabels, priorityTones, shortAddress } from '../domain';

/** The few facts a duty manager needs to judge a new call before opening it. */
export function CallNotificationDetails({ callId, fallback }: { callId: string; fallback?: string | null }) {
  const call = useQuery({
    ...getEmergencyCallOptions({ path: { id: callId } }),
    // The address is looked up after the call is saved, so it can arrive a few seconds later.
    refetchInterval: (query) => (query.state.data && !query.state.data.address_label ? 5_000 : false),
  });

  if (!call.data) return fallback ? <p className="notification-popup-body">{fallback}</p> : null;

  const { priority, patient_name, caller_name, address_label, details } = call.data;
  return (
    <div className="notification-popup-call">
      <p className="notification-popup-call-who">
        {priority && <StatusChip tone={priorityTones[priority]}>{priorityLabels[priority]}</StatusChip>}
        <strong>{patient_name ?? caller_name ?? 'Unnamed caller'}</strong>
      </p>
      <p className="notification-popup-body">{address_label ? shortAddress(address_label) : 'Address resolving'}</p>
      {details && <p className="notification-popup-body notification-popup-quote">“{details}”</p>}
    </div>
  );
}
