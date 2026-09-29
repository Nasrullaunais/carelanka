import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';

enum LocationAccess { precise, approximate, denied, deniedForever, serviceOff }

final class LocationFix {
  const LocationFix({
    required this.latitude,
    required this.longitude,
    required this.accuracyMetres,
    required this.capturedAt,
  });

  final double latitude;
  final double longitude;
  final double accuracyMetres;
  final DateTime capturedAt;
}

abstract interface class DeviceLocation {
  Future<LocationAccess> checkAccess();

  /// Shows the system prompt. When only approximate access is held, this is
  /// also what asks the user to switch to precise.
  Future<LocationAccess> requestAccess();

  Future<LocationFix?> recentFix(Duration maxAge);
  Future<LocationFix> currentFix(Duration timeLimit);
  Stream<LocationFix> fixes();
  Future<bool> openLocationSettings();
  Future<bool> openAppSettings();
}

final class GeolocatorDeviceLocation implements DeviceLocation {
  const GeolocatorDeviceLocation();

  static const _preciseAccuracyPurpose = 'EmergencyLocation';

  @override
  Future<LocationAccess> checkAccess() async {
    if (!await Geolocator.isLocationServiceEnabled()) {
      return LocationAccess.serviceOff;
    }
    return _accessFor(await Geolocator.checkPermission());
  }

  @override
  Future<LocationAccess> requestAccess() async {
    if (!await Geolocator.isLocationServiceEnabled()) {
      return LocationAccess.serviceOff;
    }
    var access = await _accessFor(await Geolocator.checkPermission());
    if (access == LocationAccess.denied ||
        (access == LocationAccess.approximate &&
            defaultTargetPlatform == TargetPlatform.android)) {
      access = await _accessFor(await Geolocator.requestPermission());
    }
    if (access == LocationAccess.approximate &&
        defaultTargetPlatform == TargetPlatform.iOS &&
        await Geolocator.requestTemporaryFullAccuracy(
              purposeKey: _preciseAccuracyPurpose,
            ) ==
            LocationAccuracyStatus.precise) {
      access = LocationAccess.precise;
    }
    return access;
  }

  @override
  Future<LocationFix?> recentFix(Duration maxAge) async {
    final position = await Geolocator.getLastKnownPosition();
    if (position == null) return null;
    if (DateTime.now().difference(position.timestamp) > maxAge) return null;
    return _fix(position);
  }

  @override
  Future<LocationFix> currentFix(Duration timeLimit) async => _fix(
    await Geolocator.getCurrentPosition(
      locationSettings: _settings(timeLimit: timeLimit),
    ),
  );

  @override
  Stream<LocationFix> fixes() =>
      Geolocator.getPositionStream(locationSettings: _settings()).map(_fix);

  @override
  Future<bool> openLocationSettings() => Geolocator.openLocationSettings();

  @override
  Future<bool> openAppSettings() => Geolocator.openAppSettings();

  Future<LocationAccess> _accessFor(LocationPermission permission) async =>
      switch (permission) {
        LocationPermission.always ||
        LocationPermission.whileInUse => _preciseOr(LocationAccess.approximate),
        LocationPermission.deniedForever => LocationAccess.deniedForever,
        _ => LocationAccess.denied,
      };

  Future<LocationAccess> _preciseOr(LocationAccess fallback) async =>
      await Geolocator.getLocationAccuracy() == LocationAccuracyStatus.precise
      ? LocationAccess.precise
      : fallback;

  static LocationSettings _settings({Duration? timeLimit}) =>
      switch (defaultTargetPlatform) {
        TargetPlatform.android => AndroidSettings(
          accuracy: LocationAccuracy.best,
          intervalDuration: const Duration(seconds: 2),
          timeLimit: timeLimit,
        ),
        TargetPlatform.iOS => AppleSettings(
          accuracy: LocationAccuracy.best,
          activityType: ActivityType.other,
          timeLimit: timeLimit,
        ),
        _ => LocationSettings(
          accuracy: LocationAccuracy.best,
          timeLimit: timeLimit,
        ),
      };

  static LocationFix _fix(Position position) => LocationFix(
    latitude: position.latitude,
    longitude: position.longitude,
    accuracyMetres: position.accuracy,
    capturedAt: position.timestamp.toUtc(),
  );
}
