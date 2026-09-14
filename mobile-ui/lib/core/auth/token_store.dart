import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Holds the JWT pair in the platform's encrypted store — Keystore on Android,
/// Keychain on iOS. Never in plain preferences.
class TokenStore {
  TokenStore([FlutterSecureStorage? storage])
      : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  static const _accessKey = 'carelanka.access_token';
  static const _refreshKey = 'carelanka.refresh_token';

  String? _cachedAccess;

  /// Read synchronously by the request interceptor, so it is kept in memory
  /// after the first load rather than hitting the keystore on every call.
  String? get cachedAccessToken => _cachedAccess;

  Future<String?> readAccessToken() async {
    return _cachedAccess ??= await _storage.read(key: _accessKey);
  }

  Future<String?> readRefreshToken() => _storage.read(key: _refreshKey);

  Future<void> save({required String accessToken, required String refreshToken}) async {
    _cachedAccess = accessToken;
    await _storage.write(key: _accessKey, value: accessToken);
    await _storage.write(key: _refreshKey, value: refreshToken);
  }

  Future<void> clear() async {
    _cachedAccess = null;
    await _storage.delete(key: _accessKey);
    await _storage.delete(key: _refreshKey);
  }
}
