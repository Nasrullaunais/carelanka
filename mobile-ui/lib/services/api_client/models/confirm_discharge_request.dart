// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'confirm_discharge_request.g.dart';

@JsonSerializable()
class ConfirmDischargeRequest {
  const ConfirmDischargeRequest({
    this.summaryNote,
  });
  
  factory ConfirmDischargeRequest.fromJson(Map<String, Object?> json) => _$ConfirmDischargeRequestFromJson(json);
  
  @JsonKey(name: 'summary_note')
  final String? summaryNote;

  Map<String, Object?> toJson() => _$ConfirmDischargeRequestToJson(this);
}
