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

  test('stopping before the first GPS fix prevents a late upload', () async {
    final gateway = FakeDispatchGateway('ambulance-1');
    final location = StreamLocationGateway();
    final reporter = CrewLocationReporter(
      dispatches: gateway,
      location: location,
    );
    await reporter.resume();
    await reporter.stop();
    expect(location.listening, isFalse);
    location.emit(const CrewPosition(6.927079, 79.861244));
    await Future<void>.delayed(Duration.zero);
    expect(gateway.reports, isEmpty);
    expect(reporter.state, CrewLocationReportingState.stopped);
    reporter.dispose();
  });

  test(
    'reports a new fix straight away, then keeps re-sending the latest',
    () async {
      final gateway = FakeDispatchGateway('ambulance-1');
      final location = StreamLocationGateway();
      final reporter = CrewLocationReporter(
        dispatches: gateway,
        location: location,
        interval: const Duration(milliseconds: 5),
      );
      await reporter.resume();
      expect(gateway.reports, isEmpty);

      location.emit(const CrewPosition(6.9, 79.8));
      await Future<void>.delayed(Duration.zero);
      expect(gateway.reports, [const CrewPosition(6.9, 79.8)]);

      location.emit(const CrewPosition(6.95, 79.85));
      await Future<void>.delayed(const Duration(milliseconds: 20));
      expect(gateway.reports.last, const CrewPosition(6.95, 79.85));
      expect(reporter.state, CrewLocationReportingState.reporting);
      reporter.dispose();
      expect(location.listening, isFalse);
    },
  );

  test(
    'a GPS error stops re-sending the old position until a new fix arrives',
    () async {
      final gateway = FakeDispatchGateway('ambulance-1');
      final location = StreamLocationGateway();
      final reporter = CrewLocationReporter(
        dispatches: gateway,
        location: location,
        interval: const Duration(milliseconds: 5),
      );
      await reporter.resume();
      location.emit(const CrewPosition(6.8, 79.7));
      await Future<void>.delayed(Duration.zero);
      location.fail();
      await Future<void>.delayed(Duration.zero);
      expect(reporter.state, CrewLocationReportingState.failed);
      final sentBeforeFailure = gateway.reports.length;
      await Future<void>.delayed(const Duration(milliseconds: 20));
      expect(
        gateway.reports,
        hasLength(sentBeforeFailure),
        reason: 'an old position must not be re-sent as if it were new',
      );

      location.emit(const CrewPosition(6.9, 79.8));
      await Future<void>.delayed(const Duration(milliseconds: 20));
      expect(reporter.state, CrewLocationReportingState.reporting);
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
    await reporter.stop();

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
  Stream<CrewPosition> positions() => Stream.value(position);

  @override
  Future<bool> openAppSettings() async => true;

  @override
  Future<bool> openLocationSettings() async => true;
}

final class StreamLocationGateway implements CrewLocationGateway {
  final _fixes = StreamController<CrewPosition>.broadcast();

  bool get listening => _fixes.hasListener;

  void emit(CrewPosition position) => _fixes.add(position);

  void fail() => _fixes.addError(StateError('GPS lost'));

  @override
  Future<CrewLocationPermission> requestPermission() async =>
      CrewLocationPermission.granted;

  @override
  Stream<CrewPosition> positions() => _fixes.stream;

  @override
  Future<bool> openAppSettings() async => true;

  @override
  Future<bool> openLocationSettings() async => true;
}
