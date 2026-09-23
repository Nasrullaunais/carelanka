// coverage:ignore-file
// GENERATED CODE - DO NOT MODIFY BY HAND
// ignore_for_file: type=lint, unused_import, invalid_annotation_target, unnecessary_import

import 'package:json_annotation/json_annotation.dart';

import 'call_priority.dart';
import 'dispatch_status.dart';

part 'diversion_impact.g.dart';

@JsonSerializable()
class DiversionImpact {
  const DiversionImpact({
    this.sourceDispatchId,
    this.sourceCallId,
    this.sourceCallPriority,
    this.sourceCallAddressLabel,
    this.sourceDispatchStatus,
    this.sourceCallWaitingMinutesSoFar,
    this.sourceCallAdditionalWaitMinutes,
    this.replacementAmbulanceId,
    this.replacementAmbulanceRegistration,
    this.minutesSavedForThisCall,
  });

  factory DiversionImpact.fromJson(Map<String, Object?> json) =>
      _$DiversionImpactFromJson(json);

  @JsonKey(name: 'source_dispatch_id')
  final String? sourceDispatchId;
  @JsonKey(name: 'source_call_id')
  final String? sourceCallId;
  @JsonKey(name: 'source_call_priority')
  final CallPriority? sourceCallPriority;
  @JsonKey(name: 'source_call_address_label')
  final String? sourceCallAddressLabel;
  @JsonKey(name: 'source_dispatch_status')
  final DispatchStatus? sourceDispatchStatus;
  @JsonKey(name: 'source_call_waiting_minutes_so_far')
  final int? sourceCallWaitingMinutesSoFar;
  @JsonKey(name: 'source_call_additional_wait_minutes')
  final int? sourceCallAdditionalWaitMinutes;
  @JsonKey(name: 'replacement_ambulance_id')
  final String? replacementAmbulanceId;
  @JsonKey(name: 'replacement_ambulance_registration')
  final String? replacementAmbulanceRegistration;
  @JsonKey(name: 'minutes_saved_for_this_call')
  final int? minutesSavedForThisCall;

  Map<String, Object?> toJson() => _$DiversionImpactToJson(this);
}
