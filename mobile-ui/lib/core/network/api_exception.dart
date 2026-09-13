import 'package:dio/dio.dart';

/// A failed API call, already turned into something a screen can display.
///
/// The API answers with `application/problem+json`, carrying a stable
/// machine-readable code in `code` (`cl_pat_033`, `cl_adm_003`, …). Branch on
/// [code], never on [message] — the text is translatable and will change.
class ApiException implements Exception {
  const ApiException({
    required this.message,
    this.statusCode,
    this.code,
    this.fieldErrors = const {},
  });

  final String message;
  final int? statusCode;
  final String? code;
  final Map<String, List<String>> fieldErrors;

  bool get isUnauthorized => statusCode == 401;
  bool get isForbidden => statusCode == 403;
  bool get isNotFound => statusCode == 404;
  bool get isConflict => statusCode == 409;

  /// True when the request never reached the server, so there is no response
  /// to classify — a screen must handle this or it will report a write that
  /// never happened as a success.
  bool get isNetworkFailure => statusCode == null;

  factory ApiException.from(DioException error) {
    final response = error.response;
    if (response == null) {
      return const ApiException(
        message: 'Could not reach the server. Check your connection and try again.',
      );
    }

    final body = response.data;
    if (body is! Map) {
      return ApiException(
        message: 'Something went wrong (${response.statusCode}).',
        statusCode: response.statusCode,
      );
    }

    return ApiException(
      message: (body['detail'] ?? body['title'] ?? 'Something went wrong.') as String,
      statusCode: response.statusCode,
      code: body['code'] as String?,
      fieldErrors: _readFieldErrors(body['errors']),
    );
  }

  static Map<String, List<String>> _readFieldErrors(Object? errors) {
    if (errors is! Map) return const {};
    return {
      for (final entry in errors.entries)
        entry.key.toString(): (entry.value as List?)?.map((e) => e.toString()).toList() ?? const [],
    };
  }

  @override
  String toString() => message;
}
