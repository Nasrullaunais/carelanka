// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'dispatch_status.dart';

part 'update_my_dispatch_status_request.g.dart';

@JsonSerializable()
class UpdateMyDispatchStatusRequest {
  const UpdateMyDispatchStatusRequest({
    this.status,
    this.latitude,
    this.longitude,
  });
  
  factory UpdateMyDispatchStatusRequest.fromJson(Map<String, Object?> json) => _$UpdateMyDispatchStatusRequestFromJson(json);
  
  final DispatchStatus? status;
  final double? latitude;
  final double? longitude;

  Map<String, Object?> toJson() => _$UpdateMyDispatchStatusRequestToJson(this);
}
