// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'address_suggestion.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AddressSuggestion _$AddressSuggestionFromJson(Map<String, dynamic> json) =>
    AddressSuggestion(
      label: json['label'] as String?,
      latitude: (json['latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble(),
      approximateAccuracyMetres: (json['approximate_accuracy_metres'] as num?)
          ?.toDouble(),
    );

Map<String, dynamic> _$AddressSuggestionToJson(AddressSuggestion instance) =>
    <String, dynamic>{
      'label': instance.label,
      'latitude': instance.latitude,
      'longitude': instance.longitude,
      'approximate_accuracy_metres': instance.approximateAccuracyMetres,
    };
