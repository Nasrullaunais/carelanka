import type { InboxNotification, NotificationType } from '../services/api/generated';

export const notificationTypeLabels: Record<NotificationType, string> = {
  dispatch_assigned: 'Dispatch assigned',
  appointment_booked: 'Appointment booked',
  appointment_rescheduled: 'Appointment rescheduled',
  appointment_cancelled: 'Appointment cancelled',
  appointment_reminder: 'Appointment reminder',
  admission_approved: 'Admission approved',
  bed_assigned: 'Bed assigned',
  discharge_ready: 'Discharge ready',
  bill_raised: 'Bill raised',
  bill_settled: 'Bill settled',
  care_reply_ready: 'Care reply ready',
  prescription_ready: 'Prescription ready',
  prescription_delivered: 'Prescription delivered',
  lab_report_ready: 'Lab report ready',
  ambulance_on_the_way: 'Ambulance on the way',
  ambulance_arrived: 'Ambulance arrived',
  cancellation_answered: 'Cancellation answered',
  emergency_call_received: 'Emergency call received',
  cancellation_request_waiting: 'Cancellation request waiting',
  dispatch_proposal_waiting: 'Diversion needs approval',
  dispatch_proposal_failed: 'No ambulance recommended',
  admission_awaiting_approval: 'Admission awaiting approval',
  care_query_flagged: 'Care query flagged',
  care_reply_waiting: 'Care reply waiting',
  equipment_warning_raised: 'Equipment warning raised',
  maintenance_due: 'Maintenance due',
  pharmacy_stock_low: 'Pharmacy stock low',
  lab_test_requested: 'Lab test requested',
  leave_requested: 'Leave requested',
  leave_approved: 'Leave approved',
  leave_rejected: 'Leave rejected',
  shift_changed: 'Shift changed',
  roster_proposal_waiting: 'Roster proposal waiting',
};

// One route per NotificationType so a new type with no route is a compile error, not a dead click.
const notificationRoutes: Record<NotificationType, (entityId: string) => string> = {
  dispatch_assigned: () => '/emergency',
  appointment_booked: () => '/appointments',
  appointment_rescheduled: () => '/appointments',
  appointment_cancelled: () => '/appointments',
  appointment_reminder: () => '/appointments',
  admission_approved: () => '/patients',
  bed_assigned: () => '/patients',
  discharge_ready: () => '/discharge',
  bill_raised: () => '/discharge',
  bill_settled: () => '/discharge',
  care_reply_ready: () => '/care-recommendations',
  prescription_ready: () => '/pharmacy',
  prescription_delivered: () => '/pharmacy',
  lab_report_ready: () => '/laboratory',
  ambulance_on_the_way: () => '/emergency',
  ambulance_arrived: () => '/emergency',
  cancellation_answered: () => '/emergency',
  emergency_call_received: emergencyCall,
  cancellation_request_waiting: () => '/emergency/cancellations',
  dispatch_proposal_waiting: emergencyCall,
  dispatch_proposal_failed: emergencyCall,
  admission_awaiting_approval: () => '/intake',
  care_query_flagged: () => '/care-recommendations',
  care_reply_waiting: () => '/care-recommendations',
  equipment_warning_raised: () => '/warnings',
  maintenance_due: () => '/maintenance-unit',
  pharmacy_stock_low: () => '/pharmacy',
  lab_test_requested: () => '/laboratory',
  leave_requested: () => '/staff/leave-approval',
  leave_approved: () => '/staff',
  leave_rejected: () => '/staff',
  shift_changed: () => '/staff/coverage',
  roster_proposal_waiting: () => '/staff/roster-proposals',
};

function emergencyCall(callId: string): string {
  return callId ? `/emergency/calls/${encodeURIComponent(callId)}` : '/emergency';
}

export function routeForNotification(notification: InboxNotification): string | null {
  if (!notification.type) return null;
  return notificationRoutes[notification.type](notification.entity_id ?? '');
}
