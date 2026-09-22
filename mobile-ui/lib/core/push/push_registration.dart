import 'dart:async';

import 'package:flutter/foundation.dart';

import '../auth/auth_controller.dart';
import '../network/api_exception.dart';
import 'device_registrar.dart';
import 'push_gateway.dart';

/// Keeps this phone registered for push while a staff member is signed in.
/// Push is only a nudge: every failure here is logged and swallowed, because the
/// app still finds new work by asking the server.
final class PushRegistration {
  PushRegistration({
    required PushGateway gateway,
    required DeviceRegistrar registrar,
    required AuthController auth,
    required VoidCallback onNotificationOpened,
  })  : _gateway = gateway,
        _registrar = registrar,
        _auth = auth,
        _onNotificationOpened = onNotificationOpened;

  final PushGateway _gateway;
  final DeviceRegistrar _registrar;
  final AuthController _auth;
  final VoidCallback _onNotificationOpened;

  final _subscriptions = <StreamSubscription<Object?>>[];
  String? _registrationId;
  bool _registering = false;

  void start() {
    _auth.addListener(_onAuthChanged);
    _subscriptions
      ..add(_gateway.tokenRefreshes.listen((token) => _register(token)))
      ..add(_gateway.opened.listen((_) => _onNotificationOpened()));
    _onAuthChanged();
  }

  Future<void> stop() async {
    _auth.removeListener(_onAuthChanged);
    for (final subscription in _subscriptions) {
      await subscription.cancel();
    }
    _subscriptions.clear();
  }

  Future<void> unregister() async {
    final id = _registrationId;
    _registrationId = null;
    if (id == null) return;
    try {
      await _registrar.unregister(id);
    } on ApiException catch (error) {
      debugPrint('Could not stop pushes for this phone: ${error.message}');
    }
  }

  void _onAuthChanged() {
    if (_auth.status == AuthStatus.signedIn && _auth.isStaff) {
      _registerCurrentToken();
    } else if (_auth.status == AuthStatus.signedOut) {
      _registrationId = null;
    }
  }

  Future<void> _registerCurrentToken() async {
    if (_registering || _registrationId != null) return;
    _registering = true;
    try {
      if (!await _gateway.requestPermission()) return;
      final token = await _gateway.token();
      if (token != null) await _register(token);
    } catch (error) {
      debugPrint('Push registration skipped: $error');
    } finally {
      _registering = false;
    }
  }

  Future<void> _register(String token) async {
    if (_auth.status != AuthStatus.signedIn || !_auth.isStaff) return;
    try {
      _registrationId = await _registrar.register(token);
    } on ApiException catch (error) {
      debugPrint('Could not register this phone for push: ${error.message}');
    }
  }
}
