import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';

import '../../../core/network/api.dart';
import '../../../core/network/api_exception.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/report_ambulance_location_request.dart';

enum CrewLocationPermission { granted, denied, permanentlyDenied, unavailable }

enum CrewLocationReportingState {
  stopped,
  reporting,
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
  Future<CrewPosition> currentPosition();
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
      await _reportCurrent(generation);
      if (generation != _generation) return;
      _timer = Timer.periodic(interval, (_) => _reportCurrent(generation));
    } catch (_) {
      if (generation == _generation) {
        _setState(CrewLocationReportingState.failed);
      }
    }
  }

  Future<void> pause() => stop();

  Future<void> stop() async => _stop();

  void _stop() {
    _generation++;
    _timer?.cancel();
    _timer = null;
    if (_state == CrewLocationReportingState.reporting) {
      _setState(CrewLocationReportingState.stopped);
    }
  }

  Future<void> _reportCurrent(int generation) async {
    if (_reporting || generation != _generation) return;
    _reporting = true;
    try {
      final ambulanceId = await _dispatches.assignedAmbulanceId();
      if (generation != _generation) return;
      if (ambulanceId == null) {
        _setState(CrewLocationReportingState.stopped);
        return;
      }
      final position = await _location.currentPosition();
      if (generation != _generation) return;
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
    super.dispose();
  }

  void _setState(CrewLocationReportingState state) {
    if (_state == state) return;
    _state = state;
    notifyListeners();
  }
}

final class GeolocatorCrewLocationGateway implements CrewLocationGateway {
  @override
  Future<CrewLocationPermission> requestPermission() async {
    if (!await Geolocator.isLocationServiceEnabled()) {
      return CrewLocationPermission.unavailable;
    }
    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) {
      permission = await Geolocator.requestPermission();
    }
    return switch (permission) {
      LocationPermission.always ||
      LocationPermission.whileInUse => CrewLocationPermission.granted,
      LocationPermission.deniedForever =>
        CrewLocationPermission.permanentlyDenied,
      _ => CrewLocationPermission.denied,
    };
  }

  @override
  Future<CrewPosition> currentPosition() async {
    final position = await Geolocator.getCurrentPosition();
    return CrewPosition(position.latitude, position.longitude);
  }
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
