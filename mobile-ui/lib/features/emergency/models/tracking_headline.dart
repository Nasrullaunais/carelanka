import '../../../services/api_client/models/call_status.dart';
import '../../../services/api_client/models/my_call_tracking.dart';

extension TrackingHeadline on MyCallTracking? {
  String get headline => switch (this?.callStatus) {
    CallStatus.received => 'Request received',
    CallStatus.dispatched => 'An ambulance is on the way',
    CallStatus.enRoute => 'Ambulance response in progress',
    CallStatus.completed =>
      this?.transported == false
          ? 'The crew finished at the scene'
          : 'Response completed',
    CallStatus.cancelled => 'Request cancelled',
    _ => 'Checking your request…',
  };
}
