// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'ambulance_status.dart';

part 'update_ambulance_request.g.dart';

@JsonSerializable()
class UpdateAmbulanceRequest {
  const UpdateAmbulanceRequest({
    this.status,
    this.registrationNumber,
    this.outOfServiceReason,
  });
  
  factory UpdateAmbulanceRequest.fromJson(Map<String, Object?> json) => _$UpdateAmbulanceRequestFromJson(json);
  
  final AmbulanceStatus? status;
  @JsonKey(name: 'registration_number')
  final String? registrationNumber;
  @JsonKey(name: 'out_of_service_reason')
  final String? outOfServiceReason;

  Map<String, Object?> toJson() => _$UpdateAmbulanceRequestToJson(this);
}
