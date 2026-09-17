// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

part 'assign_ambulance_crew_request.g.dart';

@JsonSerializable()
class AssignAmbulanceCrewRequest {
  const AssignAmbulanceCrewRequest({
    required this.staffMemberId,
  });

  factory AssignAmbulanceCrewRequest.fromJson(Map<String, Object?> json) => _$AssignAmbulanceCrewRequestFromJson(json);

  @JsonKey(name: 'staff_member_id')
  final String staffMemberId;

  Map<String, Object?> toJson() => _$AssignAmbulanceCrewRequestToJson(this);
}
