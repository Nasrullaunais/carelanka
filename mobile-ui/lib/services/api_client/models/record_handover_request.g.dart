// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'record_handover_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

RecordHandoverRequest _$RecordHandoverRequestFromJson(
  Map<String, dynamic> json,
) => RecordHandoverRequest(
  notes: json['notes'] as String?,
  patientCondition: json['patient_condition'] as String?,
);

Map<String, dynamic> _$RecordHandoverRequestToJson(
  RecordHandoverRequest instance,
) => <String, dynamic>{
  'notes': instance.notes,
  'patient_condition': instance.patientCondition,
};
