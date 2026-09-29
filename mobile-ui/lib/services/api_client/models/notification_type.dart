// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

@JsonEnum()
enum NotificationType {
  @JsonValue('dispatch_assigned')
  dispatchAssigned('dispatch_assigned'),
  @JsonValue('appointment_booked')
  appointmentBooked('appointment_booked'),
  @JsonValue('appointment_rescheduled')
  appointmentRescheduled('appointment_rescheduled'),
  @JsonValue('appointment_cancelled')
  appointmentCancelled('appointment_cancelled'),
  @JsonValue('appointment_reminder')
  appointmentReminder('appointment_reminder'),
  @JsonValue('admission_approved')
  admissionApproved('admission_approved'),
  @JsonValue('bed_assigned')
  bedAssigned('bed_assigned'),
  @JsonValue('discharge_ready')
  dischargeReady('discharge_ready'),
  @JsonValue('bill_raised')
  billRaised('bill_raised'),
  @JsonValue('bill_settled')
  billSettled('bill_settled'),
  @JsonValue('care_reply_ready')
  careReplyReady('care_reply_ready'),
  @JsonValue('prescription_ready')
  prescriptionReady('prescription_ready'),
  @JsonValue('prescription_delivered')
  prescriptionDelivered('prescription_delivered'),
  @JsonValue('lab_report_ready')
  labReportReady('lab_report_ready'),
  @JsonValue('ambulance_on_the_way')
  ambulanceOnTheWay('ambulance_on_the_way'),
  @JsonValue('ambulance_arrived')
  ambulanceArrived('ambulance_arrived'),
  @JsonValue('cancellation_answered')
  cancellationAnswered('cancellation_answered'),
  @JsonValue('emergency_call_received')
  emergencyCallReceived('emergency_call_received'),
  @JsonValue('cancellation_request_waiting')
  cancellationRequestWaiting('cancellation_request_waiting'),
  @JsonValue('dispatch_proposal_waiting')
  dispatchProposalWaiting('dispatch_proposal_waiting'),
  @JsonValue('dispatch_proposal_failed')
  dispatchProposalFailed('dispatch_proposal_failed'),
  @JsonValue('admission_awaiting_approval')
  admissionAwaitingApproval('admission_awaiting_approval'),
  @JsonValue('care_query_flagged')
  careQueryFlagged('care_query_flagged'),
  @JsonValue('care_reply_waiting')
  careReplyWaiting('care_reply_waiting'),
  @JsonValue('equipment_warning_raised')
  equipmentWarningRaised('equipment_warning_raised'),
  @JsonValue('maintenance_due')
  maintenanceDue('maintenance_due'),
  @JsonValue('pharmacy_stock_low')
  pharmacyStockLow('pharmacy_stock_low'),
  @JsonValue('lab_test_requested')
  labTestRequested('lab_test_requested'),
  @JsonValue('leave_requested')
  leaveRequested('leave_requested'),
  @JsonValue('leave_approved')
  leaveApproved('leave_approved'),
  @JsonValue('leave_rejected')
  leaveRejected('leave_rejected'),
  @JsonValue('shift_changed')
  shiftChanged('shift_changed'),
  @JsonValue('roster_proposal_waiting')
  rosterProposalWaiting('roster_proposal_waiting'),
  /// Default value for all unparsed values, allows backward compatibility when adding new values on the backend.
  $unknown(null);

  const NotificationType(this.json);

  factory NotificationType.fromJson(String json) => values.firstWhere(
        (e) => e.json == json,
        orElse: () => $unknown,
      );

  final String? json;
  String toJson() {
    final value = json;
    if (value == null) {
      throw StateError('Cannot convert enum value with null JSON representation to String. '
          'This usually happens for \$unknown or @JsonValue(null) entries.');
    }
    return value as String;
  }

  @override
  String toString() => json?.toString() ?? super.toString();
  /// Returns all defined enum values excluding the $unknown value.
  static List<NotificationType> get $valuesDefined => values.where((value) => value != $unknown).toList();
}
