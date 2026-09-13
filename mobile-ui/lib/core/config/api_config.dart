import 'package:flutter/foundation.dart';

/// Where the ASP.NET API lives, per platform.
///
/// The OpenAPI document publishes `servers: /api`, which is relative and
/// unusable from a native client, so the host is supplied here instead.
class ApiConfig {
  const ApiConfig._();

  static const _override = String.fromEnvironment('API_BASE_URL');

  static const _port = 5231;

  static String get baseUrl {
    if (_override.isNotEmpty) return _override;

    // Inside an Android emulator `localhost` is the emulated phone itself.
    // The machine running the API is reachable at 10.0.2.2.
    if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) {
      return 'http://10.0.2.2:$_port/api';
    }
    return 'http://localhost:$_port/api';
  }
}
