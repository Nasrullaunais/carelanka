import 'package:dio/dio.dart';

import '../auth/session_expiry.dart';
import '../auth/token_store.dart';

/// Attaches the bearer token, and on a 401 tries the refresh token once before
/// giving up on the session.
class AuthInterceptor extends QueuedInterceptor {
  AuthInterceptor({
    required TokenStore tokens,
    required SessionExpiry sessionExpiry,
    required String baseUrl,
  })  : _tokens = tokens,
        _sessionExpiry = sessionExpiry,
        _refreshDio = Dio(BaseOptions(baseUrl: baseUrl));

  final TokenStore _tokens;
  final SessionExpiry _sessionExpiry;

  /// Deliberately has no interceptors: refreshing through the main client would
  /// recurse the moment the refresh call itself answered 401.
  final Dio _refreshDio;

  static const _retriedKey = 'carelanka.retried_after_refresh';

  static const _unauthenticatedPaths = {
    '/auth/login',
    '/auth/patient/login',
    '/auth/patient/register',
    '/auth/refresh',
  };

  @override
  Future<void> onRequest(RequestOptions options, RequestInterceptorHandler handler) async {
    if (!_unauthenticatedPaths.contains(options.path)) {
      final token = await _tokens.readAccessToken();
      if (token != null) {
        options.headers['Authorization'] = 'Bearer $token';
      }
    }
    handler.next(options);
  }

  @override
  Future<void> onError(DioException err, ErrorInterceptorHandler handler) async {
    final request = err.requestOptions;
    final shouldRefresh = err.response?.statusCode == 401 &&
        !_unauthenticatedPaths.contains(request.path) &&
        request.extra[_retriedKey] != true;

    if (!shouldRefresh) {
      handler.next(err);
      return;
    }

    final refreshed = await _refresh();
    if (!refreshed) {
      await _tokens.clear();
      _sessionExpiry.expire();
      handler.next(err);
      return;
    }

    request.extra[_retriedKey] = true;
    request.headers['Authorization'] = 'Bearer ${_tokens.cachedAccessToken}';

    try {
      final retried = await _refreshDio.fetch<dynamic>(request);
      handler.resolve(retried);
    } on DioException catch (e) {
      handler.next(e);
    }
  }

  Future<bool> _refresh() async {
    final refreshToken = await _tokens.readRefreshToken();
    if (refreshToken == null) return false;

    try {
      final response = await _refreshDio.post<Map<String, dynamic>>(
        '/auth/refresh',
        data: {'refresh_token': refreshToken},
      );
      final body = response.data;
      if (body == null) return false;

      await _tokens.save(
        accessToken: body['access_token'] as String,
        refreshToken: body['refresh_token'] as String,
      );
      return true;
    } on DioException {
      return false;
    }
  }
}
