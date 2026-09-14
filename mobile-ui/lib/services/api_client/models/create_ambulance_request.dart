// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'create_ambulance_request.g.dart';

@JsonSerializable()
class CreateAmbulanceRequest {
  const CreateAmbulanceRequest({
    required this.registrationNumber,
    this.currentLatitude,
    this.currentLongitude,
  });
  
  factory CreateAmbulanceRequest.fromJson(Map<String, Object?> json) => _$CreateAmbulanceRequestFromJson(json);
  
  @JsonKey(name: 'registration_number')
  final String? registrationNumber;
  @JsonKey(name: 'current_latitude')
  final double? currentLatitude;
  @JsonKey(name: 'current_longitude')
  final double? currentLongitude;

  Map<String, Object?> toJson() => _$CreateAmbulanceRequestToJson(this);
}
