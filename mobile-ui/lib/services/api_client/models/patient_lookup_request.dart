// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'patient_lookup_request.g.dart';

@JsonSerializable()
class PatientLookupRequest {
  const PatientLookupRequest({
    required this.nic,
  });
  
  factory PatientLookupRequest.fromJson(Map<String, Object?> json) => _$PatientLookupRequestFromJson(json);
  
  final String nic;

  Map<String, Object?> toJson() => _$PatientLookupRequestToJson(this);
}
