// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'lookup_staff_request.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

LookupStaffRequest _$LookupStaffRequestFromJson(Map<String, dynamic> json) =>
    LookupStaffRequest(
      staffIds: (json['staff_ids'] as List<dynamic>)
          .map((e) => e as String)
          .toList(),
    );

Map<String, dynamic> _$LookupStaffRequestToJson(LookupStaffRequest instance) =>
    <String, dynamic>{'staff_ids': instance.staffIds};
