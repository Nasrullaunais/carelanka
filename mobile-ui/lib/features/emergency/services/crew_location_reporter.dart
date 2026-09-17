import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';

import '../../../core/network/api.dart';
import '../../../core/network/api_exception.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/dispatch_detail.dart';
import '../../../services/api_client/models/dispatch_status.dart';
import '../../../services/api_client/models/report_ambulance_location_request.dart';

enum CrewLocationPermission { granted, denied, permanentlyDenied, unavailable }

enum CrewLocationReportingState { stopped, reporting, permissionDenied, permissionPermanentlyDenied, unavailable, failed }

final class CrewPosition {
  const CrewPosition(this.latitude, this.longitude);

  final double latitude;
  final double longitude;

  @override
  bool operator ==(Object other) => other is CrewPosition && other.latitude == latitude && other.longitude == longitude;

  @override
  int get hashCode => Object.hash(latitude, longitude);
}

final class CrewDispatch {
  const CrewDispatch(this.id, this.ambulanceId, this.status);

  final String id;
  final String ambulanceId;
  final DispatchStatus status;

  bool get isLive => switch (status) {
        DispatchStatus.assigned ||
        DispatchStatus.acknowledged ||
        DispatchStatus.enRouteToScene ||
        DispatchStatus.atScene ||
        DispatchStatus.transportingToHospital => true,
        _ => false,
      };
}

abstract interface class CrewLocationGateway {
  Future<CrewLocationPermission> requestPermission();
  Future<CrewPosition> currentPosition();
}

abstract interface class CrewDispatchGateway {
  Future<CrewDispatch?> activeDispatch();
  Future<void> report(String ambulanceId, CrewPosition position);
}

final class CrewLocationReporter extends ChangeNotifier {
  CrewLocationReporter({
    required CrewDispatchGateway dispatches,
    required CrewLocationGateway location,
    this.interval = const Duration(seconds: 12),
  })  : _dispatches = dispatches,
        _location = location;

  final CrewDispatchGateway _dispatches;
  final CrewLocationGateway _location;
  final Duration interval;
  Timer? _timer;
  bool _reporting = false;
  CrewLocationReportingState _state = CrewLocationReportingState.stopped;

  CrewLocationReportingState get state => _state;

  Future<void> resume() async {
    await stop();
    try {
      final permission = await _location.requestPermission();
      if (permission != CrewLocationPermission.granted) {
      _setState(switch (permission) {
          CrewLocationPermission.denied => CrewLocationReportingState.permissionDenied,
          CrewLocationPermission.permanentlyDenied => CrewLocationReportingState.permissionPermanentlyDenied,
          CrewLocationPermission.unavailable => CrewLocationReportingState.unavailable,
          CrewLocationPermission.granted => CrewLocationReportingState.stopped,
        });
        return;
      }
      final dispatch = await _dispatches.activeDispatch();
      if (dispatch == null || !dispatch.isLive) {
        _setState(CrewLocationReportingState.stopped);
        return;
      }
      _setState(CrewLocationReportingState.reporting);
      await _report(dispatch);
      if (_state != CrewLocationReportingState.reporting) return;
      _timer = Timer.periodic(interval, (_) => _reportCurrent());
    } catch (_) {
      _setState(CrewLocationReportingState.failed);
      await stop();
    }
  }

  Future<void> pause() => stop();

  Future<void> stop() async {
    _timer?.cancel();
    _timer = null;
    _reporting = false;
    if (_state == CrewLocationReportingState.reporting) _setState(CrewLocationReportingState.stopped);
  }

  Future<void> _reportCurrent() async {
    if (_reporting) return;
    try {
      final dispatch = await _dispatches.activeDispatch();
      if (dispatch == null || !dispatch.isLive) {
        await stop();
        return;
      }
      await _report(dispatch);
    } catch (_) {
      _setState(CrewLocationReportingState.failed);
      await stop();
    }
  }

  Future<void> _report(CrewDispatch dispatch) async {
    if (_reporting) return;
    _reporting = true;
    try {
      final position = await _location.currentPosition();
      await _dispatches.report(dispatch.ambulanceId, position);
    } catch (_) {
      _setState(CrewLocationReportingState.failed);
      await stop();
    } finally {
      _reporting = false;
    }
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
    if (!await Geolocator.isLocationServiceEnabled()) return CrewLocationPermission.unavailable;
    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) permission = await Geolocator.requestPermission();
    return switch (permission) {
      LocationPermission.always || LocationPermission.whileInUse => CrewLocationPermission.granted,
      LocationPermission.deniedForever => CrewLocationPermission.permanentlyDenied,
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
  Future<CrewDispatch?> activeDispatch() async {
    try {
      return _toCrewDispatch(await callApi(() => _api.myRun.getMyActiveDispatch()));
    } on ApiException catch (error) {
      if (error.isNotFound) return null;
      rethrow;
    }
  }

  @override
  Future<void> report(String ambulanceId, CrewPosition position) => callApi(
        () => _api.ambulances.reportAmbulanceLocation(
          id: ambulanceId,
          body: ReportAmbulanceLocationRequest(latitude: position.latitude, longitude: position.longitude),
        ),
      );

  CrewDispatch? _toCrewDispatch(DispatchDetail dispatch) {
    final id = dispatch.id;
    final ambulanceId = dispatch.ambulanceId;
    final status = dispatch.status;
    if (id == null || ambulanceId == null || status == null) return null;
    return CrewDispatch(id, ambulanceId, status);
  }
}
