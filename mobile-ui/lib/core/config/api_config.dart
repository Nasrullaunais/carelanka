import 'package:flutter/foundation.dart';

// Hardcoded here because the OpenAPI doc publishes a relative `servers: /api`, unusable from a native client.
class ApiConfig {
  const ApiConfig._();

  static const _override = String.fromEnvironment('API_BASE_URL');

  static const _port = 5231;

  static String get baseUrl {
    if (_override.isNotEmpty) return _override;

    // Inside an Android emulator, localhost is the emulated phone itself — the host machine is 10.0.2.2.
    if (!kIsWeb && defaultTargetPlatform == TargetPlatform.android) {
      return 'http://10.0.2.2:$_port/api';
    }
    return 'http://localhost:$_port/api';
  }
}
