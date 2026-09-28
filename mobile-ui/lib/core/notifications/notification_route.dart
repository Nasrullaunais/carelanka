import '../../features/emergency/emergency_routes.dart';
import '../../features/patient/patient_routes.dart';
import '../../services/api_client/models/inbox_notification.dart';
import '../../services/api_client/models/notification_type.dart';

/// One entry per [NotificationType] so a new type with no route is a compile error.
/// A type with no built mobile screen (everything staff-facing - Patient Management,
/// Equipment and Staff have no mobile screens outside this feature) routes to null,
/// same as an unknown type.
const Map<NotificationType, String? Function(InboxNotification)> _routes = {
  NotificationType.dispatchAssigned: _myRun,
  NotificationType.appointmentBooked: _patientHome,
  NotificationType.appointmentRescheduled: _patientHome,
  NotificationType.appointmentCancelled: _patientHome,
  NotificationType.appointmentReminder: _patientHome,
  NotificationType.admissionApproved: _patientHome,
  NotificationType.bedAssigned: _patientHome,
  NotificationType.dischargeReady: _patientHome,
  NotificationType.billRaised: _patientHome,
  NotificationType.billSettled: _patientHome,
  NotificationType.careReplyReady: _patientHome,
  NotificationType.prescriptionReady: _patientHome,
  NotificationType.prescriptionDelivered: _patientHome,
  NotificationType.labReportReady: _patientHome,
  NotificationType.ambulanceOnTheWay: _patientTracking,
  NotificationType.ambulanceArrived: _patientTracking,
  NotificationType.cancellationAnswered: _patientTracking,
  NotificationType.emergencyCallReceived: _noMobileScreen,
  NotificationType.cancellationRequestWaiting: _noMobileScreen,
  NotificationType.dispatchProposalWaiting: _noMobileScreen,
  NotificationType.admissionAwaitingApproval: _noMobileScreen,
  NotificationType.careQueryFlagged: _noMobileScreen,
  NotificationType.careReplyWaiting: _noMobileScreen,
  NotificationType.equipmentWarningRaised: _noMobileScreen,
  NotificationType.maintenanceDue: _noMobileScreen,
  NotificationType.pharmacyStockLow: _noMobileScreen,
  NotificationType.labTestRequested: _noMobileScreen,
  NotificationType.leaveRequested: _noMobileScreen,
  NotificationType.leaveApproved: _noMobileScreen,
  NotificationType.leaveRejected: _noMobileScreen,
  NotificationType.shiftChanged: _noMobileScreen,
  NotificationType.rosterProposalWaiting: _noMobileScreen,
};

String _myRun(InboxNotification notification) => EmergencyPaths.myRun;

String _patientHome(InboxNotification notification) => PatientPaths.home;

String? _patientTracking(InboxNotification notification) {
  final callId = notification.entityId;
  if (callId == null || callId.isEmpty) return PatientPaths.home;
  return '${EmergencyPaths.patientTracking}/$callId';
}

String? _noMobileScreen(InboxNotification notification) => null;

String? routeForNotification(InboxNotification notification) {
  final type = notification.type;
  if (type == null || type == NotificationType.$unknown) return null;
  return _routes[type]?.call(notification);
}

/// Same map, entered from a push payload (`type`/`entity_type`/`entity_id`) instead of an
/// inbox row - a background or cold-start tap never carries the full [InboxNotification].
String? routeForPushData(Map<String, String> data) {
  final typeWire = data['type'];
  if (typeWire == null) return null;

  return routeForNotification(InboxNotification(
    type: NotificationType.fromJson(typeWire),
    entityType: data['entity_type'],
    entityId: data['entity_id'],
  ));
}
