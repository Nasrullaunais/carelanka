import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/auth/auth_form.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../core/widgets/phone_width.dart';
import '../../../services/api_client/models/gender.dart';
import '../state/profile_controller.dart';
import '../validation/patient_fields.dart';
import '../widgets/panels.dart';

class MyDetailsScreen extends StatefulWidget {
  const MyDetailsScreen({super.key});

  @override
  State<MyDetailsScreen> createState() => _MyDetailsScreenState();
}

class _MyDetailsScreenState extends State<MyDetailsScreen> {
  final _formKey = GlobalKey<FormState>();
  late final bool _firstTime;
  final _nic = TextEditingController();
  final _fullName = TextEditingController();
  final _phone = TextEditingController();
  final _address = TextEditingController();
  final _emergencyName = TextEditingController();
  final _emergencyPhone = TextEditingController();

  Gender? _gender;
  DateTime? _dateOfBirth;

  // Off until the first submit, then on: errors appear when the form is sent, and clear as each
  // field is fixed rather than sitting there red until the next submit.
  bool _submitted = false;

  @override
  void initState() {
    super.initState();
    final existing = context.read<ProfileController>().profile.valueOrNull;
    _firstTime = existing == null;
    if (existing != null) {
      _nic.text = existing.nic ?? '';
      _fullName.text = existing.fullName;
      _phone.text = existing.phone ?? '';
      _address.text = existing.address ?? '';
      _emergencyName.text = existing.emergencyContactName ?? '';
      _emergencyPhone.text = existing.emergencyContactPhone ?? '';
      _gender = _offeredGenders.contains(existing.gender)
          ? existing.gender
          : null;
      _dateOfBirth = existing.dateOfBirth;
    }
  }

  @override
  void dispose() {
    for (final controller in [
      _nic,
      _fullName,
      _phone,
      _address,
      _emergencyName,
      _emergencyPhone,
    ]) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<DateTime?> _pickDateOfBirth() {
    final now = DateTime.now();
    return showDatePicker(
      context: context,
      initialDate: _dateOfBirth ?? DateTime(now.year - 30),
      firstDate: DateTime(now.year - 120),
      lastDate: now,
      helpText: 'Date of birth',
    );
  }

  Future<void> _submit() async {
    setState(() => _submitted = true);

    if (!_formKey.currentState!.validate()) return;

    final controller = context.read<ProfileController>();
    final saved = await controller.save(
      nic: _nic.text.trim(),
      fullName: _fullName.text.trim(),
      gender: _gender!,
      dateOfBirth: _dateOfBirth,
      phone: _phone.text,
      address: _address.text,
      emergencyContactName: _emergencyName.text,
      emergencyContactPhone: _emergencyPhone.text,
    );

    if (!mounted) return;

    if (!saved) {
      // Field errors already render under their fields — only toast when there's no field to blame.
      if (controller.fieldErrors.isEmpty) {
        final message =
            controller.saveError?.message ?? 'Could not save your details.';
        ScaffoldMessenger.of(
          context,
        ).showSnackBar(SnackBar(content: Text(message)));
      }
      return;
    }

    Navigator.of(context).pop();
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Your details have been saved.')),
    );
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<ProfileController>();
    final errors = controller.fieldErrors;
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return PhoneWidth(
      child: Scaffold(
        appBar: AppBar(
          title: Text(_firstTime ? 'Add my details' : 'My details'),
        ),
        body: SafeArea(
          child: Form(
            key: _formKey,
            autovalidateMode: _submitted
                ? AutovalidateMode.onUserInteraction
                : AutovalidateMode.disabled,
            child: ListView(
              padding: const EdgeInsets.fromLTRB(
                AppTheme.gutter,
                8,
                AppTheme.gutter,
                32,
              ),
              children: [
                if (_firstTime) ...[
                  NoticeBanner(
                    icon: Icons.assignment_ind_outlined,
                    accent: scheme.primary,
                    title: 'Complete your registration',
                    body:
                        'These details are required before you can book a visit.',
                  ),
                  const SizedBox(height: 20),
                ],
                SectionCard(
                  title: 'About you',
                  icon: Icons.person_outline,
                  child: Column(
                    children: [
                      _Field(
                        controller: _fullName,
                        label: 'Full name',
                        enabled: !controller.saving,
                        maxLength: PatientFieldLimits.fullName,
                        serverErrors: errors['full_name'],
                        validator: validateFullName,
                      ),
                      _Field(
                        controller: _nic,
                        label: 'NIC',
                        enabled: !controller.saving,
                        maxLength: PatientFieldLimits.nic,
                        serverErrors: errors['nic'],
                        validator: validateNic,
                        // Re-validates the date of birth field against the new NIC as it's typed,
                        // once the form has been submitted once.
                        onChanged: _submitted ? (_) => setState(() {}) : null,
                      ),
                      const SizedBox(height: 12),
                      DropdownButtonFormField<Gender>(
                        initialValue: _gender,
                        decoration: InputDecoration(
                          labelText: 'Gender',
                          errorText: _firstError(errors['gender']),
                        ),
                        items: _offeredGenders
                            .map(
                              (g) => DropdownMenuItem(
                                value: g,
                                child: Text(genderLabel(g)),
                              ),
                            )
                            .toList(),
                        onChanged: controller.saving
                            ? null
                            : (v) => setState(() => _gender = v),
                        validator: (v) =>
                            v == null ? 'Choose your gender' : null,
                      ),
                      const SizedBox(height: 12),
                      _DateOfBirthField(
                        value: _dateOfBirth,
                        nic: _nic.text,
                        enabled: !controller.saving,
                        serverError: _firstError(errors['date_of_birth']),
                        pick: _pickDateOfBirth,
                        onChanged: (picked) =>
                            setState(() => _dateOfBirth = picked),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 16),
                SectionCard(
                  title: 'How to reach you',
                  icon: Icons.contact_page_outlined,
                  child: Column(
                    children: [
                      _Field(
                        controller: _phone,
                        label: 'Phone',
                        enabled: !controller.saving,
                        keyboardType: TextInputType.phone,
                        maxLength: PatientFieldLimits.phone,
                        serverErrors: errors['phone'],
                        validator: validatePhoneNumber,
                      ),
                      _Field(
                        controller: _address,
                        label: 'Address (optional)',
                        enabled: !controller.saving,
                        maxLength: PatientFieldLimits.address,
                        serverErrors: errors['address'],
                        validator: validateAddress,
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 16),
                SectionCard(
                  title: 'Emergency/guardian contact',
                  icon: Icons.emergency_outlined,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Who the hospital should contact in an emergency.',
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: scheme.onSurfaceVariant,
                        ),
                      ),
                      const SizedBox(height: 12),
                      _Field(
                        controller: _emergencyName,
                        label: 'Name (optional)',
                        enabled: !controller.saving,
                        maxLength: PatientFieldLimits.contactName,
                        serverErrors: errors['emergency_contact_name'],
                        validator: validateContactName,
                      ),
                      _Field(
                        controller: _emergencyPhone,
                        label: 'Phone (optional)',
                        enabled: !controller.saving,
                        keyboardType: TextInputType.phone,
                        maxLength: PatientFieldLimits.phone,
                        serverErrors: errors['emergency_contact_phone'],
                        validator: (v) => (v == null || v.trim().isEmpty)
                            ? null
                            : validatePhoneNumber(v),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 28),
                FilledButton(
                  onPressed: controller.saving ? null : _submit,
                  child: controller.saving
                      ? const SizedBox(
                          height: 20,
                          width: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : Text(_firstTime ? 'Save my details' : 'Save changes'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

String? _firstError(List<String>? serverErrors) =>
    (serverErrors == null || serverErrors.isEmpty) ? null : serverErrors.first;

// The wire enum also carries `other`/`unknown` for records staff created — genderLabel below still has to handle them.
const _offeredGenders = [Gender.male, Gender.female];

String genderLabel(Gender gender) => switch (gender) {
  Gender.male => 'Male',
  Gender.female => 'Female',
  Gender.other => 'Other',
  Gender.unknown => 'Prefer not to say',
  Gender.$unknown => 'Unknown',
};

String prettyFieldName(String wireName) {
  final words = wireName.split('_').where((w) => w.isNotEmpty).toList();
  if (words.isEmpty) return wireName;
  final first = words.first;
  return [
    first[0].toUpperCase() + first.substring(1),
    ...words.skip(1),
  ].join(' ');
}

String initialsOf(String name) {
  final parts = name
      .trim()
      .split(RegExp(r'\s+'))
      .where((p) => p.isNotEmpty)
      .toList();
  if (parts.isEmpty) return '?';
  if (parts.length == 1) return parts.first.characters.first.toUpperCase();
  return (parts.first.characters.first + parts.last.characters.first)
      .toUpperCase();
}

void openMyDetails(BuildContext context, ProfileController controller) {
  Navigator.of(context).push(
    MaterialPageRoute(
      builder: (_) => ChangeNotifierProvider<ProfileController>.value(
        value: controller,
        child: const MyDetailsScreen(),
      ),
    ),
  );
}

class _DateOfBirthField extends StatelessWidget {
  const _DateOfBirthField({
    required this.value,
    required this.nic,
    required this.enabled,
    required this.pick,
    required this.onChanged,
    this.serverError,
  });

  final DateTime? value;
  final String nic;
  final bool enabled;
  final Future<DateTime?> Function() pick;
  final ValueChanged<DateTime> onChanged;
  final String? serverError;

  @override
  Widget build(BuildContext context) {
    return FormField<DateTime>(
      initialValue: value,
      validator: (v) =>
          v == null ? 'Enter your date of birth' : validateDateOfBirthAgainstNic(v, nic),
      builder: (field) => InkWell(
        onTap: enabled
            ? () async {
                final picked = await pick();
                if (picked == null) return;
                field.didChange(picked);
                onChanged(picked);
              }
            : null,
        borderRadius: BorderRadius.circular(AppTheme.radiusM),
        child: InputDecorator(
          decoration: InputDecoration(
            labelText: 'Date of birth',
            errorText: field.errorText ?? serverError,
            prefixIcon: const Icon(Icons.cake_outlined),
          ),
          isEmpty: field.value == null,
          child: field.value == null
              ? null
              : Text(FriendlyDate.date(field.value!)),
        ),
      ),
    );
  }
}

class _Field extends StatelessWidget {
  const _Field({
    required this.controller,
    required this.label,
    required this.enabled,
    this.validator,
    this.keyboardType,
    this.maxLength,
    this.serverErrors,
    this.onChanged,
  });

  final TextEditingController controller;
  final String label;
  final bool enabled;
  final String? Function(String?)? validator;
  final TextInputType? keyboardType;
  final int? maxLength;
  final List<String>? serverErrors;
  final ValueChanged<String>? onChanged;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: TextFormField(
        controller: controller,
        enabled: enabled,
        keyboardType: keyboardType,
        maxLength: maxLength,
        buildCounter: nearLimitCounter(),
        decoration: InputDecoration(
          labelText: label,
          errorText: _firstError(serverErrors),
        ),
        validator: validator,
        onChanged: onChanged,
      ),
    );
  }
}
