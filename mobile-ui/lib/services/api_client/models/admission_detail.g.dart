// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'admission_detail.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AdmissionDetail _$AdmissionDetailFromJson(Map<String, dynamic> json) =>
    AdmissionDetail(
      id: json['id'] as String,
      source: AdmissionSource.fromJson(json['source'] as String),
      admissionCategory: AdmissionCategory.fromJson(
        json['admission_category'] as String,
      ),
      urgency: AdmissionUrgency.fromJson(json['urgency'] as String),
      status: AdmissionStatus.fromJson(json['status'] as String),
      detailsComplete: json['details_complete'] as bool,
      requiresBed: json['requires_bed'] as bool,
      categorySetByStaffId: json['category_set_by_staff_id'] as String,
      categorySetAt: DateTime.parse(json['category_set_at'] as String),
      isInfectious: json['is_infectious'] as bool,
      missingFields: (json['missing_fields'] as List<dynamic>)
          .map((e) => e as String)
          .toList(),
      bedAssignments: (json['bed_assignments'] as List<dynamic>)
          .map((e) => BedAssignment.fromJson(e as Map<String, dynamic>))
          .toList(),
      patient: json['patient'] == null
          ? null
          : PatientSummary.fromJson(json['patient'] as Map<String, dynamic>),
      wardName: json['ward_name'] as String?,
      bedNumber: json['bed_number'] as String?,
      expectedArrival: json['expected_arrival'] == null
          ? null
          : DateTime.parse(json['expected_arrival'] as String),
      admittedAt: json['admitted_at'] == null
          ? null
          : DateTime.parse(json['admitted_at'] as String),
      dispatchId: json['dispatch_id'] as String?,
      categorySetByStaffName: json['category_set_by_staff_name'] as String?,
      reportedByUserId: json['reported_by_user_id'] as String?,
      dischargedAt: json['discharged_at'] == null
          ? null
          : DateTime.parse(json['discharged_at'] as String),
      cancelReason: json['cancel_reason'] == null
          ? null
          : CancelReason.fromJson(json['cancel_reason'] as String),
      cancelNote: json['cancel_note'] as String?,
      createdAt: json['created_at'] == null
          ? null
          : DateTime.parse(json['created_at'] as String),
      updatedAt: json['updated_at'] == null
          ? null
          : DateTime.parse(json['updated_at'] as String),
      discharge: json['discharge'] == null
          ? null
          : Discharge.fromJson(json['discharge'] as Map<String, dynamic>),
      bill: json['bill'] == null
          ? null
          : Bill.fromJson(json['bill'] as Map<String, dynamic>),
    );

Map<String, dynamic> _$AdmissionDetailToJson(AdmissionDetail instance) =>
    <String, dynamic>{
      'id': instance.id,
      'patient': instance.patient,
      'source': instance.source,
      'admission_category': instance.admissionCategory,
      'urgency': instance.urgency,
      'status': instance.status,
      'details_complete': instance.detailsComplete,
      'requires_bed': instance.requiresBed,
      'ward_name': instance.wardName,
      'bed_number': instance.bedNumber,
      'expected_arrival': instance.expectedArrival?.toIso8601String(),
      'admitted_at': instance.admittedAt?.toIso8601String(),
      'dispatch_id': instance.dispatchId,
      'category_set_by_staff_id': instance.categorySetByStaffId,
      'category_set_by_staff_name': instance.categorySetByStaffName,
      'category_set_at': instance.categorySetAt.toIso8601String(),
      'is_infectious': instance.isInfectious,
      'reported_by_user_id': instance.reportedByUserId,
      'missing_fields': instance.missingFields,
      'discharged_at': instance.dischargedAt?.toIso8601String(),
      'cancel_reason': instance.cancelReason,
      'cancel_note': instance.cancelNote,
      'created_at': instance.createdAt?.toIso8601String(),
      'updated_at': instance.updatedAt?.toIso8601String(),
      'bed_assignments': instance.bedAssignments,
      'discharge': instance.discharge,
      'bill': instance.bill,
    };
