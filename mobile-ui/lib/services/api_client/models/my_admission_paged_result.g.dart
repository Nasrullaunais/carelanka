// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'my_admission_paged_result.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

MyAdmissionPagedResult _$MyAdmissionPagedResultFromJson(
  Map<String, dynamic> json,
) => MyAdmissionPagedResult(
  items: (json['items'] as List<dynamic>)
      .map((e) => MyAdmission.fromJson(e as Map<String, dynamic>))
      .toList(),
  page: (json['page'] as num).toInt(),
  pageSize: (json['page_size'] as num).toInt(),
  totalItems: (json['total_items'] as num).toInt(),
  totalPages: (json['total_pages'] as num).toInt(),
);

Map<String, dynamic> _$MyAdmissionPagedResultToJson(
  MyAdmissionPagedResult instance,
) => <String, dynamic>{
  'items': instance.items,
  'page': instance.page,
  'page_size': instance.pageSize,
  'total_items': instance.totalItems,
  'total_pages': instance.totalPages,
};
