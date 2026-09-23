import 'package:carelanka_mobile/features/emergency/services/crew_location_reporter.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_status.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test(
    'reports a responding ambulance only while its dispatch is live',
    () async {
      final gateway = FakeDispatchGateway(
        const CrewDispatch(
          'dispatch-1',
          'ambulance-1',
          DispatchStatus.enRouteToScene,
        ),
      );
      final reporter = CrewLocationReporter(
        dispatches: gateway,
        location: FakeLocationGateway(const CrewPosition(6.927079, 79.861244)),
        interval: const Duration(milliseconds: 1),
      );

      await reporter.resume();
      await Future<void>.delayed(const Duration(milliseconds: 5));
      await reporter.pause();

      expect(gateway.reports, isNotEmpty);
      expect(
        gateway.reports,
        everyElement(const CrewPosition(6.927079, 79.861244)),
      );
    },
  );

  test(
    'does not report when permission is denied or the run has ended',
    () async {
      final gateway = FakeDispatchGateway(
        const CrewDispatch(
          'dispatch-1',
          'ambulance-1',
          DispatchStatus.handedOver,
        ),
      );
      final reporter = CrewLocationReporter(
        dispatches: gateway,
        location: FakeLocationGateway(const CrewPosition(6.927079, 79.861244)),
        interval: const Duration(milliseconds: 1),
      );

      await reporter.resume();
      expect(reporter.state, CrewLocationReportingState.stopped);
      expect(gateway.reports, isEmpty);
    },
  );

  test('exposes denied permission without starting a report timer', () async {
    final gateway = FakeDispatchGateway(
      const CrewDispatch(
        'dispatch-1',
        'ambulance-1',
        DispatchStatus.enRouteToScene,
      ),
    );
    final reporter = CrewLocationReporter(
      dispatches: gateway,
      location: FakeLocationGateway(
        const CrewPosition(6.927079, 79.861244),
        permission: CrewLocationPermission.denied,
      ),
      interval: const Duration(milliseconds: 1),
    );

    await reporter.resume();
    await Future<void>.delayed(const Duration(milliseconds: 5));

    expect(reporter.state, CrewLocationReportingState.permissionDenied);
    expect(gateway.reports, isEmpty);
  });

  test(
    'stops after a reporting failure instead of retrying a stale run',
    () async {
      final gateway = FakeDispatchGateway(
        const CrewDispatch(
          'dispatch-1',
          'ambulance-1',
          DispatchStatus.enRouteToScene,
        ),
        failReports: true,
      );
      final reporter = CrewLocationReporter(
        dispatches: gateway,
        location: FakeLocationGateway(const CrewPosition(6.927079, 79.861244)),
        interval: const Duration(milliseconds: 1),
      );

      await reporter.resume();
      await Future<void>.delayed(const Duration(milliseconds: 5));

      expect(reporter.state, CrewLocationReportingState.failed);
      expect(gateway.reportAttempts, 1);
    },
  );
}

final class FakeDispatchGateway implements CrewDispatchGateway {
  FakeDispatchGateway(this.active, {this.failReports = false});

  CrewDispatch? active;
  final bool failReports;
  final List<CrewPosition> reports = [];
  int reportAttempts = 0;

  @override
  Future<CrewDispatch?> activeDispatch() async => active;

  @override
  Future<void> report(String ambulanceId, CrewPosition position) async {
    reportAttempts++;
    if (failReports) throw StateError('offline');
    reports.add(position);
  }
}

final class FakeLocationGateway implements CrewLocationGateway {
  FakeLocationGateway(
    this.position, {
    this.permission = CrewLocationPermission.granted,
  });

  final CrewPosition position;
  final CrewLocationPermission permission;

  @override
  Future<CrewLocationPermission> requestPermission() async => permission;

  @override
  Future<CrewPosition> currentPosition() async => position;
}
