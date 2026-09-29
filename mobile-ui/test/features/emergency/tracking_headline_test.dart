import 'package:carelanka_mobile/features/emergency/models/tracking_headline.dart';
import 'package:carelanka_mobile/services/api_client/models/call_status.dart';
import 'package:carelanka_mobile/services/api_client/models/my_call_tracking.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('the caller headline', () {
    test('follows the call while it is open', () {
      expect(
        const MyCallTracking(callStatus: CallStatus.received).headline,
        'Request received',
      );
      expect(
        const MyCallTracking(callStatus: CallStatus.dispatched).headline,
        'An ambulance is on the way',
      );
      expect(
        const MyCallTracking(callStatus: CallStatus.cancelled).headline,
        'Request cancelled',
      );
    });

    test('says the crew finished at the scene when nobody was taken', () {
      const tracking = MyCallTracking(
        callStatus: CallStatus.completed,
        transported: false,
      );

      expect(tracking.headline, 'The crew finished at the scene');
    });

    test('says the response completed after a hospital trip', () {
      const tracking = MyCallTracking(
        callStatus: CallStatus.completed,
        transported: true,
      );

      expect(tracking.headline, 'Response completed');
    });

    test('does not guess when it is not known whether anyone was taken', () {
      const tracking = MyCallTracking(callStatus: CallStatus.completed);

      expect(tracking.headline, 'Response completed');
    });

    test('waits quietly before the first answer', () {
      const MyCallTracking? nothing = null;

      expect(nothing.headline, 'Checking your request…');
    });
  });
}
