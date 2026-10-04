import '../../../services/api_client/models/call_priority.dart';
import '../../../services/api_client/models/dispatch_status.dart';
import '../../../services/api_client/models/emergency_call_outcome.dart';

enum RunStep {
  acknowledge('Accept this run'),
  startDriving('Start driving to the scene'),
  arrivedAtScene('I have reached the scene'),
  leaveForHospital('Patient on board, going to hospital'),
  handOver('Hand over at the hospital');

  const RunStep(this.label);

  final String label;

  DispatchStatus? get nextStatus => switch (this) {
    RunStep.startDriving => DispatchStatus.enRouteToScene,
    RunStep.arrivedAtScene => DispatchStatus.atScene,
    RunStep.leaveForHospital => DispatchStatus.transportingToHospital,
    _ => null,
  };
}

extension DispatchStatusRun on DispatchStatus {
  bool get isLive => switch (this) {
    DispatchStatus.assigned ||
    DispatchStatus.acknowledged ||
    DispatchStatus.enRouteToScene ||
    DispatchStatus.atScene ||
    DispatchStatus.transportingToHospital => true,
    _ => false,
  };

  RunStep? get nextStep => switch (this) {
    DispatchStatus.assigned => RunStep.acknowledge,
    DispatchStatus.acknowledged => RunStep.startDriving,
    DispatchStatus.enRouteToScene => RunStep.arrivedAtScene,
    DispatchStatus.atScene => RunStep.leaveForHospital,
    DispatchStatus.transportingToHospital => RunStep.handOver,
    _ => null,
  };

  bool get isPrePickup => switch (this) {
    DispatchStatus.assigned ||
    DispatchStatus.acknowledged ||
    DispatchStatus.enRouteToScene => true,
    _ => false,
  };

  bool get canDecline => this == DispatchStatus.assigned;

  bool get canEndAtScene => this == DispatchStatus.atScene;

  bool get canNavigate => isLive && this != DispatchStatus.assigned;

  String get crewLabel => switch (this) {
    DispatchStatus.assigned => 'Waiting for you to accept',
    DispatchStatus.acknowledged => 'Accepted',
    DispatchStatus.enRouteToScene => 'On the way to the scene',
    DispatchStatus.atScene => 'At the scene',
    DispatchStatus.transportingToHospital => 'Taking the patient to hospital',
    DispatchStatus.handedOver => 'Handed over',
    DispatchStatus.closedAtScene => 'Ended at the scene',
    DispatchStatus.declined => 'Declined',
    DispatchStatus.cancelled => 'Cancelled',
    DispatchStatus.reassigned => 'Given to another ambulance',
    DispatchStatus.$unknown => 'Unknown',
  };
}

/// The ways a crew can end a run without driving to hospital, in the order
/// they are offered. The server accepts exactly these.
const sceneOutcomes = [
  EmergencyCallOutcome.treatedAtScene,
  EmergencyCallOutcome.refusedTransport,
  EmergencyCallOutcome.patientNotFound,
  EmergencyCallOutcome.deceasedAtScene,
];

extension EmergencyCallOutcomeLabel on EmergencyCallOutcome {
  String get label => switch (this) {
    EmergencyCallOutcome.transported => 'Taken to hospital',
    EmergencyCallOutcome.treatedAtScene => 'Treated at the scene',
    EmergencyCallOutcome.refusedTransport => 'Patient refused to come',
    EmergencyCallOutcome.patientNotFound => 'Nobody found at the scene',
    EmergencyCallOutcome.deceasedAtScene => 'Patient died at the scene',
    EmergencyCallOutcome.falseAlarm => 'False alarm',
    EmergencyCallOutcome.duplicateCall => 'Duplicate call',
    EmergencyCallOutcome.callerCancelled => 'Caller cancelled',
    EmergencyCallOutcome.noLongerNeeded => 'No longer needed',
    EmergencyCallOutcome.$unknown => 'Unknown',
  };
}

extension CallPriorityLabel on CallPriority {
  String get label => switch (this) {
    CallPriority.critical => 'Critical',
    CallPriority.high => 'High',
    CallPriority.medium => 'Medium',
    CallPriority.low => 'Low',
    CallPriority.$unknown => 'Unknown',
  };
}
