import 'dart:async';

import 'package:flutter/foundation.dart';

import '../auth/auth_controller.dart';
import '../network/api_exception.dart';
import 'device_registrar.dart';
import 'permission_asked_store.dart';
import 'push_gateway.dart';

/// Keeps this phone registered for push while anyone - staff or patient - is signed in.
/// Push is only a nudge: every failure here is logged and swallowed, because the
/// app still finds new work by asking the server.
final class PushRegistration {
  PushRegistration({
    required PushGateway gateway,
    required DeviceRegistrar registrar,
    required AuthController auth,
    required void Function(PushNotificationEvent event) onNotificationOpened,
    required void Function(PushNotificationEvent event) onForegroundMessage,
    PermissionAskedStore? permissionAskedStore,
  })  : _gateway = gateway,
        _registrar = registrar,
        _auth = auth,
        _onNotificationOpened = onNotificationOpened,
        _onForegroundMessage = onForegroundMessage,
        _permissionAskedStore = permissionAskedStore ?? SecurePermissionAskedStore();

  final PushGateway _gateway;
  final DeviceRegistrar _registrar;
  final AuthController _auth;
  final void Function(PushNotificationEvent event) _onNotificationOpened;
  final void Function(PushNotificationEvent event) _onForegroundMessage;
  final PermissionAskedStore _permissionAskedStore;

  final _subscriptions = <StreamSubscription<Object?>>[];
  String? _registrationId;
  bool _registering = false;

  void start() {
    _auth.addListener(_onAuthChanged);
    _subscriptions
      ..add(_gateway.tokenRefreshes.listen((token) => _register(token)))
      ..add(_gateway.opened.listen(_onNotificationOpened))
      ..add(_gateway.foregroundMessages.listen(_onForegroundMessage));
    _onAuthChanged();
    unawaited(_handleColdStart());
  }

  // getInitialMessage only ever answers once, right after a cold start - a later call
  // (e.g. a hot restart during development) correctly returns null.
  Future<void> _handleColdStart() async {
    final message = await _gateway.initialMessage();
    if (message != null) _onNotificationOpened(message);
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
    if (_auth.status == AuthStatus.signedIn) {
      _registerCurrentToken();
    } else if (_auth.status == AuthStatus.signedOut) {
      _registrationId = null;
    }
  }

  Future<void> _registerCurrentToken() async {
    if (_registering || _registrationId != null) return;
    _registering = true;
    try {
      if (!await _hasPermission()) return;
      final token = await _gateway.token();
      if (token != null) await _register(token);
    } catch (error) {
      debugPrint('Push registration skipped: $error');
    } finally {
      _registering = false;
    }
  }

  // Android remembers the user's answer once asked, so asking again on a later launch would
  // only re-show the system dialog for someone who denied without checking "don't ask again".
  Future<bool> _hasPermission() async {
    if (await _permissionAskedStore.hasAsked()) return _gateway.hasPermission();
    await _permissionAskedStore.markAsked();
    return _gateway.requestPermission();
  }

  Future<void> _register(String token) async {
    if (_auth.status != AuthStatus.signedIn) return;
    try {
      _registrationId = await _registrar.register(token);
    } on ApiException catch (error) {
      debugPrint('Could not register this phone for push: ${error.message}');
    }
  }
}
