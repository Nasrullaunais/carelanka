import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../../services/api_client/care_lanka_api.dart';
import '../auth/session_expiry.dart';
import '../auth/token_store.dart';
import '../config/api_config.dart';
import 'api_exception.dart';
import 'auth_interceptor.dart';

/// [dio] is also returned for the rare request the generated client cannot make -
/// the retrofit generator decodes any binary response through `utf8.decoder`
/// (`downloadLabReport`, `downloadMyLabReport`), which corrupts a PDF or image. Fetch
/// those with `dio.get(path, options: Options(responseType: ResponseType.bytes))`
/// instead; the auth interceptor is on the same instance, so the token still attaches.
({CareLankaApi api, Dio dio}) buildApi({
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

  return (api: CareLankaApi(dio, baseUrl: baseUrl), dio: dio);
}

Future<T> callApi<T>(Future<T> Function() request) async {
  try {
    return await request();
  } on DioException catch (error) {
    throw ApiException.from(error);
  }
}

/// Fetches a binary file from [path] (relative to the API base URL) with the same
/// auth header every other request gets. The generated client cannot do this itself -
/// its retrofit binding decodes any binary GET through `utf8.decoder`, which corrupts
/// a PDF or image - so this goes around it with `dio` directly.
Future<Uint8List> downloadBytes(Dio dio, String path) {
  return callApi(() async {
    final response = await dio.get<List<int>>(
      path,
      options: Options(responseType: ResponseType.bytes),
    );
    return Uint8List.fromList(response.data!);
  });
}
