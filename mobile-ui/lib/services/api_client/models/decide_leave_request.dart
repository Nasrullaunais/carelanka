// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'decide_leave_request.g.dart';

@JsonSerializable()
class DecideLeaveRequest {
  const DecideLeaveRequest({
    required this.decision,
    this.notes,
  });
  
  factory DecideLeaveRequest.fromJson(Map<String, Object?> json) => _$DecideLeaveRequestFromJson(json);
  
  final String decision;
  final String? notes;

  Map<String, Object?> toJson() => _$DecideLeaveRequestToJson(this);
}
