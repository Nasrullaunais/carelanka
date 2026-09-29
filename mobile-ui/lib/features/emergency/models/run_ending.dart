import '../../../core/utils/friendly_date.dart';
import '../../../services/api_client/models/dispatch_detail.dart';
import '../../../services/api_client/models/dispatch_status.dart';

enum RunEndingKind {
  cancelled,
  reassigned,
  diverted,
  handedOver,
  endedAtScene,
  unavailable,
}

final class RunEnding {
  const RunEnding._(
    this.kind, {
    this.reason,
    this.registration,
    this.completedAt,
  });

  const RunEnding.unavailable() : this._(RunEndingKind.unavailable);

  final RunEndingKind kind;
  final String? reason;
  final String? registration;
  final DateTime? completedAt;

  /// Null for a run the crew declined themselves: they already know.
  static RunEnding? from(DispatchDetail ended, {String? activeRunId}) {
    final reason = _clean(switch (ended.status) {
      DispatchStatus.cancelled => ended.cancellationReason,
      DispatchStatus.reassigned => ended.reassignmentReason,
      _ => null,
    });
    RunEnding of(RunEndingKind kind) => RunEnding._(
      kind,
      reason: reason,
      registration: ended.ambulanceRegistration,
      completedAt: ended.completedAt,
    );

    return switch (ended.status) {
      DispatchStatus.cancelled => of(RunEndingKind.cancelled),
      DispatchStatus.reassigned => of(
        activeRunId != null && ended.supersededByDispatchId == activeRunId
            ? RunEndingKind.diverted
            : RunEndingKind.reassigned,
      ),
      DispatchStatus.handedOver => of(RunEndingKind.handedOver),
      DispatchStatus.endedAtScene => of(RunEndingKind.endedAtScene),
      _ => null,
    };
  }

  String get title => switch (kind) {
    RunEndingKind.cancelled => 'Run cancelled',
    RunEndingKind.reassigned => 'Run given to another ambulance',
    RunEndingKind.diverted => 'You have been sent to a more urgent call',
    RunEndingKind.handedOver =>
      completedAt == null
          ? 'Handover recorded'
          : 'Handover recorded at ${FriendlyDate.time(completedAt!)}',
    RunEndingKind.endedAtScene => 'Run finished at the scene',
    RunEndingKind.unavailable => 'This run is no longer assigned to you',
  };

  String get message => switch (kind) {
    RunEndingKind.cancelled => [
      if (reason != null) 'Reason: $reason',
      'You can stop driving.',
    ].join('\n'),
    RunEndingKind.reassigned =>
      reason == null
          ? 'Another ambulance is taking this call.'
          : 'Reason: $reason',
    RunEndingKind.diverted => 'Accept the new run below.',
    RunEndingKind.handedOver || RunEndingKind.endedAtScene =>
      '${registration ?? 'Your ambulance'} is available for the next run.',
    RunEndingKind.unavailable => 'Check with the duty manager if unsure.',
  };

  static String? _clean(String? text) {
    final trimmed = text?.trim();
    return trimmed == null || trimmed.isEmpty ? null : trimmed;
  }
}
