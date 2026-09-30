import 'dart:async';

import 'package:flutter/foundation.dart';

import '../../../core/network/api.dart';
import '../../../core/network/api_exception.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/report_ambulance_location_request.dart';
import 'device_location.dart';

enum CrewLocationPermission {
  granted,
  approximateOnly,
  denied,
  permanentlyDenied,
  unavailable,
}

enum CrewLocationReportingState {
  stopped,
  reporting,
  approximateOnly,
  permissionDenied,
  permissionPermanentlyDenied,
  unavailable,
  failed,
}

final class CrewPosition {
  const CrewPosition(this.latitude, this.longitude);

  final double latitude;
  final double longitude;

  @override
  bool operator ==(Object other) =>
      other is CrewPosition &&
      other.latitude == latitude &&
      other.longitude == longitude;

  @override
  int get hashCode => Object.hash(latitude, longitude);
}

final class CrewReportingMode {
  const CrewReportingMode.foreground() : runRegistration = null;
  const CrewReportingMode.run(String this.runRegistration);

  final String? runRegistration;

  bool get isRun => runRegistration != null;

  @override
  bool operator ==(Object other) =>
      other is CrewReportingMode && other.runRegistration == runRegistration;

  @override
  int get hashCode => runRegistration.hashCode;
}

abstract interface class CrewLocationGateway {
  Future<CrewLocationPermission> requestPermission();
  Future<CrewLocationPermission> currentPermission();
  Future<CrewPosition> currentPosition();
  Stream<CrewPosition> positions(CrewReportingMode mode);
  Future<bool> openAppSettings();
  Future<bool> openLocationSettings();
}

abstract interface class CrewDispatchGateway {
  Future<String?> assignedAmbulanceId();
  Future<void> report(String ambulanceId, CrewPosition position);
}

/// Shares the crew's ambulance position. With no live run it only does so
/// while the app is on screen; on a run it keeps going in the background.
final class CrewLocationReporter extends ChangeNotifier {
  CrewLocationReporter({
    required CrewDispatchGateway dispatches,
    required CrewLocationGateway location,
    this.minUploadGap = const Duration(seconds: 10),
    this.heartbeat = const Duration(seconds: 30),
    this.retryDelay = const Duration(seconds: 30),
    DateTime Function() now = DateTime.now,
  }) : _dispatches = dispatches,
       _location = location,
       _now = now;

  static const distanceFilterMetres = 25;
  static const recentFixAge = Duration(seconds: 30);

  final CrewDispatchGateway _dispatches;
  final CrewLocationGateway _location;
  final Duration minUploadGap;
  final Duration heartbeat;
  final Duration retryDelay;
  final DateTime Function() _now;

  CrewReportingMode _mode = const CrewReportingMode.foreground();
  CrewLocationReportingState _state = CrewLocationReportingState.stopped;
  StreamSubscription<CrewPosition>? _subscription;
  Timer? _throttle;
  Timer? _heartbeat;
  Timer? _retry;
  CrewPosition? _pending;
  CrewPosition? _lastFix;
  DateTime? _lastFixAt;
  String? _ambulanceId;
  DateTime? _lastAttemptAt;
  bool _visible = false;
  bool _uploading = false;
  bool _disposed = false;
  int _generation = 0;

  CrewLocationReportingState get state => _state;

  /// The newest fix, or null when it is too old to say where the crew is now.
  CrewPosition? get recentPosition {
    final at = _lastFixAt;
    if (at == null || _now().difference(at) > recentFixAge) return null;
    return _lastFix;
  }

  bool get _wanted => _visible || _mode.isRun;

  Future<void> resume() async {
    _visible = true;
    if (_subscription != null) return;
    await _restart();
  }

  Future<void> pause() async {
    _visible = false;
    if (!_mode.isRun) _stopReporting();
  }

  Future<void> stop() async {
    _visible = false;
    _mode = const CrewReportingMode.foreground();
    _lastFix = null;
    _lastFixAt = null;
    _stopReporting();
  }

  Future<void> useMode(CrewReportingMode mode) async {
    if (_mode == mode) return;
    _mode = mode;
    if (_wanted) {
      await _restart();
    } else {
      _stopReporting();
    }
  }

  Future<void> fixAccess() async {
    switch (_state) {
      case CrewLocationReportingState.permissionDenied:
        await resume();
      case CrewLocationReportingState.approximateOnly ||
          CrewLocationReportingState.permissionPermanentlyDenied:
        await _location.openAppSettings();
      case CrewLocationReportingState.unavailable:
        await _location.openLocationSettings();
      case CrewLocationReportingState.stopped ||
          CrewLocationReportingState.reporting ||
          CrewLocationReportingState.failed:
        return;
    }
  }

  Future<void> _restart() async {
    _teardown();
    final generation = _generation;
    try {
      final permission = await _location.requestPermission();
      if (generation != _generation) return;
      if (permission != CrewLocationPermission.granted) {
        _blocked(permission);
        return;
      }
      _ambulanceId = await _dispatches.assignedAmbulanceId();
      if (generation != _generation) return;
      if (_ambulanceId == null) {
        _setState(CrewLocationReportingState.stopped);
        _scheduleRetry();
        return;
      }
      _subscription = _location
          .positions(_mode)
          .listen(_offer, onError: (Object _) => _streamFailed(generation));
      _armHeartbeat(Duration.zero);
    } catch (_) {
      if (generation != _generation) return;
      _teardown();
      _setState(CrewLocationReportingState.failed);
      _scheduleRetry();
    }
  }

  Future<void> _streamFailed(int generation) async {
    if (generation != _generation) return;
    _teardown();
    final current = _generation;
    try {
      final permission = await _location.currentPermission();
      if (current != _generation) return;
      if (permission == CrewLocationPermission.granted) {
        _setState(CrewLocationReportingState.failed);
        _scheduleRetry();
      } else {
        _blocked(permission);
      }
    } catch (_) {
      if (current != _generation) return;
      _setState(CrewLocationReportingState.failed);
      _scheduleRetry();
    }
  }

  void _blocked(CrewLocationPermission permission) {
    _setState(switch (permission) {
      CrewLocationPermission.approximateOnly =>
        CrewLocationReportingState.approximateOnly,
      CrewLocationPermission.denied =>
        CrewLocationReportingState.permissionDenied,
      CrewLocationPermission.permanentlyDenied =>
        CrewLocationReportingState.permissionPermanentlyDenied,
      CrewLocationPermission.unavailable =>
        CrewLocationReportingState.unavailable,
      CrewLocationPermission.granted => CrewLocationReportingState.stopped,
    });
    if (permission == CrewLocationPermission.unavailable) _scheduleRetry();
  }

  void _offer(CrewPosition position) {
    _pending = position;
    _lastFix = position;
    _lastFixAt = _now();
    if (_uploading || _throttle != null) return;
    final generation = _generation;
    final gap = _lastAttemptAt == null
        ? Duration.zero
        : minUploadGap - _now().difference(_lastAttemptAt!);
    if (gap <= Duration.zero) {
      unawaited(_upload(generation));
      return;
    }
    _throttle = Timer(gap, () {
      _throttle = null;
      unawaited(_upload(generation));
    });
  }

  Future<void> _upload(int generation) async {
    final position = _pending;
    if (position == null || generation != _generation) return;
    _pending = null;
    _uploading = true;
    _lastAttemptAt = _now();
    try {
      final ambulanceId = _ambulanceId ??= await _dispatches
          .assignedAmbulanceId();
      if (generation != _generation) return;
      if (ambulanceId == null) {
        _teardown();
        _setState(CrewLocationReportingState.stopped);
        _scheduleRetry();
        return;
      }
      await _dispatches.report(ambulanceId, position);
      if (generation == _generation) {
        _setState(CrewLocationReportingState.reporting);
      }
    } catch (_) {
      if (generation != _generation) return;
      _ambulanceId = null;
      _setState(CrewLocationReportingState.failed);
    } finally {
      if (generation == _generation) {
        _uploading = false;
        _armHeartbeat(heartbeat);
        if (_pending case final next?) _offer(next);
      }
    }
  }

  /// Stationary phones send no stream updates, which would make a parked
  /// ambulance look stale; this asks for a fresh fix after a silence.
  void _armHeartbeat(Duration after) {
    _heartbeat?.cancel();
    final generation = _generation;
    _heartbeat = Timer(after, () async {
      try {
        final position = await _location.currentPosition();
        if (generation == _generation) _offer(position);
      } catch (_) {
        if (generation != _generation) return;
        _setState(CrewLocationReportingState.failed);
        _armHeartbeat(heartbeat);
      }
    });
  }

  void _scheduleRetry() {
    _retry?.cancel();
    _retry = Timer(retryDelay, () {
      if (_wanted) unawaited(_restart());
    });
  }

  void _stopReporting() {
    _teardown();
    if (_state == CrewLocationReportingState.reporting ||
        _state == CrewLocationReportingState.failed) {
      _setState(CrewLocationReportingState.stopped);
    }
  }

  void _teardown() {
    _generation++;
    _subscription?.cancel();
    _subscription = null;
    _throttle?.cancel();
    _throttle = null;
    _heartbeat?.cancel();
    _heartbeat = null;
    _retry?.cancel();
    _retry = null;
    _pending = null;
    _uploading = false;
    _lastAttemptAt = null;
  }

  @override
  void dispose() {
    _disposed = true;
    _teardown();
    super.dispose();
  }

  void _setState(CrewLocationReportingState state) {
    if (_state == state || _disposed) return;
    _state = state;
    notifyListeners();
  }
}

final class GeolocatorCrewLocationGateway implements CrewLocationGateway {
  const GeolocatorCrewLocationGateway(this._location);

  static const _fixTimeLimit = Duration(seconds: 10);

  final DeviceLocation _location;

  @override
  Future<CrewLocationPermission> requestPermission() async =>
      _permissionFor(await _location.requestAccess());

  static CrewLocationPermission _permissionFor(LocationAccess access) =>
      switch (access) {
        LocationAccess.precise => CrewLocationPermission.granted,
        LocationAccess.approximate => CrewLocationPermission.approximateOnly,
        LocationAccess.denied => CrewLocationPermission.denied,
        LocationAccess.deniedForever =>
          CrewLocationPermission.permanentlyDenied,
        LocationAccess.serviceOff => CrewLocationPermission.unavailable,
      };

  @override
  Future<CrewLocationPermission> currentPermission() async =>
      _permissionFor(await _location.checkAccess());

  @override
  Future<CrewPosition> currentPosition() async {
    final fix = await _location.currentFix(_fixTimeLimit);
    return CrewPosition(fix.latitude, fix.longitude);
  }

  @override
  Stream<CrewPosition> positions(CrewReportingMode mode) => _location
      .fixes(
        distanceFilterMetres: CrewLocationReporter.distanceFilterMetres,
        background: switch (mode.runRegistration) {
          final registration? => BackgroundTracking(
            notificationTitle: 'Sharing ambulance location',
            notificationText: 'Run in progress for $registration',
          ),
          null => null,
        },
      )
      .map((fix) => CrewPosition(fix.latitude, fix.longitude));

  @override
  Future<bool> openAppSettings() => _location.openAppSettings();

  @override
  Future<bool> openLocationSettings() => _location.openLocationSettings();
}

final class GeneratedCrewDispatchGateway implements CrewDispatchGateway {
  GeneratedCrewDispatchGateway(this._api);

  final CareLankaApi _api;

  @override
  Future<String?> assignedAmbulanceId() async {
    try {
      return (await callApi(
        () => _api.ambulances.getMyAmbulanceAssignment(),
      )).id;
    } on ApiException catch (error) {
      if (error.isNotFound) return null;
      rethrow;
    }
  }

  @override
  Future<void> report(String ambulanceId, CrewPosition position) => callApi(
    () => _api.ambulances.reportAmbulanceLocation(
      id: ambulanceId,
      body: ReportAmbulanceLocationRequest(
        latitude: position.latitude,
        longitude: position.longitude,
      ),
    ),
  );
}
