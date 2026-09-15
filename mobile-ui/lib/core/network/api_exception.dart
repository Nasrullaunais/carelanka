import 'package:dio/dio.dart';

// Branch on `code`, never on `message` — the text is translatable and will change.
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

  // Screens must handle this explicitly or a failed write reads as a success.
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

  // Keys arrive as either a field name (`full_name`) or a JSON path (`$.date_of_birth`) when the body failed to deserialize — the `$.` prefix is stripped so both map to the same field.
  static Map<String, List<String>> _readFieldErrors(Object? errors) {
    if (errors is! Map) return const {};

    final byField = <String, List<String>>{};

    for (final entry in errors.entries) {
      final field = entry.key.toString().replaceFirst(RegExp(r'^\$\.'), '');
      final messages = (entry.value as List?)?.map((e) => e.toString()).toList() ?? const [];

      // `request` refers to the whole body, not a form field — already reported against the field that caused it.
      if (field.isEmpty || field == 'request') continue;

      byField.putIfAbsent(field, () => []).addAll(messages);
    }

    return byField;
  }

  @override
  String toString() => message;
}
