import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../services/api_client/models/admission_status.dart';
import '../../../services/api_client/models/appointment_status.dart';

/// How a status looks. Colour and icon only — the *words* always come from the
/// server's `status_text`, so the app and the ward never say different things.
///
/// Three colours, not seven. Every state is one of:
///   **teal**  — on track, nothing for you to do
///   **amber** — waiting on somebody, or something is needed from you
///   **red**   — called off
///   **grey**  — finished, in the past
///
/// Giving each of the seven states its own colour was the first attempt. It
/// made a list of visits look like a paint chart and, worse, gave no hint which
/// row mattered. A reader can hold three meanings in their head, not seven.
///
/// Both switches are exhaustive over a generated enum. Add a state to the API
/// and this file stops compiling, which is the point.
class StatusLook {
  const StatusLook({required this.icon, required this.color, required this.surface});

  final IconData icon;
  final Color color;
  final Color surface;

  static StatusLook ofAdmission(AdmissionStatus status, ColorScheme scheme) {
    return switch (status) {
      // Nobody has a bed for them yet, and that is the ward's move to make.
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
      AppointmentStatus.checkedIn => StatusLook(
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
      // Not an error the patient can fix, but not a normal ending either.
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

/// The server's status sentence, wearing its colour.
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
      padding: EdgeInsets.symmetric(horizontal: compact ? 9 : 12, vertical: compact ? 5 : 7),
      decoration: BoxDecoration(
        color: look.surface,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(look.icon, size: compact ? 13 : 16, color: look.color),
          SizedBox(width: compact ? 6 : 7),
          Flexible(
            child: Text(
              label,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                color: look.color,
                fontSize: compact ? 11.5 : 13,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
