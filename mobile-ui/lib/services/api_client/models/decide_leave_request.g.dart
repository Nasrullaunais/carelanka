// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'decide_leave_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

DecideLeaveRequest _$DecideLeaveRequestFromJson(Map<String, dynamic> json) =>
    DecideLeaveRequest(
      decision: json['decision'] as String,
      notes: json['notes'] as String?,
    );

Map<String, dynamic> _$DecideLeaveRequestToJson(DecideLeaveRequest instance) =>
    <String, dynamic>{'decision': instance.decision, 'notes': instance.notes};
