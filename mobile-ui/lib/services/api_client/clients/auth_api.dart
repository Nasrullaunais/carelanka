// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import '../models/auth_tokens.dart';
import '../models/current_principal.dart';
import '../models/patient_login_request.dart';
import '../models/patient_register_request.dart';
import '../models/refresh_token_request.dart';
import '../models/staff_login_request.dart';

part 'auth_api.g.dart';

@RestApi()
abstract class AuthApi {
  factory AuthApi(Dio dio, {String? baseUrl}) = _AuthApi;

  @POST('/auth/login')
  Future<AuthTokens> login({
    @Body() StaffLoginRequest? body,
  });

  @POST('/auth/patient/register')
  Future<AuthTokens> registerPatientAccount({
    @Body() PatientRegisterRequest? body,
  });

  @POST('/auth/patient/login')
  Future<AuthTokens> loginPatient({
    @Body() PatientLoginRequest? body,
  });

  @POST('/auth/refresh')
  Future<AuthTokens> refreshToken({
    @Body() RefreshTokenRequest? body,
  });

  @POST('/auth/logout')
  Future<void> logout({
    @Body() RefreshTokenRequest? body,
  });

  @GET('/auth/me')
  Future<CurrentPrincipal> getCurrentUser();
}
