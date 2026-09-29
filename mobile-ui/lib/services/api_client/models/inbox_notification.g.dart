// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'inbox_notification.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

InboxNotification _$InboxNotificationFromJson(Map<String, dynamic> json) =>
    InboxNotification(
      id: json['id'] as String?,
      type: json['type'] == null
          ? null
          : NotificationType.fromJson(json['type'] as String),
      title: json['title'] as String?,
      body: json['body'] as String?,
      entityType: json['entity_type'] as String?,
      entityId: json['entity_id'] as String?,
      readAt: json['read_at'] == null
          ? null
          : DateTime.parse(json['read_at'] as String),
      createdAt: json['created_at'] == null
          ? null
          : DateTime.parse(json['created_at'] as String),
    );

Map<String, dynamic> _$InboxNotificationToJson(InboxNotification instance) =>
    <String, dynamic>{
      'id': instance.id,
      'type': instance.type,
      'title': instance.title,
      'body': instance.body,
      'entity_type': instance.entityType,
      'entity_id': instance.entityId,
      'read_at': instance.readAt?.toIso8601String(),
      'created_at': instance.createdAt?.toIso8601String(),
    };
