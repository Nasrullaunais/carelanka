// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'current_principal.dart';

part 'auth_tokens.g.dart';

@JsonSerializable()
class AuthTokens {
  const AuthTokens({
    required this.accessToken,
    required this.tokenType,
    required this.expiresIn,
    required this.refreshToken,
    required this.principal,
  });
  
  factory AuthTokens.fromJson(Map<String, Object?> json) => _$AuthTokensFromJson(json);
  
  @JsonKey(name: 'access_token')
  final String accessToken;
  @JsonKey(name: 'token_type')
  final String tokenType;
  @JsonKey(name: 'expires_in')
  final int expiresIn;
  @JsonKey(name: 'refresh_token')
  final String refreshToken;
  final CurrentPrincipal principal;

  Map<String, Object?> toJson() => _$AuthTokensToJson(this);
}
