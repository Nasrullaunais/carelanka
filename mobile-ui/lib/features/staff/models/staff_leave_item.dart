class StaffLeaveItem {
  final String id;
  final String type;
  final bool isUrgent;
  final String startDate;
  final String endDate;
  final String? reason;
  final String status;
  final String? reviewedByStaffId;
  final DateTime? reviewedAt;
  final String? reviewNotes;
  final String? swapWithStaffMemberId;
  final String? swapShiftId;
  final DateTime? createdAt;
  final int affectedShiftsCount;

  const StaffLeaveItem({
    required this.id,
    required this.type,
    required this.isUrgent,
    required this.startDate,
    required this.endDate,
    this.reason,
    required this.status,
    this.reviewedByStaffId,
    this.reviewedAt,
    this.reviewNotes,
    this.swapWithStaffMemberId,
    this.swapShiftId,
    this.createdAt,
    this.affectedShiftsCount = 0,
  });

  DateTime? get parsedStartDate => DateTime.tryParse(startDate);
  DateTime? get parsedEndDate => DateTime.tryParse(endDate);

  int get totalDays {
    final s = parsedStartDate;
    final e = parsedEndDate;
    if (s == null || e == null) return 1;
    final diff = e.difference(s).inDays;
    return diff < 0 ? 1 : diff + 1;
  }

  String get typeDisplay => switch (type) {
        'annual' => 'Annual Leave',
        'sick' => 'Sick Leave',
        'emergency' => 'Emergency Leave',
        'shift_swap' => 'Shift Swap',
        _ => type,
      };

  String get statusDisplay => switch (status) {
        'pending' => 'Pending',
        'approved' => 'Approved',
        'rejected' => 'Rejected',
        'withdrawn' => 'Withdrawn',
        _ => status,
      };

  bool get canWithdraw => status == 'pending';

  factory StaffLeaveItem.fromJson(Map<String, dynamic> json) {
    final affected = json['affected_shifts'];
    final affectedCount = affected is List ? affected.length : 0;

    return StaffLeaveItem(
      id: json['id'] as String? ?? '',
      type: json['type'] as String? ?? 'annual',
      isUrgent: json['is_urgent'] as bool? ?? false,
      startDate: json['start_date'] as String? ?? '',
      endDate: json['end_date'] as String? ?? '',
      reason: json['reason'] as String?,
      status: json['status'] as String? ?? 'pending',
      reviewedByStaffId: json['reviewed_by_staff_id'] as String?,
      reviewedAt: json['reviewed_at'] != null
          ? DateTime.tryParse(json['reviewed_at'] as String)
          : null,
      reviewNotes: json['review_notes'] as String?,
      swapWithStaffMemberId: json['swap_with_staff_member_id'] as String?,
      swapShiftId: json['swap_shift_id'] as String?,
      createdAt: json['created_at'] != null
          ? DateTime.tryParse(json['created_at'] as String)
          : null,
      affectedShiftsCount: affectedCount,
    );
  }
}
