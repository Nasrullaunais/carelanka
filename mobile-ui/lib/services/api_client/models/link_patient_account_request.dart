// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'link_patient_account_request.g.dart';

@JsonSerializable()
class LinkPatientAccountRequest {
  const LinkPatientAccountRequest({
    required this.userAccountId,
  });
  
  factory LinkPatientAccountRequest.fromJson(Map<String, Object?> json) => _$LinkPatientAccountRequestFromJson(json);
  
  @JsonKey(name: 'user_account_id')
  final String userAccountId;

  Map<String, Object?> toJson() => _$LinkPatientAccountRequestToJson(this);
}
