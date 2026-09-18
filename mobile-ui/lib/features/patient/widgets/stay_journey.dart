import 'package:flutter/material.dart';
import 'package:flutter_animate/flutter_animate.dart';

import '../../../core/theme/app_theme.dart';
import '../../../services/api_client/models/admission_status.dart';

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

class StayJourney {
  const StayJourney({required this.reached, required this.cancelled});

  // Null only when the stay was cancelled before it started.
  final JourneyStep? reached;

  final bool cancelled;

  // Folds the seven stored AdmissionStatus values into WorklistStatus's four patient-facing steps (patient-spec.yaml), same as the ward board.
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

class StayJourneyTracker extends StatelessWidget {
  const StayJourneyTracker({super.key, required this.journey});

  final StayJourney journey;

  @override
  Widget build(BuildContext context) {
    if (journey.cancelled) {
      return const SizedBox.shrink();
    }

    return Column(
      children: [
        for (var i = 0; i < JourneyStep.values.length; i++)
          _Stop(
            step: JourneyStep.values[i],
            done: journey.isDone(JourneyStep.values[i]),
            current: journey.isCurrent(JourneyStep.values[i]),
            isLast: i == JourneyStep.values.length - 1,
            scheme: Theme.of(context).colorScheme,
            index: i,
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
    required this.index,
  });

  final JourneyStep step;
  final bool done;
  final bool current;
  final bool isLast;
  final ColorScheme scheme;
  final int index;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final reached = done || current;

    return IntrinsicHeight(
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Column(
            children: [
              _buildDot(theme),
              if (!isLast)
                Expanded(
                  child: Container(
                    width: 3,
                    margin: const EdgeInsets.symmetric(vertical: 4),
                    decoration: BoxDecoration(
                      borderRadius: BorderRadius.circular(2),
                      gradient: done
                          ? LinearGradient(
                              begin: Alignment.topCenter,
                              end: Alignment.bottomCenter,
                              colors: [
                                scheme.primary.withValues(alpha: 0.5),
                                scheme.primary.withValues(alpha: 0.2),
                              ],
                            )
                          : null,
                      color: done ? null : scheme.outlineVariant.withValues(alpha: 0.3),
                    ),
                  ),
                ),
            ],
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Padding(
              padding: EdgeInsets.only(top: 8, bottom: isLast ? 0 : 24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    step.label,
                    style: theme.textTheme.titleSmall?.copyWith(
                      color: reached ? scheme.onSurface : scheme.onSurfaceVariant.withValues(alpha: 0.6),
                      fontWeight: current ? FontWeight.w800 : FontWeight.w600,
                    ),
                  ),
                  if (current) ...[
                    const SizedBox(height: 4),
                    Text(
                      step.detail,
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: scheme.onSurfaceVariant,
                        height: 1.4,
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
          if (current)
            Container(
              margin: const EdgeInsets.only(top: 8),
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 5),
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  colors: [scheme.primary, scheme.primary.withValues(alpha: 0.8)],
                ),
                borderRadius: BorderRadius.circular(AppTheme.radiusS),
                boxShadow: [
                  BoxShadow(
                    color: scheme.primary.withValues(alpha: 0.2),
                    blurRadius: 8,
                    offset: const Offset(0, 2),
                  ),
                ],
              ),
              child: Text(
                'You are here',
                style: theme.textTheme.labelSmall?.copyWith(
                  color: scheme.onPrimary,
                  fontWeight: FontWeight.w700,
                ),
              ),
            )
                .animate(onPlay: (c) => c.repeat(reverse: true))
                .scale(
                  begin: const Offset(1, 1),
                  end: const Offset(1.03, 1.03),
                  duration: 1500.ms,
                  curve: Curves.easeInOut,
                ),
        ],
      ),
    ).animate().fadeIn(delay: (80 * index).ms, duration: 350.ms).slideX(begin: 0.05);
  }

  Widget _buildDot(ThemeData theme) {
    final size = current ? 42.0 : 38.0;

    if (current) {
      return Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [scheme.primary, scheme.primary.withValues(alpha: 0.7)],
          ),
          shape: BoxShape.circle,
          boxShadow: [
            BoxShadow(
              color: scheme.primary.withValues(alpha: 0.3),
              blurRadius: 12,
              offset: const Offset(0, 4),
            ),
          ],
        ),
        child: Icon(step.icon, size: 20, color: scheme.onPrimary),
      );
    }

    if (done) {
      return Container(
        width: size,
        height: size,
        decoration: BoxDecoration(
          color: scheme.primaryContainer,
          shape: BoxShape.circle,
        ),
        child: Icon(Icons.check_rounded, size: 20, color: scheme.primary),
      );
    }

    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: scheme.surfaceContainerHighest.withValues(alpha: 0.5),
        shape: BoxShape.circle,
        border: Border.all(
          color: scheme.outlineVariant.withValues(alpha: 0.3),
          width: 1.5,
        ),
      ),
      child: Icon(step.icon, size: 18, color: scheme.onSurfaceVariant.withValues(alpha: 0.5)),
    );
  }
}
