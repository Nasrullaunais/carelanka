import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Whether this phone has ever been asked for notification permission - kept outside the
/// OS's own memory of the answer, since re-asking a user who denied once (without "don't ask
/// again") would show the system dialog a second time.
abstract interface class PermissionAskedStore {
  Future<bool> hasAsked();
  Future<void> markAsked();
}

final class SecurePermissionAskedStore implements PermissionAskedStore {
  SecurePermissionAskedStore([FlutterSecureStorage? storage])
      : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  static const _key = 'carelanka.notification_permission_asked';

  @override
  Future<bool> hasAsked() async => (await _storage.read(key: _key)) == 'true';

  @override
  Future<void> markAsked() => _storage.write(key: _key, value: 'true');
}
