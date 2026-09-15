import 'package:dio/dio.dart';

import '../../services/api_client/care_lanka_api.dart';
import '../auth/session_expiry.dart';
import '../auth/token_store.dart';
import '../config/api_config.dart';
import 'api_exception.dart';
import 'auth_interceptor.dart';

CareLankaApi buildApi({
  required TokenStore tokens,
  required SessionExpiry sessionExpiry,
}) {
  final baseUrl = ApiConfig.baseUrl;
  final dio = Dio(BaseOptions(
    baseUrl: baseUrl,
    connectTimeout: const Duration(seconds: 15),
    receiveTimeout: const Duration(seconds: 30),
    validateStatus: (status) => status != null && status < 400,
  ));

  dio.interceptors.add(AuthInterceptor(
    tokens: tokens,
    sessionExpiry: sessionExpiry,
    baseUrl: baseUrl,
  ));

  return CareLankaApi(dio, baseUrl: baseUrl);
}

Future<T> callApi<T>(Future<T> Function() request) async {
  try {
    return await request();
  } on DioException catch (error) {
    throw ApiException.from(error);
  }
}
