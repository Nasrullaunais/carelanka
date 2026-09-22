// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'report_ambulance_location_request.g.dart';

@JsonSerializable()
class ReportAmbulanceLocationRequest {
  const ReportAmbulanceLocationRequest({
    this.latitude,
    this.longitude,
  });
  
  factory ReportAmbulanceLocationRequest.fromJson(Map<String, Object?> json) => _$ReportAmbulanceLocationRequestFromJson(json);
  
  final double? latitude;
  final double? longitude;

  Map<String, Object?> toJson() => _$ReportAmbulanceLocationRequestToJson(this);
}
