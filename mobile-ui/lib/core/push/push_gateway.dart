import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

abstract interface class PushGateway {
  Future<bool> requestPermission();
  Future<String?> token();
  Stream<String> get tokenRefreshes;
  Stream<void> get opened;
}

final class FirebasePushGateway implements PushGateway {
  FirebasePushGateway._(this._messaging);

  final FirebaseMessaging _messaging;

  // Push is Android-only for now, and a build without google-services.json just runs without it.
  static Future<FirebasePushGateway?> create() async {
    if (kIsWeb || defaultTargetPlatform != TargetPlatform.android) return null;
    try {
      await Firebase.initializeApp();
      return FirebasePushGateway._(FirebaseMessaging.instance);
    } catch (error) {
      debugPrint('Push is off: Firebase did not start ($error).');
      return null;
    }
  }

  @override
  Future<bool> requestPermission() async {
    final settings = await _messaging.requestPermission();
    return settings.authorizationStatus == AuthorizationStatus.authorized ||
        settings.authorizationStatus == AuthorizationStatus.provisional;
  }

  @override
  Future<String?> token() => _messaging.getToken();

  @override
  Stream<String> get tokenRefreshes => _messaging.onTokenRefresh;

  @override
  Stream<void> get opened => FirebaseMessaging.onMessageOpenedApp;
}
