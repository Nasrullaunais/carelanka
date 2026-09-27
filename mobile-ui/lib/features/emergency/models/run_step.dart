import '../../../services/api_client/models/dispatch_status.dart';

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

  bool get canDecline => this == DispatchStatus.assigned;

  bool get canNavigate => isLive && this != DispatchStatus.assigned;

  String get crewLabel => switch (this) {
    DispatchStatus.assigned => 'Waiting for you to accept',
    DispatchStatus.acknowledged => 'Accepted',
    DispatchStatus.enRouteToScene => 'On the way to the scene',
    DispatchStatus.atScene => 'At the scene',
    DispatchStatus.transportingToHospital => 'Taking the patient to hospital',
    DispatchStatus.handedOver => 'Handed over',
    DispatchStatus.declined => 'Declined',
    DispatchStatus.cancelled => 'Cancelled',
    DispatchStatus.reassigned => 'Given to another ambulance',
    DispatchStatus.$unknown => 'Unknown',
  };
}
