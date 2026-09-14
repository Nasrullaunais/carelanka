// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'validation_problem_details.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

ValidationProblemDetails _$ValidationProblemDetailsFromJson(
  Map<String, dynamic> json,
) => ValidationProblemDetails(
  type: json['type'] as String?,
  title: json['title'] as String?,
  status: (json['status'] as num?)?.toInt(),
  detail: json['detail'] as String?,
  instance: json['instance'] as String?,
  errors: (json['errors'] as Map<String, dynamic>?)?.map(
    (k, e) =>
        MapEntry(k, (e as List<dynamic>).map((e) => e as String).toList()),
  ),
);

Map<String, dynamic> _$ValidationProblemDetailsToJson(
  ValidationProblemDetails instance,
) => <String, dynamic>{
  'type': instance.type,
  'title': instance.title,
  'status': instance.status,
  'detail': instance.detail,
  'instance': instance.instance,
  'errors': instance.errors,
};
