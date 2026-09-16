import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../../../services/api_client/models/patient_claim_preview.dart';
import '../hospital_contact.dart';
import '../state/profile_controller.dart';
import '../validation/patient_fields.dart';
import '../widgets/dialer.dart';
import '../widgets/panels.dart';

void openClaimRecord(BuildContext context, ProfileController controller) {
  Navigator.of(context).push(MaterialPageRoute(
    builder: (_) => ChangeNotifierProvider<ProfileController>.value(
      value: controller,
      child: const ClaimRecordScreen(),
    ),
  ));
}

/// For the patient the hospital registered at the desk - a walk-in, an emergency arrival -
/// who only installed the app afterwards. Their stay already exists on a record staff
/// created; this joins their login to it instead of creating a second record.
class ClaimRecordScreen extends StatefulWidget {
  const ClaimRecordScreen({super.key});

  @override
  State<ClaimRecordScreen> createState() => _ClaimRecordScreenState();
}

class _ClaimRecordScreenState extends State<ClaimRecordScreen> {
  final _formKey = GlobalKey<FormState>();
  final _code = TextEditingController();
  final _nic = TextEditingController();

  bool _submitted = false;

  PatientClaimPreview? _preview;

  @override
  void dispose() {
    _code.dispose();
    _nic.dispose();
    super.dispose();
  }

  Future<void> _find() async {
    setState(() => _submitted = true);

    if (!_formKey.currentState!.validate()) return;

    final preview = await context.read<ProfileController>().previewClaim(
          patientCode: _code.text,
          nic: _nic.text,
        );

    if (!mounted) return;

    if (preview == null) {
      _showError('That patient code and NIC do not match. Call the desk.');
      return;
    }

    setState(() => _preview = preview);
  }

  Future<void> _confirm() async {
    final claimed = await context.read<ProfileController>().claim(
          patientCode: _code.text,
          nic: _nic.text,
        );

    if (!mounted) return;

    if (!claimed) {
      setState(() => _preview = null);
      _showError('That patient code and NIC do not match. Call the desk.');
      return;
    }

    Navigator.of(context).pop();
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Your hospital record is now linked.')),
    );
  }

  void _showError(String fallback) {
    final message = context.read<ProfileController>().saveError?.message ?? fallback;
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message), duration: const Duration(seconds: 5)));
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<ProfileController>();
    final preview = _preview;

    return Scaffold(
      appBar: AppBar(title: const Text('I have a patient code')),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(AppTheme.gutter, 12, AppTheme.gutter, 32),
          children: preview == null
              ? _form(controller)
              : _confirmation(preview, controller),
        ),
      ),
    );
  }

  List<Widget> _form(ProfileController controller) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return [
      Text(
        'If the hospital registered you at the desk, your record already exists. '
        'Enter the patient code from the slip they gave you and we will find it.',
        style: theme.textTheme.bodyMedium?.copyWith(color: scheme.onSurfaceVariant),
      ),
      const SizedBox(height: 20),
      Form(
        key: _formKey,
        autovalidateMode:
            _submitted ? AutovalidateMode.onUserInteraction : AutovalidateMode.disabled,
        child: Column(
          children: [
            TextFormField(
              controller: _code,
              enabled: !controller.saving,
              autocorrect: false,
              textCapitalization: TextCapitalization.characters,
              inputFormatters: [
                FilteringTextInputFormatter.allow(RegExp('[A-Za-z0-9]')),
                LengthLimitingTextInputFormatter(8),
                _UpperCaseFormatter(),
              ],
              decoration: const InputDecoration(
                labelText: 'Patient code',
                hintText: 'PK4M9XB2',
              ),
              validator: (value) {
                final code = (value ?? '').trim();
                if (code.isEmpty) return 'Enter the patient code from your slip.';
                if (!_codeFormat.hasMatch(code)) {
                  return 'A patient code is a P followed by seven characters.';
                }
                return null;
              },
            ),
            const SizedBox(height: 14),
            TextFormField(
              controller: _nic,
              enabled: !controller.saving,
              autocorrect: false,
              textCapitalization: TextCapitalization.characters,
              maxLength: PatientFieldLimits.nic,
              decoration: const InputDecoration(
                labelText: 'NIC',
                hintText: '199534501V',
                counterText: '',
              ),
              validator: validateNic,
            ),
          ],
        ),
      ),
      const SizedBox(height: 10),
      Text(
        'We ask for both so that a lost slip on its own is not enough to open your record.',
        style: theme.textTheme.bodySmall?.copyWith(color: scheme.onSurfaceVariant),
      ),
      const SizedBox(height: 22),
      FilledButton(
        onPressed: controller.saving ? null : _find,
        style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
        child: controller.saving
            ? const SizedBox(
                height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
            : const Text('Find my record'),
      ),
      const SizedBox(height: 24),
      const _DeskHelp(
        title: 'No slip, or the code will not work?',
        body: 'Some records are created without a code you can use. '
            'The desk can link your account for you.',
      ),
    ];
  }

  List<Widget> _confirmation(PatientClaimPreview preview, ProfileController controller) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return [
      Text('Is this you?', style: theme.textTheme.titleLarge),
      const SizedBox(height: 8),
      Text(
        'We have hidden most of the details on purpose. You should still recognise them.',
        style: theme.textTheme.bodyMedium?.copyWith(color: scheme.onSurfaceVariant),
      ),
      const SizedBox(height: 18),
      SectionCard(
        child: Column(
          children: [
            DetailRow(
              label: 'Name',
              value: preview.maskedFullName,
              icon: Icons.person_outline,
            ),
            DetailRow(
              label: 'Phone',
              value: preview.maskedPhone,
              icon: Icons.phone_outlined,
            ),
            DetailRow(
              label: 'Patient code',
              value: preview.patientCode,
              icon: Icons.badge_outlined,
            ),
          ],
        ),
      ),
      const SizedBox(height: 22),
      FilledButton(
        onPressed: controller.saving ? null : _confirm,
        style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
        child: controller.saving
            ? const SizedBox(
                height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
            : const Text('Yes, this is me'),
      ),
      const SizedBox(height: 10),
      OutlinedButton(
        onPressed: controller.saving ? null : () => setState(() => _preview = null),
        style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
        child: const Text('No, go back'),
      ),
      const SizedBox(height: 24),
      const _DeskHelp(
        title: 'Not you?',
        body: 'Do not continue. Call the hospital and we will sort it out.',
      ),
    ];
  }

  static final _codeFormat = RegExp(r'^[Pp][0-9A-Za-z]{7}$');
}

class _DeskHelp extends StatelessWidget {
  const _DeskHelp({required this.title, required this.body});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return NoticeBanner(
      icon: Icons.support_agent_outlined,
      accent: scheme.warning,
      title: title,
      body: body,
      action: OutlinedButton.icon(
        onPressed: () => callNumber(context, HospitalContact.reception),
        icon: const Icon(Icons.call_outlined, size: 18),
        label: const Text(HospitalContact.reception),
        style: OutlinedButton.styleFrom(minimumSize: const Size(0, 42)),
      ),
    );
  }
}

class _UpperCaseFormatter extends TextInputFormatter {
  @override
  TextEditingValue formatEditUpdate(TextEditingValue _, TextEditingValue next) =>
      next.copyWith(text: next.text.toUpperCase());
}
