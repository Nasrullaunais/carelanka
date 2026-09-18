import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../services/api_client/models/admission_status.dart';
import '../../../services/api_client/models/appointment_status.dart';

// Both switches below are exhaustive over a generated enum — a new API state fails the build here rather than silently falling through.
class StatusLook {
  const StatusLook({required this.icon, required this.color, required this.surface});

  final IconData icon;
  final Color color;
  final Color surface;

  static StatusLook ofAdmission(AdmissionStatus status, ColorScheme scheme) {
    return switch (status) {
      AdmissionStatus.awaitingBed => StatusLook(
          icon: Icons.hourglass_empty_rounded,
          color: scheme.warning,
          surface: scheme.warningSurface,
        ),
      AdmissionStatus.awaitingApproval => StatusLook(
          icon: Icons.how_to_reg_outlined,
          color: scheme.warning,
          surface: scheme.warningSurface,
        ),
      AdmissionStatus.bedReserved => StatusLook(
          icon: Icons.bed_outlined,
          color: scheme.primary,
          surface: scheme.primaryContainer,
        ),
      AdmissionStatus.admitted => StatusLook(
          icon: Icons.local_hospital_outlined,
          color: scheme.primary,
          surface: scheme.primaryContainer,
        ),
      AdmissionStatus.readyForDischarge => StatusLook(
          icon: Icons.luggage_outlined,
          color: scheme.primary,
          surface: scheme.primaryContainer,
        ),
      AdmissionStatus.discharged => StatusLook(
          icon: Icons.check_circle_outline,
          color: scheme.muted,
          surface: scheme.mutedSurface,
        ),
      AdmissionStatus.cancelled => StatusLook(
          icon: Icons.cancel_outlined,
          color: scheme.error,
          surface: scheme.errorContainer,
        ),
      AdmissionStatus.$unknown => StatusLook(
          icon: Icons.help_outline,
          color: scheme.muted,
          surface: scheme.mutedSurface,
        ),
    };
  }

  static StatusLook ofAppointment(AppointmentStatus status, ColorScheme scheme) {
    return switch (status) {
      AppointmentStatus.scheduled => StatusLook(
          icon: Icons.event_available_outlined,
          color: scheme.primary,
          surface: scheme.primaryContainer,
        ),
      AppointmentStatus.confirmed => StatusLook(
          icon: Icons.how_to_reg_outlined,
          color: scheme.primary,
          surface: scheme.primaryContainer,
        ),
      AppointmentStatus.completed => StatusLook(
          icon: Icons.check_circle_outline,
          color: scheme.muted,
          surface: scheme.mutedSurface,
        ),
      AppointmentStatus.cancelled => StatusLook(
          icon: Icons.cancel_outlined,
          color: scheme.error,
          surface: scheme.errorContainer,
        ),
      AppointmentStatus.noShow => StatusLook(
          icon: Icons.event_busy_outlined,
          color: scheme.warning,
          surface: scheme.warningSurface,
        ),
      AppointmentStatus.$unknown => StatusLook(
          icon: Icons.help_outline,
          color: scheme.muted,
          surface: scheme.mutedSurface,
        ),
    };
  }
}

class StatusChip extends StatelessWidget {
  const StatusChip({super.key, required this.label, required this.look, this.compact = false});

  StatusChip.admission({
    super.key,
    required this.label,
    required AdmissionStatus status,
    required ColorScheme scheme,
    this.compact = false,
  }) : look = StatusLook.ofAdmission(status, scheme);

  StatusChip.appointment({
    super.key,
    required this.label,
    required AppointmentStatus status,
    required ColorScheme scheme,
    this.compact = false,
  }) : look = StatusLook.ofAppointment(status, scheme);

  final String label;
  final StatusLook look;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: EdgeInsets.symmetric(
        horizontal: compact ? 10 : 14,
        vertical: compact ? 5 : 7,
      ),
      decoration: BoxDecoration(
        color: look.surface.withValues(alpha: 0.8),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(
          color: look.color.withValues(alpha: 0.15),
          width: 1,
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(look.icon, size: compact ? 13 : 16, color: look.color),
          SizedBox(width: compact ? 6 : 8),
          Flexible(
            child: Text(
              label,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                color: look.color,
                fontSize: compact ? 11.5 : 13,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
