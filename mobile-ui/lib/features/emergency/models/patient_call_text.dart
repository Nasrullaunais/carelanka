import '../../../services/api_client/models/call_status.dart';
import '../../../services/api_client/models/cancellation_request_status.dart';
import '../../../services/api_client/models/dispatch_status.dart';
import '../../../services/api_client/models/my_call_tracking.dart';
import 'run_step.dart';

typedef TrackingStage = ({String title, String message});

bool isOpenCall(CallStatus? status) =>
    status == CallStatus.received ||
    status == CallStatus.dispatched ||
    status == CallStatus.enRoute;

/// What the caller is told, in the order a request moves through.
TrackingStage trackingStage(MyCallTracking tracking) {
  final ambulance = tracking.ambulanceRegistration;
  final outcome = tracking.outcome?.label;
  return switch ((tracking.callStatus, tracking.dispatchStatus)) {
    (CallStatus.completed, _) => (
      title: 'Response completed',
      message: outcome ?? 'The ambulance crew has finished.',
    ),
    (CallStatus.cancelled, _) => (
      title: 'Request closed',
      message: outcome ?? 'No ambulance is coming for this request.',
    ),
    (_, DispatchStatus.assigned) => (
      title: 'Ambulance found',
      message:
          'Waiting for the crew to accept. This usually takes under a minute.',
    ),
    (_, DispatchStatus.acknowledged) => (
      title: 'Crew accepted',
      message:
          'The crew of ${ambulance ?? 'the ambulance'} is getting ready to leave.',
    ),
    (_, DispatchStatus.enRouteToScene) => (
      title: 'Ambulance on the way',
      message: '${ambulance ?? 'The ambulance'} is driving to you.',
    ),
    (_, DispatchStatus.atScene) => (
      title: 'The ambulance has arrived',
      message: 'The crew is with the patient.',
    ),
    (_, DispatchStatus.transportingToHospital) => (
      title: 'On the way to hospital',
      message: 'The crew is taking the patient to hospital.',
    ),
    _ when tracking.lookingForAnotherAmbulance == true => (
      title: 'Finding another ambulance',
      message:
          'The first ambulance could not come. We are sending a different one.',
    ),
    (CallStatus.received, _) => (
      title: 'Request received',
      message: 'We are finding the nearest free ambulance.',
    ),
    _ => (title: 'Checking your request…', message: ''),
  };
}

String callStatusLabel(CallStatus? status) => switch (status) {
  CallStatus.received => 'Waiting for an ambulance',
  CallStatus.dispatched => 'Ambulance sent',
  CallStatus.enRoute => 'Ambulance on the way',
  CallStatus.completed => 'Completed',
  CallStatus.cancelled => 'Closed',
  _ => 'Ambulance request',
};

String? cancellationText(CancellationRequestStatus? status) => switch (status) {
  CancellationRequestStatus.pending =>
    'You asked to cancel. The duty manager is checking it. The ambulance keeps coming until they agree.',
  CancellationRequestStatus.approved =>
    'Your cancellation was accepted. The ambulance has been called back.',
  CancellationRequestStatus.rejected =>
    'The duty manager kept the ambulance coming.',
  CancellationRequestStatus.expired =>
    'Your cancellation request closed without a decision, because this request has already ended.',
  _ => null,
};
