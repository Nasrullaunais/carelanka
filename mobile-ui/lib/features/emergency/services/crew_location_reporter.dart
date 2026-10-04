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

abstract interface class CrewLocationGateway {
  Future<CrewLocationPermission> requestPermission();

  /// Fixes that keep arriving when the phone is locked or Google Maps is open.
  Stream<CrewPosition> positions();
  Future<bool> openAppSettings();
  Future<bool> openLocationSettings();
}

abstract interface class CrewDispatchGateway {
  Future<String?> assignedAmbulanceId();
  Future<void> report(String ambulanceId, CrewPosition position);
}

final class CrewLocationReporter extends ChangeNotifier {
  CrewLocationReporter({
    required CrewDispatchGateway dispatches,
    required CrewLocationGateway location,
    this.interval = const Duration(seconds: 12),
  }) : _dispatches = dispatches,
       _location = location;

  final CrewDispatchGateway _dispatches;
  final CrewLocationGateway _location;
  final Duration interval;
  Timer? _timer;
  StreamSubscription<CrewPosition>? _positions;
  CrewPosition? _latest;
  bool _reporting = false;
  CrewLocationReportingState _state = CrewLocationReportingState.stopped;

  CrewLocationReportingState get state => _state;

  int _generation = 0;

  Future<void> resume() async {
    _stop();
    final generation = _generation;
    try {
      final permission = await _location.requestPermission();
      if (generation != _generation) return;
      if (permission != CrewLocationPermission.granted) {
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
        return;
      }
      _positions = _location.positions().listen(
        (position) {
          if (generation != _generation) return;
          final first = _latest == null;
          _latest = position;
          if (first) _reportLatest(generation);
        },
        onError: (Object _) {
          if (generation != _generation) return;
          // Without a live fix the last one is no longer true, so stop re-sending it.
          _latest = null;
          _setState(CrewLocationReportingState.failed);
        },
      );
      // Re-sends the last fix while parked, so the position never looks stale.
      _timer = Timer.periodic(interval, (_) => _reportLatest(generation));
    } catch (_) {
      if (generation == _generation) {
        _setState(CrewLocationReportingState.failed);
      }
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

  Future<void> stop() async => _stop();

  void _stop() {
    _generation++;
    _timer?.cancel();
    _timer = null;
    _positions?.cancel();
    _positions = null;
    _latest = null;
    if (_state == CrewLocationReportingState.reporting) {
      _setState(CrewLocationReportingState.stopped);
    }
  }

  Future<void> _reportLatest(int generation) async {
    final position = _latest;
    if (position == null || _reporting || generation != _generation) return;
    _reporting = true;
    try {
      final ambulanceId = await _dispatches.assignedAmbulanceId();
      if (generation != _generation) return;
      if (ambulanceId == null) {
        _setState(CrewLocationReportingState.stopped);
        return;
      }
      await _dispatches.report(ambulanceId, position);
      if (generation == _generation) {
        _setState(CrewLocationReportingState.reporting);
      }
    } catch (_) {
      if (generation == _generation) {
        _setState(CrewLocationReportingState.failed);
      }
    } finally {
      _reporting = false;
    }
  }

  @override
  void dispose() {
    _generation++;
    _timer?.cancel();
    _positions?.cancel();
    super.dispose();
  }

  void _setState(CrewLocationReportingState state) {
    if (_state == state) return;
    _state = state;
    notifyListeners();
  }
}

final class GeolocatorCrewLocationGateway implements CrewLocationGateway {
  const GeolocatorCrewLocationGateway(this._location);

  final DeviceLocation _location;

  @override
  Future<CrewLocationPermission> requestPermission() async =>
      switch (await _location.requestAccess()) {
        LocationAccess.precise => CrewLocationPermission.granted,
        LocationAccess.approximate => CrewLocationPermission.approximateOnly,
        LocationAccess.denied => CrewLocationPermission.denied,
        LocationAccess.deniedForever =>
          CrewLocationPermission.permanentlyDenied,
        LocationAccess.serviceOff => CrewLocationPermission.unavailable,
      };

  @override
  Stream<CrewPosition> positions() => _location.trackingFixes().map(
    (fix) => CrewPosition(fix.latitude, fix.longitude),
  );

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
