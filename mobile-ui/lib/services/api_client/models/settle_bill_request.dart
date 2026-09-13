// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'settle_bill_request.g.dart';

@JsonSerializable()
class SettleBillRequest {
  const SettleBillRequest({
    this.settlementNote,
  });
  
  factory SettleBillRequest.fromJson(Map<String, Object?> json) => _$SettleBillRequestFromJson(json);
  
  @JsonKey(name: 'settlement_note')
  final String? settlementNote;

  Map<String, Object?> toJson() => _$SettleBillRequestToJson(this);
}
