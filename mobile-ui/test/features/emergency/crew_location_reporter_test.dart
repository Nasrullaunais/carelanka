import 'dart:async';

import 'package:carelanka_mobile/features/emergency/services/crew_location_reporter.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('picks up a new crew assignment without reopening the screen', () async {
    final gateway = FakeDispatchGateway(null);
    final reporter = CrewLocationReporter(
      dispatches: gateway,
      location: FakeLocationGateway(const CrewPosition(6.927079, 79.861244)),
      interval: const Duration(milliseconds: 1),
    );
    await reporter.resume();
    expect(gateway.reports, isEmpty);
    gateway.active = 'ambulance-1';
    await Future<void>.delayed(const Duration(milliseconds: 10));
    expect(gateway.reports, isNotEmpty);
    reporter.dispose();
  });

  test(
    'pausing while GPS is pending prevents a late location upload',
    () async {
      final gateway = FakeDispatchGateway('ambulance-1');
      final location = PendingLocationGateway();
      final reporter = CrewLocationReporter(
        dispatches: gateway,
        location: location,
      );
      final resumed = reporter.resume();
      await location.requested.future;
      await reporter.pause();
      location.position.complete(const CrewPosition(6.927079, 79.861244));
      await resumed;
      expect(gateway.reports, isEmpty);
      expect(reporter.state, CrewLocationReportingState.stopped);
      reporter.dispose();
    },
  );

  test('reports the assigned ambulance before or during a dispatch', () async {
    final gateway = FakeDispatchGateway('ambulance-1');
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
  });

  test('does not report without an assigned ambulance', () async {
    final gateway = FakeDispatchGateway(null);
    final reporter = CrewLocationReporter(
      dispatches: gateway,
      location: FakeLocationGateway(const CrewPosition(6.927079, 79.861244)),
      interval: const Duration(milliseconds: 1),
    );

    await reporter.resume();
    expect(reporter.state, CrewLocationReportingState.stopped);
    expect(gateway.reports, isEmpty);
    reporter.dispose();
  });

  test('exposes denied permission without starting a report timer', () async {
    final gateway = FakeDispatchGateway('ambulance-1');
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
    'recovers after a transient reporting failure and rechecks assignment',
    () async {
      final gateway = FakeDispatchGateway('ambulance-1', failReports: true);
      final reporter = CrewLocationReporter(
        dispatches: gateway,
        location: FakeLocationGateway(const CrewPosition(6.927079, 79.861244)),
        interval: const Duration(milliseconds: 1),
      );

      await reporter.resume();
      await Future<void>.delayed(const Duration(milliseconds: 5));

      expect(reporter.state, CrewLocationReportingState.failed);
      expect(gateway.reportAttempts, greaterThanOrEqualTo(1));
      gateway.failReports = false;
      await Future<void>.delayed(const Duration(milliseconds: 10));
      expect(reporter.state, CrewLocationReportingState.reporting);
      expect(gateway.reports, isNotEmpty);
      reporter.dispose();
    },
  );
}

final class FakeDispatchGateway implements CrewDispatchGateway {
  FakeDispatchGateway(this.active, {this.failReports = false});

  String? active;
  bool failReports;
  final List<CrewPosition> reports = [];
  int reportAttempts = 0;

  @override
  Future<String?> assignedAmbulanceId() async => active;

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

final class PendingLocationGateway implements CrewLocationGateway {
  final requested = Completer<void>();
  final position = Completer<CrewPosition>();

  @override
  Future<CrewLocationPermission> requestPermission() async =>
      CrewLocationPermission.granted;

  @override
  Future<CrewPosition> currentPosition() {
    requested.complete();
    return position.future;
  }
}
