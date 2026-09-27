// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'diversion_impact.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DiversionImpact _$DiversionImpactFromJson(Map<String, dynamic> json) =>
    DiversionImpact(
      sourceDispatchId: json['source_dispatch_id'] as String?,
      sourceCallId: json['source_call_id'] as String?,
      sourceCallPriority: json['source_call_priority'] == null
          ? null
          : CallPriority.fromJson(json['source_call_priority'] as String),
      sourceCallAddressLabel: json['source_call_address_label'] as String?,
      sourceDispatchStatus: json['source_dispatch_status'] == null
          ? null
          : DispatchStatus.fromJson(json['source_dispatch_status'] as String),
      sourceCallWaitingMinutesSoFar:
          (json['source_call_waiting_minutes_so_far'] as num?)?.toInt(),
      sourceCallAdditionalWaitMinutes:
          (json['source_call_additional_wait_minutes'] as num?)?.toInt(),
      replacementAmbulanceId: json['replacement_ambulance_id'] as String?,
      replacementAmbulanceRegistration:
          json['replacement_ambulance_registration'] as String?,
      minutesSavedForThisCall: (json['minutes_saved_for_this_call'] as num?)
          ?.toInt(),
    );

Map<String, dynamic> _$DiversionImpactToJson(
  DiversionImpact instance,
) => <String, dynamic>{
  'source_dispatch_id': instance.sourceDispatchId,
  'source_call_id': instance.sourceCallId,
  'source_call_priority': instance.sourceCallPriority,
  'source_call_address_label': instance.sourceCallAddressLabel,
  'source_dispatch_status': instance.sourceDispatchStatus,
  'source_call_waiting_minutes_so_far': instance.sourceCallWaitingMinutesSoFar,
  'source_call_additional_wait_minutes':
      instance.sourceCallAdditionalWaitMinutes,
  'replacement_ambulance_id': instance.replacementAmbulanceId,
  'replacement_ambulance_registration':
      instance.replacementAmbulanceRegistration,
  'minutes_saved_for_this_call': instance.minutesSavedForThisCall,
};
