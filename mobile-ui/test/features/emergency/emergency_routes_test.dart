import 'package:carelanka_mobile/features/emergency/emergency_routes.dart';
import 'package:carelanka_mobile/services/api_client/models/principal_role.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

void main() {
  group('emergencyHomePathFor', () {
    test('sends ambulance crew straight to My run', () {
      expect(
        emergencyHomePathFor(PrincipalRole.ambulanceCrew),
        EmergencyPaths.myRun,
      );
    });

    test('gives every other role no Emergency home screen', () {
      final others = PrincipalRole.values.where(
        (role) => role != PrincipalRole.ambulanceCrew,
      );

      for (final role in others) {
        expect(
          emergencyHomePathFor(role),
          isNull,
          reason: '$role must not land on a crew screen',
        );
      }
    });
  });

  group('emergencyRoutes', () {
    final paths = emergencyRoutes
        .whereType<GoRoute>()
        .map((route) => route.path)
        .toList();

    test('registers every Emergency screen the app links to', () {
      expect(
        paths,
        containsAll([
          EmergencyPaths.patientReport,
          '${EmergencyPaths.patientTracking}/:id',
          EmergencyPaths.myRun,
          EmergencyPaths.history,
          '${EmergencyPaths.history}/:id',
        ]),
      );
    });

    test('never registers the same path twice', () {
      expect(paths.toSet().length, paths.length);
    });

    test('keeps patient screens under /me and crew screens under /crew', () {
      expect(EmergencyPaths.patientReport, startsWith('/me/'));
      expect(EmergencyPaths.patientTracking, startsWith('/me/'));
      expect(EmergencyPaths.myRun, startsWith('/crew/'));
      expect(EmergencyPaths.history, startsWith('/crew/'));
    });
  });
}
