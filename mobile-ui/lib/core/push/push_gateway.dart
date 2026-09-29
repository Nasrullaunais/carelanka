import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

/// One push message, trimmed to what the app ever needs from it - display text plus the
/// same `entity_type`/`entity_id`/`type` data the inbox itself carries.
class PushNotificationEvent {
  const PushNotificationEvent({this.title, this.body, required this.data});

  final String? title;
  final String? body;
  final Map<String, String> data;
}

abstract interface class PushGateway {
  Future<bool> requestPermission();
  Future<bool> hasPermission();
  Future<String?> token();
  Stream<String> get tokenRefreshes;

  /// A background or terminated tap that brought the app to the foreground.
  Stream<PushNotificationEvent> get opened;

  /// A push that arrived while the app was already open - the OS never shows its own
  /// pop-up for this, so the app must tell the user itself.
  Stream<PushNotificationEvent> get foregroundMessages;

  /// The push that cold-started the app, if any. Only ever meaningful once, right after launch.
  Future<PushNotificationEvent?> initialMessage();
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
  Future<bool> hasPermission() async {
    final settings = await _messaging.getNotificationSettings();
    return settings.authorizationStatus == AuthorizationStatus.authorized ||
        settings.authorizationStatus == AuthorizationStatus.provisional;
  }

  @override
  Future<String?> token() => _messaging.getToken();

  @override
  Stream<String> get tokenRefreshes => _messaging.onTokenRefresh;

  @override
  Stream<PushNotificationEvent> get opened =>
      FirebaseMessaging.onMessageOpenedApp.map(_asEvent);

  @override
  Stream<PushNotificationEvent> get foregroundMessages =>
      FirebaseMessaging.onMessage.map(_asEvent);

  @override
  Future<PushNotificationEvent?> initialMessage() async {
    final message = await _messaging.getInitialMessage();
    return message == null ? null : _asEvent(message);
  }

  static PushNotificationEvent _asEvent(RemoteMessage message) => PushNotificationEvent(
        title: message.notification?.title,
        body: message.notification?.body,
        data: message.data.map((key, value) => MapEntry(key, '$value')),
      );
}
