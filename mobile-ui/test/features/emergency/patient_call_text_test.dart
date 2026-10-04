import 'package:carelanka_mobile/features/emergency/models/patient_call_text.dart';
import 'package:carelanka_mobile/services/api_client/models/call_status.dart';
import 'package:carelanka_mobile/services/api_client/models/cancellation_request_status.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_status.dart';
import 'package:carelanka_mobile/services/api_client/models/emergency_call_outcome.dart';
import 'package:carelanka_mobile/services/api_client/models/my_call_tracking.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('says an ambulance is coming only once the crew is driving', () {
    expect(
      trackingStage(
        const MyCallTracking(
          callStatus: CallStatus.dispatched,
          dispatchStatus: DispatchStatus.assigned,
        ),
      ).title,
      'Ambulance found',
    );
    expect(
      trackingStage(
        const MyCallTracking(
          callStatus: CallStatus.enRoute,
          dispatchStatus: DispatchStatus.enRouteToScene,
          ambulanceRegistration: 'WP-CA-1234',
        ),
      ).message,
      'WP-CA-1234 is driving to you.',
    );
  });

  test('explains a declined run instead of silently going back', () {
    final stage = trackingStage(
      const MyCallTracking(
        callStatus: CallStatus.received,
        lookingForAnotherAmbulance: true,
      ),
    );

    expect(stage.title, 'Finding another ambulance');
  });

  test('a closed request says how it ended', () {
    final stage = trackingStage(
      const MyCallTracking(
        callStatus: CallStatus.cancelled,
        outcome: EmergencyCallOutcome.duplicateCall,
      ),
    );

    expect(stage.title, 'Request closed');
    expect(stage.message, 'Duplicate call');
  });

  test('every cancellation state reads as a sentence, never a code', () {
    for (final status in CancellationRequestStatus.values) {
      final text = cancellationText(status);
      if (status == CancellationRequestStatus.$unknown) {
        expect(text, isNull);
      } else {
        expect(text, isNot(contains('CancellationRequestStatus')));
        expect(text, endsWith('.'));
      }
    }
    expect(callStatusLabel(CallStatus.enRoute), 'Ambulance on the way');
  });
}
