class MyShiftItem {
  final String allocationId;
  final String shiftId;
  final String wardName;
  final String date;
  final String startTime;
  final String endTime;
  final bool crossesMidnight;
  final String status;
  final DateTime? clockedInAt;
  final DateTime? clockedOutAt;
  final bool canClockIn;
  final bool wasReassigned;

  const MyShiftItem({
    required this.allocationId,
    required this.shiftId,
    required this.wardName,
    required this.date,
    required this.startTime,
    required this.endTime,
    required this.crossesMidnight,
    required this.status,
    this.clockedInAt,
    this.clockedOutAt,
    required this.canClockIn,
    required this.wasReassigned,
  });

  bool get isClockedIn => clockedInAt != null && clockedOutAt == null;
  bool get isCompleted => clockedOutAt != null;
  bool get canClockOut => isClockedIn;

  DateTime? get parsedDate => DateTime.tryParse(date);

  factory MyShiftItem.fromJson(Map<String, dynamic> json) {
    return MyShiftItem(
      allocationId: json['allocation_id'] as String? ?? '',
      shiftId: json['shift_id'] as String? ?? '',
      wardName: json['ward_name'] as String? ?? 'General Ward',
      date: json['date'] as String? ?? '',
      startTime: json['start_time'] as String? ?? '',
      endTime: json['end_time'] as String? ?? '',
      crossesMidnight: json['crosses_midnight'] as bool? ?? false,
      status: json['status'] as String? ?? 'confirmed',
      clockedInAt: json['clocked_in_at'] != null
          ? DateTime.tryParse(json['clocked_in_at'] as String)
          : null,
      clockedOutAt: json['clocked_out_at'] != null
          ? DateTime.tryParse(json['clocked_out_at'] as String)
          : null,
      canClockIn: json['can_clock_in'] as bool? ?? false,
      wasReassigned: json['was_reassigned'] as bool? ?? false,
    );
  }
}
