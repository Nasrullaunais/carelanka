// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/device_registration.dart';
import '../models/register_device_request.dart';

part 'device_tokens_api.g.dart';

@RestApi()
abstract class DeviceTokensApi {
  factory DeviceTokensApi(Dio dio, {String? baseUrl}) = _DeviceTokensApi;

  @PUT('/device-tokens')
  Future<DeviceRegistration> registerDevice({
    @Body() RegisterDeviceRequest? body,
  });

  @DELETE('/device-tokens/{id}')
  Future<void> unregisterDevice({
    @Path('id') required String id,
  });
}
