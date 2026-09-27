import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';

import '../services/device_location.dart';

enum CallerLocationStatus {
  checking,
  needsPermission,
  blocked,
  serviceOff,
  searching,
  slow,
  located,
  unavailable,
}

class CallerLocationController extends ChangeNotifier {
  CallerLocationController(
    this._location, {
    this.slowAfter = const Duration(seconds: 20),
  });

  static const weakAccuracyMetres = 100.0;
  static const _recentFixMaxAge = Duration(minutes: 1);

  final DeviceLocation _location;
  final Duration slowAfter;
  StreamSubscription<LocationFix>? _subscription;
  Timer? _slowTimer;
  int _generation = 0;
  bool _paused = false;
  bool _disposed = false;

  CallerLocationStatus status = CallerLocationStatus.checking;
  LocationFix? fix;
  bool approximate = false;
  bool preciseRefused = false;

  bool get canSend => fix != null;

  bool get isWeak => fix != null && fix!.accuracyMetres > weakAccuracyMetres;

  Future<void> start() => _begin(prompt: true);

  Future<void> retry() => _begin(prompt: false);

  Future<void> askForPrecise() async {
    await _begin(prompt: true);
    if (approximate) {
      preciseRefused = true;
      _notify();
    }
  }

  Future<bool> openAppSettings() => _location.openAppSettings();

  Future<bool> openLocationSettings() => _location.openLocationSettings();

  void pause() {
    _paused = true;
    _generation++;
    _stopListening();
  }

  Future<void> resume() async {
    if (!_paused) return;
    _paused = false;
    await _begin(prompt: false);
  }

  Future<void> _begin({required bool prompt}) async {
    final generation = ++_generation;
    _stopListening();
    if (fix == null) _set(CallerLocationStatus.checking);
    try {
      final access = prompt
          ? await _location.requestAccess()
          : await _location.checkAccess();
      if (generation != _generation) return;
      switch (access) {
        case LocationAccess.precise || LocationAccess.approximate:
          approximate = access == LocationAccess.approximate;
          if (!approximate) preciseRefused = false;
          await _listen(generation);
        case LocationAccess.denied:
          _lose(CallerLocationStatus.needsPermission);
        case LocationAccess.deniedForever:
          _lose(CallerLocationStatus.blocked);
        case LocationAccess.serviceOff:
          _lose(CallerLocationStatus.serviceOff);
      }
    } catch (_) {
      if (generation == _generation) _lose(CallerLocationStatus.unavailable);
    }
  }

  Future<void> _listen(int generation) async {
    if (fix == null) {
      _set(CallerLocationStatus.searching);
      final recent = await _location.recentFix(_recentFixMaxAge);
      if (generation != _generation) return;
      if (recent != null) _accept(recent);
    } else {
      _set(CallerLocationStatus.located);
    }
    _subscription = _location.fixes().listen(
      _accept,
      onError: (Object error) {
        if (generation != _generation) return;
        _stopListening();
        _lose(switch (error) {
          LocationServiceDisabledException() => CallerLocationStatus.serviceOff,
          PermissionDeniedException() => CallerLocationStatus.needsPermission,
          _ => CallerLocationStatus.unavailable,
        });
      },
    );
    if (fix == null) {
      _slowTimer = Timer(slowAfter, () {
        if (fix == null) _set(CallerLocationStatus.slow);
      });
    }
  }

  void _accept(LocationFix value) {
    fix = value;
    _slowTimer?.cancel();
    status = CallerLocationStatus.located;
    _notify();
  }

  void _lose(CallerLocationStatus value) {
    fix = null;
    _set(value);
  }

  void _stopListening() {
    _subscription?.cancel();
    _subscription = null;
    _slowTimer?.cancel();
    _slowTimer = null;
  }

  void _set(CallerLocationStatus value) {
    status = value;
    _notify();
  }

  void _notify() {
    if (!_disposed) notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _generation++;
    _stopListening();
    super.dispose();
  }
}
