import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../services/api_client/models/care_recommendation_status.dart';
import '../../../services/api_client/models/care_reviewer_role.dart';
import '../../../services/api_client/models/my_care_recommendation.dart';
import '../services/patient_service.dart';
import '../validation/patient_fields.dart';
import 'panels.dart';

/// How many past reports the card shows. Older ones stay on the server; a patient scrolling
/// a stay's worth of messages inside one card on a home screen is worse than a short tail.
const _visibleHistory = 5;

/// A card inside My Stay, and nowhere else - it only exists while the patient is admitted, so
/// the 409 the server can return is a backstop rather than something ordinary use can reach.
/// Describes how the patient feels, in their own words, and starts the Patient Care Advisory
/// Agent. The agent's draft is never shown here - only a Doctor or Ward Nurse ever reads it,
/// and this screen shows nothing more than "reviewed" until they approve it.
///
/// Laid out as a conversation: what the patient sent sits on the right, what the ward replied
/// on the left, oldest first, with the box to write in underneath. Everything the ward sends
/// back carries the name of the doctor or nurse who signed it off.
class CareQueryCard extends StatefulWidget {
  const CareQueryCard({super.key});

  @override
  State<CareQueryCard> createState() => _CareQueryCardState();
}

class _CareQueryCardState extends State<CareQueryCard> {
  final _formKey = GlobalKey<FormState>();
  final _text = TextEditingController();

  bool _submitting = false;
  Future<List<MyCareRecommendation>>? _history;

  // Read once, from the same PatientService instance every other patient screen shares -
  // not a fresh one built from CareLankaApi, so a test's fake reaches this widget too.
  PatientService get _service => context.read<PatientService>();

  @override
  void initState() {
    super.initState();
    _loadHistory();
  }

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  void _loadHistory() {
    setState(() {
      _history = _service.loadMyCareRecommendations().then((page) => page.items);
    });
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _submitting = true);

    try {
      await _service.submitCareQuery(_text.text);
      if (!mounted) return;

      _text.clear();
      FocusScope.of(context).unfocus();
      _loadHistory();
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Sent. A nurse or doctor will look at this.'),
        ),
      );
    } on ApiException catch (error) {
      if (!mounted) return;

      final message = error.code == PatientService.notCurrentlyAdmittedForCareQueryCode
          ? 'This only works while you are admitted.'
          : error.message;

      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return SectionCard(
      title: 'How are you feeling?',
      icon: Icons.chat_bubble_outline,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'A nurse or doctor reads every message. Nothing here is answered automatically, '
            'and it is not for an emergency.',
            style: theme.textTheme.bodySmall?.copyWith(color: theme.colorScheme.onSurfaceVariant),
          ),
          _Conversation(future: _history, onRetry: _loadHistory),
          const SizedBox(height: 18),
          Form(
            key: _formKey,
            child: TextFormField(
              controller: _text,
              minLines: 2,
              maxLines: 4,
              maxLength: PatientFieldLimits.careReportMax,
              buildCounter: nearLimitCounter(),
              textCapitalization: TextCapitalization.sentences,
              validator: validateCareReport,
              enabled: !_submitting,
              decoration: const InputDecoration(
                hintText: 'e.g. My headache is worse today.',
                alignLabelWithHint: true,
              ),
            ),
          ),
          const SizedBox(height: 4),
          Align(
            alignment: Alignment.centerRight,
            child: FilledButton.icon(
              onPressed: _submitting ? null : _submit,
              style: FilledButton.styleFrom(
                minimumSize: const Size(0, 44),
                padding: const EdgeInsets.symmetric(horizontal: 22),
              ),
              icon: _submitting
                  ? const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.send_rounded, size: 18),
              label: Text(_submitting ? 'Sending…' : 'Send'),
            ),
          ),
        ],
      ),
    );
  }
}

/// The message thread. Oldest at the top, so it reads downwards like any other chat - the API
/// returns newest first, which is the right order for a page of results and the wrong one here.
class _Conversation extends StatelessWidget {
  const _Conversation({required this.future, required this.onRetry});

  final Future<List<MyCareRecommendation>>? future;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    if (future == null) return const SizedBox.shrink();

    return FutureBuilder<List<MyCareRecommendation>>(
      future: future,
      builder: (context, snapshot) {
        if (snapshot.connectionState != ConnectionState.done) {
          return const SizedBox.shrink();
        }

        if (snapshot.hasError) {
          return _ThreadNote(
            child: TextButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh, size: 16),
              label: const Text('Could not load your messages. Try again'),
            ),
          );
        }

        final all = snapshot.data ?? const <MyCareRecommendation>[];
        if (all.isEmpty) return const SizedBox.shrink();

        final shown = all.take(_visibleHistory).toList().reversed.toList();

        return Column(
          children: [
            const SizedBox(height: 20),
            if (all.length > shown.length)
              _ThreadNote(child: Text('Showing your last ${shown.length} messages')),
            for (var i = 0; i < shown.length; i++) ...[
              if (_startsNewDay(shown, i)) _DaySeparator(date: shown[i].reportedAt!),
              _Exchange(item: shown[i]),
            ],
          ],
        );
      },
    );
  }

  /// A date heading goes above the first message of each day, so every bubble underneath can
  /// show a bare clock time instead of repeating the date.
  static bool _startsNewDay(List<MyCareRecommendation> items, int index) {
    final current = items[index].reportedAt;
    if (current == null) return false;
    if (index == 0) return true;

    final previous = items[index - 1].reportedAt;
    if (previous == null) return true;

    return !_sameDay(current.toLocal(), previous.toLocal());
  }

  static bool _sameDay(DateTime a, DateTime b) =>
      a.year == b.year && a.month == b.month && a.day == b.day;
}

/// One report and whatever came back for it.
class _Exchange extends StatelessWidget {
  const _Exchange({required this.item});

  final MyCareRecommendation item;

  @override
  Widget build(BuildContext context) {
    final reply = item.status == CareRecommendationStatus.approved ? item.doctorMessage : null;

    return Padding(
      padding: const EdgeInsets.only(bottom: 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _Bubble(
            text: item.reportedText ?? '',
            fromMe: true,
            footer: item.reportedAt == null ? null : FriendlyDate.time(item.reportedAt!),
          ),
          const SizedBox(height: 8),
          if (reply != null)
            _Bubble(text: reply, fromMe: false, footer: _signature(item))
          else
            _PendingNote(status: item.status),
        ],
      ),
    );
  }

  /// The citation under a reply. A missing name means the reviewer's account has since been
  /// deactivated - the reply still came from a person, so say which kind rather than nothing.
  static String _signature(MyCareRecommendation item) {
    final at = item.reviewedAt;
    final time = at == null ? '' : ' · ${FriendlyDate.time(at)}';

    return '${_reviewer(item)}$time';
  }

  static String _reviewer(MyCareRecommendation item) {
    final name = item.reviewedByName?.trim();
    final named = name != null && name.isNotEmpty;

    return switch (item.reviewedByRole) {
      CareReviewerRole.doctor => named ? 'Approved by Dr. $name' : 'Approved by a doctor',
      CareReviewerRole.wardNurse => named ? 'Approved by Nurse $name' : 'Approved by a ward nurse',
      _ => named ? 'Approved by $name' : 'Approved by the ward',
    };
  }
}

/// A chat bubble. The patient's own words sit on the right in the accent colour; the ward's
/// reply sits on the left in a plain one. The inset on the far side is what makes a long
/// message still read as belonging to one side rather than filling the card.
class _Bubble extends StatelessWidget {
  const _Bubble({required this.text, required this.fromMe, this.footer});

  final String text;
  final bool fromMe;
  final String? footer;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    const corner = Radius.circular(AppTheme.radiusL);
    const tail = Radius.circular(AppTheme.radiusS);

    return Padding(
      padding: EdgeInsets.only(left: fromMe ? 36 : 0, right: fromMe ? 0 : 36),
      child: Column(
        crossAxisAlignment: fromMe ? CrossAxisAlignment.end : CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 11),
            decoration: BoxDecoration(
              color: fromMe ? scheme.primaryContainer : scheme.surfaceContainerHighest,
              border: fromMe ? null : Border.all(color: scheme.outlineVariant),
              borderRadius: BorderRadius.only(
                topLeft: corner,
                topRight: corner,
                bottomLeft: fromMe ? corner : tail,
                bottomRight: fromMe ? tail : corner,
              ),
            ),
            child: Text(
              text,
              style: theme.textTheme.bodyMedium?.copyWith(
                color: fromMe ? scheme.onPrimaryContainer : scheme.onSurface,
                height: 1.35,
              ),
            ),
          ),
          if (footer != null) ...[
            const SizedBox(height: 5),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 4),
              child: Text(
                footer!,
                style: theme.textTheme.labelSmall?.copyWith(color: scheme.onSurfaceVariant),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _DaySeparator extends StatelessWidget {
  const _DaySeparator({required this.date});

  final DateTime date;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Center(
        child: Text(
          FriendlyDate.relativeDay(date).toUpperCase(),
          style: theme.textTheme.labelSmall?.copyWith(
            color: theme.colorScheme.onSurfaceVariant,
            fontWeight: FontWeight.w700,
            letterSpacing: 0.8,
          ),
        ),
      ),
    );
  }
}

class _ThreadNote extends StatelessWidget {
  const _ThreadNote({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Center(
        child: DefaultTextStyle.merge(
          textAlign: TextAlign.center,
          style: theme.textTheme.labelSmall!.copyWith(color: theme.colorScheme.onSurfaceVariant),
          child: child,
        ),
      ),
    );
  }
}

/// What a patient sees before a reviewer has finished. A rejected report deliberately reads the
/// same as one still waiting: the reason is staff-facing and never reaches the patient.
class _PendingNote extends StatelessWidget {
  const _PendingNote({required this.status});

  final CareRecommendationStatus? status;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    final reviewed = status == CareRecommendationStatus.rejected;
    final text = reviewed
        ? 'Checked by a nurse or doctor. They will follow up with you in person.'
        : 'Waiting for a nurse or doctor to check this.';

    return Padding(
      padding: const EdgeInsets.only(right: 36),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(
            reviewed ? Icons.check_circle_outline : Icons.schedule,
            size: 14,
            color: scheme.onSurfaceVariant,
          ),
          const SizedBox(width: 6),
          Expanded(
            child: Text(
              text,
              style: theme.textTheme.bodySmall?.copyWith(color: scheme.onSurfaceVariant),
            ),
          ),
        ],
      ),
    );
  }
}
