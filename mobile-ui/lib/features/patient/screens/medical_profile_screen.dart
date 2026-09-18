import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/async_view.dart';
import '../../../core/widgets/phone_width.dart';
import '../../../services/api_client/models/patient_medical_profile.dart';
import '../../../services/api_client/models/update_medical_profile_request.dart';
import '../state/medical_profile_controller.dart';

/// The four boxes the care advisory agent reads. Everything on this screen is typed by the
/// nurse standing at the bed — nothing here is filled in by the system, which is what keeps it
/// a handover note rather than a medical record.
class MedicalProfileScreen extends StatefulWidget {
  const MedicalProfileScreen({super.key, required this.patientName});

  final String patientName;

  @override
  State<MedicalProfileScreen> createState() => _MedicalProfileScreenState();
}

class _MedicalProfileScreenState extends State<MedicalProfileScreen> {
  final _knownConditions = TextEditingController();
  final _allergies = TextEditingController();
  final _currentSymptoms = TextEditingController();
  final _recentSituation = TextEditingController();

  bool _seeded = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<MedicalProfileController>().load();
    });
  }

  @override
  void dispose() {
    for (final controller in [
      _knownConditions,
      _allergies,
      _currentSymptoms,
      _recentSituation,
    ]) {
      controller.dispose();
    }
    super.dispose();
  }

  // The boxes are filled from the server's copy once, when it first arrives. Re-seeding on every
  // rebuild would wipe out whatever the nurse is halfway through typing.
  void _seed(PatientMedicalProfile profile) {
    if (_seeded) return;
    _seeded = true;
    _knownConditions.text = profile.knownConditions ?? '';
    _allergies.text = profile.allergies ?? '';
    _currentSymptoms.text = profile.currentSymptoms ?? '';
    _recentSituation.text = profile.recentSituation ?? '';
  }

  Future<void> _submit() async {
    final controller = context.read<MedicalProfileController>();

    final saved = await controller.save(UpdateMedicalProfileRequest(
      knownConditions: _emptyToNull(_knownConditions.text),
      allergies: _emptyToNull(_allergies.text),
      currentSymptoms: _emptyToNull(_currentSymptoms.text),
      recentSituation: _emptyToNull(_recentSituation.text),
    ));

    if (!mounted) return;

    if (!saved) {
      final message = controller.saveError?.message ?? 'Could not save the medical details.';
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
      return;
    }

    Navigator.of(context).pop();
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Medical details saved.')),
    );
  }

  static String? _emptyToNull(String value) {
    final trimmed = value.trim();
    return trimmed.isEmpty ? null : trimmed;
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<MedicalProfileController>();
    final theme = Theme.of(context);

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(title: const Text('Medical details')),
        body: AsyncView<PatientMedicalProfile>(
          state: controller.profile,
          onRetry: controller.load,
          builder: (context, profile) {
            _seed(profile);

            return ListView(
              padding: const EdgeInsets.all(AppTheme.gutter),
              children: [
                Text(widget.patientName, style: theme.textTheme.titleMedium),
                const SizedBox(height: 4),
                Text(
                  'Saving replaces the whole profile. Anything you clear here is cleared on '
                  'the record.',
                  style: theme.textTheme.bodySmall,
                ),
                const SizedBox(height: 16),
                _Box(
                  controller: _knownConditions,
                  label: 'Known conditions',
                  hint: 'Long-lived things: diabetes, asthma, hypertension.',
                  maxLength: 2000,
                ),
                _Box(
                  controller: _allergies,
                  label: 'Allergies',
                  hint: 'What they must not be given.',
                  maxLength: 1000,
                ),
                _Box(
                  controller: _currentSymptoms,
                  label: 'Current symptoms',
                  hint: 'What they are in with this time.',
                  maxLength: 2000,
                ),
                _Box(
                  controller: _recentSituation,
                  label: 'Recent situation',
                  hint: 'A fall last week, a course of antibiotics finished.',
                  maxLength: 2000,
                ),
                if (profile.updatedAt != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 4, bottom: 12),
                    child: Text(
                      'Last written by '
                      '${profile.updatedByStaffName ?? 'someone no longer on staff'} on '
                      '${FriendlyDate.full(profile.updatedAt!)}.',
                      style: theme.textTheme.bodySmall,
                    ),
                  ),
                FilledButton(
                  onPressed: controller.saving ? null : _submit,
                  child: Text(controller.saving ? 'Saving…' : 'Save'),
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}

class _Box extends StatelessWidget {
  const _Box({
    required this.controller,
    required this.label,
    required this.hint,
    required this.maxLength,
  });

  final TextEditingController controller;
  final String label;
  final String hint;
  final int maxLength;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: TextField(
        controller: controller,
        maxLines: 3,
        maxLength: maxLength,
        textCapitalization: TextCapitalization.sentences,
        decoration: InputDecoration(
          labelText: label,
          helperText: hint,
          helperMaxLines: 2,
          border: const OutlineInputBorder(),
          alignLabelWithHint: true,
        ),
      ),
    );
  }
}
