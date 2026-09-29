import 'dart:async';

import 'package:carelanka_mobile/features/emergency/services/crew_location_reporter.dart';
import 'package:flutter_test/flutter_test.dart';

const _here = CrewPosition(6.927079, 79.861244);
const _there = CrewPosition(6.93, 79.87);
const _run = CrewReportingMode.run('AMB-3');
const _foreground = CrewReportingMode.foreground();

void main() {
  CrewLocationReporter build(
    FakeDispatchGateway dispatches,
    FakeLocationGateway location, {
    Duration minUploadGap = const Duration(milliseconds: 1),
    Duration heartbeat = const Duration(seconds: 30),
    Duration retryDelay = const Duration(seconds: 30),
  }) => CrewLocationReporter(
    dispatches: dispatches,
    location: location,
    minUploadGap: minUploadGap,
    heartbeat: heartbeat,
    retryDelay: retryDelay,
  );

  test('sends the first position straight away', () async {
    final dispatches = FakeDispatchGateway('ambulance-1');
    final reporter = build(dispatches, FakeLocationGateway());

    await reporter.resume();
    await settle();

    expect(dispatches.reports, [_here]);
    expect(reporter.state, CrewLocationReportingState.reporting);
    reporter.dispose();
  });

  test('a new point waiting behind an upload replaces older ones', () async {
    final dispatches = FakeDispatchGateway('ambulance-1')..holdReports();
    final location = FakeLocationGateway();
    final reporter = build(dispatches, location);
    await reporter.resume();
    await settle();
    expect(dispatches.reportAttempts, 1);

    location.emit(const CrewPosition(1, 1));
    location.emit(const CrewPosition(2, 2));
    location.emit(_there);
    await settle();
    expect(dispatches.reportAttempts, 1);

    dispatches.releaseReports();
    await settle();

    expect(dispatches.reports, [_here, _there]);
    expect(dispatches.mostAtOnce, 1);
    reporter.dispose();
  });

  test('uploads no more often than the minimum gap', () async {
    final dispatches = FakeDispatchGateway('ambulance-1');
    final location = FakeLocationGateway();
    final reporter = build(
      dispatches,
      location,
      minUploadGap: const Duration(milliseconds: 60),
    );
    await reporter.resume();
    await settle();

    location
      ..emit(const CrewPosition(1, 1))
      ..emit(const CrewPosition(2, 2))
      ..emit(_there);
    await settle();
    expect(dispatches.reports, [_here]);

    await settle(100);
    expect(dispatches.reports, [_here, _there]);
    reporter.dispose();
  });

  test('sends a fresh position after a silence', () async {
    final dispatches = FakeDispatchGateway('ambulance-1');
    final location = FakeLocationGateway();
    final reporter = build(
      dispatches,
      location,
      heartbeat: const Duration(milliseconds: 30),
    );
    await reporter.resume();
    await settle();

    location.current = _there;
    await settle(80);

    expect(dispatches.reports, contains(_there));
    reporter.dispose();
  });

  test('does not share anything without an assigned ambulance', () async {
    final dispatches = FakeDispatchGateway(null);
    final location = FakeLocationGateway();
    final reporter = build(dispatches, location);

    await reporter.resume();
    await settle();

    expect(reporter.state, CrewLocationReportingState.stopped);
    expect(location.streamsOpened, 0);
    expect(dispatches.reports, isEmpty);
    reporter.dispose();
  });

  test('picks up a new assignment without reopening the screen', () async {
    final dispatches = FakeDispatchGateway(null);
    final reporter = build(
      dispatches,
      FakeLocationGateway(),
      retryDelay: const Duration(milliseconds: 20),
    );
    await reporter.resume();
    expect(dispatches.reports, isEmpty);

    dispatches.active = 'ambulance-1';
    await settle(80);

    expect(dispatches.reports, isNotEmpty);
    reporter.dispose();
  });

  test('stops and looks again when the crew loses the ambulance', () async {
    final dispatches = FakeDispatchGateway('ambulance-1');
    final location = FakeLocationGateway();
    final reporter = build(dispatches, location);
    await reporter.resume();
    await settle();

    dispatches.active = null;
    dispatches.failReports = true;
    location.emit(_there);
    await settle();
    location.emit(const CrewPosition(3, 3));
    await settle(30);

    expect(location.streamsCancelled, 1);
    expect(reporter.state, CrewLocationReportingState.stopped);
    reporter.dispose();
  });

  test('shows why nothing is shared when permission is missing', () async {
    final dispatches = FakeDispatchGateway('ambulance-1');
    final reporter = build(
      dispatches,
      FakeLocationGateway(permission: CrewLocationPermission.denied),
    );

    await reporter.resume();
    await settle();

    expect(reporter.state, CrewLocationReportingState.permissionDenied);
    expect(dispatches.reports, isEmpty);
    reporter.dispose();
  });

  test('recovers from a failed upload and re-checks the assignment', () async {
    final dispatches = FakeDispatchGateway('ambulance-1', failReports: true);
    final location = FakeLocationGateway();
    final reporter = build(dispatches, location);
    await reporter.resume();
    await settle();
    expect(reporter.state, CrewLocationReportingState.failed);
    final lookups = dispatches.assignmentLookups;

    dispatches.failReports = false;
    location.emit(_there);
    await settle();

    expect(reporter.state, CrewLocationReportingState.reporting);
    expect(dispatches.assignmentLookups, greaterThan(lookups));
    reporter.dispose();
  });

  test('pausing while permission is pending prevents a late upload', () async {
    final dispatches = FakeDispatchGateway('ambulance-1');
    final location = FakeLocationGateway()..holdPermission();
    final reporter = build(dispatches, location);

    final resumed = reporter.resume();
    await settle();
    await reporter.pause();
    location.releasePermission();
    await resumed;
    await settle();

    expect(dispatches.reports, isEmpty);
    expect(location.streamsOpened, 0);
    expect(reporter.state, CrewLocationReportingState.stopped);
    reporter.dispose();
  });

  group('without a live run', () {
    test('leaving the screen stops sharing', () async {
      final location = FakeLocationGateway();
      final reporter = build(FakeDispatchGateway('ambulance-1'), location);
      await reporter.resume();
      await settle();

      await reporter.pause();

      expect(location.streamsCancelled, 1);
      expect(reporter.state, CrewLocationReportingState.stopped);
      reporter.dispose();
    });

    test('the stream is opened without background tracking', () async {
      final location = FakeLocationGateway();
      final reporter = build(FakeDispatchGateway('ambulance-1'), location);

      await reporter.resume();

      expect(location.modes, [_foreground]);
      reporter.dispose();
    });
  });

  group('on a live run', () {
    test('switching to a run reopens the stream for background use', () async {
      final location = FakeLocationGateway();
      final reporter = build(FakeDispatchGateway('ambulance-1'), location);
      await reporter.resume();

      await reporter.useMode(_run);

      expect(location.modes, [_foreground, _run]);
      expect(location.streamsCancelled, 1);
      reporter.dispose();
    });

    test('keeps sharing when the app leaves the screen', () async {
      final dispatches = FakeDispatchGateway('ambulance-1');
      final location = FakeLocationGateway();
      final reporter = build(dispatches, location);
      await reporter.resume();
      await reporter.useMode(_run);

      await reporter.pause();
      location.emit(_there);
      await settle();

      expect(location.streamsCancelled, 1);
      expect(dispatches.reports, contains(_there));
      reporter.dispose();
    });

    test('coming back to the screen does not reopen the stream', () async {
      final location = FakeLocationGateway();
      final reporter = build(FakeDispatchGateway('ambulance-1'), location);
      await reporter.resume();
      await reporter.useMode(_run);
      await reporter.pause();

      await reporter.resume();

      expect(location.streamsOpened, 2);
      reporter.dispose();
    });

    test('stops when the run ends while the app is out of view', () async {
      final location = FakeLocationGateway();
      final reporter = build(FakeDispatchGateway('ambulance-1'), location);
      await reporter.resume();
      await reporter.useMode(_run);
      await reporter.pause();

      await reporter.useMode(_foreground);

      expect(location.streamsCancelled, 2);
      expect(reporter.state, CrewLocationReportingState.stopped);
      reporter.dispose();
    });

    test(
      'keeps sharing in the foreground when the run ends on screen',
      () async {
        final location = FakeLocationGateway();
        final reporter = build(FakeDispatchGateway('ambulance-1'), location);
        await reporter.resume();
        await reporter.useMode(_run);

        await reporter.useMode(_foreground);

        expect(location.modes, [_foreground, _run, _foreground]);
        expect(location.streamsCancelled, 2);
        reporter.dispose();
      },
    );

    test('logging out stops sharing even on a run', () async {
      final location = FakeLocationGateway();
      final reporter = build(FakeDispatchGateway('ambulance-1'), location);
      await reporter.resume();
      await reporter.useMode(_run);

      await reporter.stop();

      expect(location.streamsCancelled, 2);
      expect(reporter.state, CrewLocationReportingState.stopped);
      reporter.dispose();
    });

    test('shows the permission banner when access is taken away', () async {
      final location = FakeLocationGateway();
      final reporter = build(FakeDispatchGateway('ambulance-1'), location);
      await reporter.resume();
      await reporter.useMode(_run);

      location.permission = CrewLocationPermission.denied;
      location.fail();
      await settle();

      expect(reporter.state, CrewLocationReportingState.permissionDenied);
      reporter.dispose();
    });

    test('resumes by itself when location is switched back on', () async {
      final dispatches = FakeDispatchGateway('ambulance-1');
      final location = FakeLocationGateway();
      final reporter = build(
        dispatches,
        location,
        retryDelay: const Duration(milliseconds: 20),
      );
      await reporter.resume();
      await reporter.useMode(_run);

      location.permission = CrewLocationPermission.unavailable;
      location.fail();
      await settle();
      expect(reporter.state, CrewLocationReportingState.unavailable);

      location.permission = CrewLocationPermission.granted;
      location.current = _there;
      await settle(80);

      expect(reporter.state, CrewLocationReportingState.reporting);
      expect(dispatches.reports, contains(_there));
      reporter.dispose();
    });
  });
}

Future<void> settle([int milliseconds = 15]) =>
    Future<void>.delayed(Duration(milliseconds: milliseconds));

final class FakeDispatchGateway implements CrewDispatchGateway {
  FakeDispatchGateway(this.active, {this.failReports = false});

  String? active;
  bool failReports;
  final List<CrewPosition> reports = [];
  int reportAttempts = 0;
  int assignmentLookups = 0;
  int mostAtOnce = 0;
  int _inFlight = 0;
  Completer<void>? _hold;

  void holdReports() => _hold = Completer<void>();

  void releaseReports() {
    _hold?.complete();
    _hold = null;
  }

  @override
  Future<String?> assignedAmbulanceId() async {
    assignmentLookups++;
    return active;
  }

  @override
  Future<void> report(String ambulanceId, CrewPosition position) async {
    reportAttempts++;
    _inFlight++;
    if (_inFlight > mostAtOnce) mostAtOnce = _inFlight;
    try {
      await _hold?.future;
      if (failReports) throw StateError('offline');
      reports.add(position);
    } finally {
      _inFlight--;
    }
  }
}

final class FakeLocationGateway implements CrewLocationGateway {
  FakeLocationGateway({this.permission = CrewLocationPermission.granted});

  CrewLocationPermission permission;
  CrewPosition current = _here;
  final List<CrewReportingMode> modes = [];
  int streamsCancelled = 0;
  StreamController<CrewPosition>? _stream;
  Completer<void>? _permissionHold;

  int get streamsOpened => modes.length;

  void holdPermission() => _permissionHold = Completer<void>();

  void releasePermission() {
    _permissionHold?.complete();
    _permissionHold = null;
  }

  void emit(CrewPosition position) => _stream?.add(position);

  void fail() => _stream?.addError(StateError('stream failed'));

  @override
  Future<CrewLocationPermission> requestPermission() async {
    await _permissionHold?.future;
    return permission;
  }

  @override
  Future<CrewLocationPermission> currentPermission() async => permission;

  @override
  Future<CrewPosition> currentPosition() async => current;

  @override
  Stream<CrewPosition> positions(CrewReportingMode mode) {
    modes.add(mode);
    final controller = StreamController<CrewPosition>(
      onCancel: () => streamsCancelled++,
    );
    _stream = controller;
    return controller.stream;
  }

  @override
  Future<bool> openAppSettings() async => true;

  @override
  Future<bool> openLocationSettings() async => true;
}
