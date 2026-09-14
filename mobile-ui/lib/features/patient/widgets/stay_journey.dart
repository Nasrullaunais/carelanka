import 'package:flutter/material.dart';

import '../../../core/theme/app_theme.dart';
import '../../../services/api_client/models/admission_status.dart';

/// One stop on the way through a hospital stay.
enum JourneyStep {
  waitingForBed('Waiting for a bed', 'Reception is finding you a bed', Icons.hourglass_empty_rounded),
  bedReady('Bed ready', 'A bed is being held for you', Icons.bed_outlined),
  inHospital('In hospital', 'You are in your bed and being treated', Icons.local_hospital_outlined),
  home('Home', 'Your stay is finished', Icons.home_outlined);

  const JourneyStep(this.label, this.detail, this.icon);

  final String label;
  final String detail;
  final IconData icon;
}

/// Where the patient is, and whether the stay ended early.
class StayJourney {
  const StayJourney({required this.reached, required this.cancelled});

  /// The step the patient is standing on. Null only when the stay was called
  /// off before it started.
  final JourneyStep? reached;

  final bool cancelled;

  /// The four steps come from `patient-spec.yaml`'s own `WorklistStatus`
  /// grouping of the seven stored states — the same folding the ward board
  /// uses, so the patient and the ward are reading one journey, not two.
  ///
  /// `awaiting_approval` sits with `awaiting_bed`, and `ready_for_discharge`
  /// with `admitted`, because in both cases nothing has changed about where the
  /// patient physically is.
  factory StayJourney.of(AdmissionStatus status) {
    return switch (status) {
      AdmissionStatus.awaitingBed ||
      AdmissionStatus.awaitingApproval =>
        const StayJourney(reached: JourneyStep.waitingForBed, cancelled: false),
      AdmissionStatus.bedReserved =>
        const StayJourney(reached: JourneyStep.bedReady, cancelled: false),
      AdmissionStatus.admitted ||
      AdmissionStatus.readyForDischarge =>
        const StayJourney(reached: JourneyStep.inHospital, cancelled: false),
      AdmissionStatus.discharged =>
        const StayJourney(reached: JourneyStep.home, cancelled: false),
      AdmissionStatus.cancelled => const StayJourney(reached: null, cancelled: true),
      AdmissionStatus.$unknown => const StayJourney(reached: null, cancelled: false),
    };
  }

  bool isDone(JourneyStep step) =>
      reached != null && step.index < reached!.index;

  bool isCurrent(JourneyStep step) => step == reached;
}

/// The journey drawn as a rail, so "where am I up to" is one glance rather
/// than a sentence that has to be decoded.
class StayJourneyTracker extends StatelessWidget {
  const StayJourneyTracker({super.key, required this.journey});

  final StayJourney journey;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    if (journey.cancelled) {
      return const SizedBox.shrink();
    }

    return Column(
      children: [
        for (final step in JourneyStep.values)
          _Stop(
            step: step,
            done: journey.isDone(step),
            current: journey.isCurrent(step),
            isLast: step == JourneyStep.values.last,
            scheme: theme.colorScheme,
          ),
      ],
    );
  }
}

class _Stop extends StatelessWidget {
  const _Stop({
    required this.step,
    required this.done,
    required this.current,
    required this.isLast,
    required this.scheme,
  });

  final JourneyStep step;
  final bool done;
  final bool current;
  final bool isLast;
  final ColorScheme scheme;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final reached = done || current;

    final dotColor = current
        ? scheme.primary
        : done
            ? scheme.primary.withValues(alpha: 0.35)
            : scheme.surfaceContainerHighest;

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Column(
            children: [
              Container(
                width: 34,
                height: 34,
                decoration: BoxDecoration(color: dotColor, shape: BoxShape.circle),
                child: Icon(
                  done ? Icons.check : step.icon,
                  size: 18,
                  color: reached ? scheme.onPrimary : scheme.onSurfaceVariant,
                ),
              ),
              if (!isLast)
                Expanded(
                  child: Container(
                    width: 2,
                    margin: const EdgeInsets.symmetric(vertical: 4),
                    color: done ? scheme.primary.withValues(alpha: 0.35) : scheme.outlineVariant,
                  ),
                ),
            ],
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(top: 5, bottom: isLast ? 0 : 22),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    step.label,
                    style: theme.textTheme.titleSmall?.copyWith(
                      color: reached ? scheme.onSurface : scheme.onSurfaceVariant,
                      fontWeight: current ? FontWeight.w700 : FontWeight.w600,
                    ),
                  ),
                  if (current) ...[
                    const SizedBox(height: 2),
                    Text(
                      step.detail,
                      style: theme.textTheme.bodySmall
                          ?.copyWith(color: scheme.onSurfaceVariant),
                    ),
                  ],
                ],
              ),
            ),
          ),
          if (current)
            Container(
              margin: const EdgeInsets.only(top: 6),
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
              decoration: BoxDecoration(
                color: scheme.primaryContainer,
                borderRadius: BorderRadius.circular(AppTheme.radiusS),
              ),
              child: Text(
                'You are here',
                style: theme.textTheme.labelSmall?.copyWith(
                  color: scheme.onPrimaryContainer,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
        ],
      ),
    );
  }
}
