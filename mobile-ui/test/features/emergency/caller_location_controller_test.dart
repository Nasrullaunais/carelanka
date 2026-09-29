import 'dart:async';

import 'package:carelanka_mobile/features/emergency/services/device_location.dart';
import 'package:carelanka_mobile/features/emergency/state/caller_location_controller.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:geolocator/geolocator.dart';

void main() {
  test('precise access streams the latest fix and allows sending', () async {
    final location = FakeDeviceLocation();
    final controller = CallerLocationController(location);

    await controller.start();
    expect(controller.status, CallerLocationStatus.searching);
    expect(controller.canSend, isFalse);

    location.emit(_fix(accuracy: 40));
    location.emit(_fix(accuracy: 8));
    await pumpEventQueue();

    expect(controller.status, CallerLocationStatus.located);
    expect(controller.fix!.accuracyMetres, 8);
    expect(controller.isWeak, isFalse);
    expect(controller.canSend, isTrue);
    controller.dispose();
  });

  test('a recent last-known fix is shown straight away', () async {
    final location = FakeDeviceLocation()..recent = _fix(accuracy: 15);
    final controller = CallerLocationController(location);

    await controller.start();

    expect(controller.status, CallerLocationStatus.located);
    expect(controller.canSend, isTrue);
    controller.dispose();
  });

  test('refused permission blocks sending', () async {
    final location = FakeDeviceLocation()
      ..requested = LocationAccess.deniedForever;
    final controller = CallerLocationController(location);

    await controller.start();

    expect(controller.status, CallerLocationStatus.blocked);
    expect(controller.canSend, isFalse);
    controller.dispose();
  });

  test(
    'approximate access still sends but remembers a refused upgrade',
    () async {
      final location = FakeDeviceLocation()
        ..requested = LocationAccess.approximate;
      final controller = CallerLocationController(location);

      await controller.start();
      location.emit(_fix(accuracy: 2000));
      await pumpEventQueue();

      expect(controller.approximate, isTrue);
      expect(controller.isWeak, isTrue);
      expect(controller.canSend, isTrue);
      expect(controller.preciseRefused, isFalse);

      await controller.askForPrecise();
      expect(controller.preciseRefused, isTrue);

      location.requested = LocationAccess.precise;
      await controller.askForPrecise();
      expect(controller.approximate, isFalse);
      expect(controller.preciseRefused, isFalse);
      controller.dispose();
    },
  );

  test('no fix in time is reported as slow, then recovers', () async {
    final location = FakeDeviceLocation();
    final controller = CallerLocationController(
      location,
      slowAfter: const Duration(milliseconds: 10),
    );

    await controller.start();
    await Future<void>.delayed(const Duration(milliseconds: 30));
    expect(controller.status, CallerLocationStatus.slow);

    location.emit(_fix(accuracy: 20));
    await pumpEventQueue();
    expect(controller.status, CallerLocationStatus.located);
    controller.dispose();
  });

  test('location switched off mid-search drops the fix', () async {
    final location = FakeDeviceLocation();
    final controller = CallerLocationController(location);

    await controller.start();
    location.emit(_fix(accuracy: 20));
    location.fail(const LocationServiceDisabledException());
    await pumpEventQueue();

    expect(controller.status, CallerLocationStatus.serviceOff);
    expect(controller.canSend, isFalse);
    controller.dispose();
  });

  test('coming back to the app re-checks access without prompting', () async {
    final location = FakeDeviceLocation()
      ..requested = LocationAccess.serviceOff;
    final controller = CallerLocationController(location);

    await controller.start();
    expect(controller.status, CallerLocationStatus.serviceOff);

    controller.pause();
    location.checked = LocationAccess.precise;
    await controller.resume();

    expect(location.requests, 1);
    expect(controller.status, CallerLocationStatus.searching);
    controller.dispose();
  });
}

LocationFix _fix({required double accuracy}) => LocationFix(
  latitude: 6.9271,
  longitude: 79.8612,
  accuracyMetres: accuracy,
  capturedAt: DateTime.now().toUtc(),
);

final class FakeDeviceLocation implements DeviceLocation {
  LocationAccess requested = LocationAccess.precise;
  LocationAccess checked = LocationAccess.precise;
  LocationFix? recent;
  int requests = 0;
  StreamController<LocationFix>? _fixes;

  void emit(LocationFix fix) => _fixes!.add(fix);

  void fail(Object error) => _fixes!.addError(error);

  @override
  Future<LocationAccess> requestAccess() async {
    requests++;
    return requested;
  }

  @override
  Future<LocationAccess> checkAccess() async => checked;

  @override
  Future<LocationFix?> recentFix(Duration maxAge) async => recent;

  @override
  Future<LocationFix> currentFix(Duration timeLimit) =>
      throw UnimplementedError();

  @override
  Stream<LocationFix> fixes() {
    _fixes?.close();
    _fixes = StreamController<LocationFix>();
    return _fixes!.stream;
  }

  @override
  Future<bool> openAppSettings() async => true;

  @override
  Future<bool> openLocationSettings() async => true;
}
