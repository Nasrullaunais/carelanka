import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../core/auth/auth_form.dart';
import '../../../core/theme/app_theme.dart';
import '../../../core/utils/friendly_date.dart';
import '../../../services/api_client/models/gender.dart';
import '../state/profile_controller.dart';
import '../widgets/panels.dart';

/// The details the hospital needs before it can treat you.
///
/// Always pushed on top of something, so it always has a way back out. Whether
/// it reads as first-time setup or as a correction comes from whether the
/// account has a record yet, not from a flag a caller could get wrong.
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

  /// Null until picked. The form only offers male and female, so a record
  /// carrying anything else starts blank rather than showing a value the
  /// dropdown cannot display.
  Gender? _gender;
  DateTime? _dateOfBirth;

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
      _gender = _offeredGenders.contains(existing.gender) ? existing.gender : null;
      _dateOfBirth = existing.dateOfBirth;
    }
  }

  @override
  void dispose() {
    for (final controller in [_nic, _fullName, _phone, _address, _emergencyName, _emergencyPhone]) {
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
      // Field errors are already rendered under the fields they belong to.
      // Toasting "One or more fields are not valid" on top of them names
      // nothing and hides the field that does.
      if (controller.fieldErrors.isEmpty) {
        final message = controller.saveError?.message ?? 'Could not save your details.';
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
      }
      return;
    }

    Navigator.of(context).pop();
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Your details are saved.')),
    );
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<ProfileController>();
    final errors = controller.fieldErrors;
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Scaffold(
      appBar: AppBar(title: Text(_firstTime ? 'Finish setting up' : 'My details')),
      body: SafeArea(
        child: Form(
          key: _formKey,
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
                  icon: Icons.waving_hand_outlined,
                  accent: scheme.primary,
                  title: 'Your account is ready',
                  body: 'The hospital needs a few details before you can book a '
                      'visit or follow a stay.',
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
                      serverErrors: errors['full_name'],
                      validator: (v) =>
                          (v == null || v.trim().isEmpty) ? 'Enter your full name' : null,
                    ),
                    _Field(
                      controller: _nic,
                      label: 'NIC',
                      enabled: !controller.saving,
                      serverErrors: errors['nic'],
                      validator: (v) => (v == null || v.trim().isEmpty) ? 'Enter your NIC' : null,
                    ),
                    const SizedBox(height: 12),
                    DropdownButtonFormField<Gender>(
                      initialValue: _gender,
                      decoration: InputDecoration(
                        labelText: 'Gender',
                        errorText: _firstError(errors['gender']),
                      ),
                      items: _offeredGenders
                          .map((g) => DropdownMenuItem(value: g, child: Text(genderLabel(g))))
                          .toList(),
                      onChanged: controller.saving ? null : (v) => setState(() => _gender = v),
                      validator: (v) => v == null ? 'Choose your gender' : null,
                    ),
                    const SizedBox(height: 12),
                    _DateOfBirthField(
                      value: _dateOfBirth,
                      enabled: !controller.saving,
                      serverError: _firstError(errors['date_of_birth']),
                      pick: _pickDateOfBirth,
                      onChanged: (picked) => setState(() => _dateOfBirth = picked),
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
                      serverErrors: errors['phone'],
                      validator: validatePhoneNumber,
                    ),
                    _Field(
                      controller: _address,
                      label: 'Address (optional)',
                      enabled: !controller.saving,
                      serverErrors: errors['address'],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              SectionCard(
                title: 'Emergency contact',
                icon: Icons.emergency_outlined,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Who the ward should call if something happens while you '
                      'are here.',
                      style: theme.textTheme.bodySmall
                          ?.copyWith(color: scheme.onSurfaceVariant),
                    ),
                    const SizedBox(height: 12),
                    _Field(
                      controller: _emergencyName,
                      label: 'Name (optional)',
                      enabled: !controller.saving,
                      serverErrors: errors['emergency_contact_name'],
                    ),
                    _Field(
                      controller: _emergencyPhone,
                      label: 'Phone (optional)',
                      enabled: !controller.saving,
                      keyboardType: TextInputType.phone,
                      serverErrors: errors['emergency_contact_phone'],
                      // Optional, but a number in the wrong shape is worse than
                      // none — nobody finds out until the ward has to call it.
                      validator: (v) =>
                          (v == null || v.trim().isEmpty) ? null : validatePhoneNumber(v),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 28),
              FilledButton(
                onPressed: controller.saving ? null : _submit,
                child: controller.saving
                    ? const SizedBox(
                        height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                    : Text(_firstTime ? 'Save my details' : 'Save changes'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The server's own validation message, shown against the field it belongs to
/// rather than in a toast the reader has to map back.
String? _firstError(List<String>? serverErrors) =>
    (serverErrors == null || serverErrors.isEmpty) ? null : serverErrors.first;

/// What the patient form offers. The wire enum also carries `other` and
/// `unknown` for records created by staff, so [genderLabel] still handles them.
const _offeredGenders = [Gender.male, Gender.female];

String genderLabel(Gender gender) => switch (gender) {
      Gender.male => 'Male',
      Gender.female => 'Female',
      Gender.other => 'Other',
      Gender.unknown => 'Prefer not to say',
      Gender.$unknown => 'Unknown',
    };

/// `emergency_contact_phone` → `Emergency contact phone`.
///
/// `missing_fields` arrives as wire names, because the server is naming its own
/// columns. Showing them raw makes the app look like a database browser.
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
  final parts = name.trim().split(RegExp(r'\s+')).where((p) => p.isNotEmpty).toList();
  if (parts.isEmpty) return '?';
  if (parts.length == 1) return parts.first.characters.first.toUpperCase();
  return (parts.first.characters.first + parts.last.characters.first).toUpperCase();
}

/// The form reads and writes the same controller the calling screen watches, so
/// it has to be carried across into the pushed route.
void openMyDetails(BuildContext context, ProfileController controller) {
  Navigator.of(context).push(MaterialPageRoute(
    builder: (_) => ChangeNotifierProvider<ProfileController>.value(
      value: controller,
      child: const MyDetailsScreen(),
    ),
  ));
}

/// A date picker that behaves like the text fields around it: same box, same
/// floating label, and its "required" message lands under the box rather than
/// in a snack bar, so `Form.validate()` covers it like any other field.
class _DateOfBirthField extends StatelessWidget {
  const _DateOfBirthField({
    required this.value,
    required this.enabled,
    required this.pick,
    required this.onChanged,
    this.serverError,
  });

  final DateTime? value;
  final bool enabled;
  final Future<DateTime?> Function() pick;
  final ValueChanged<DateTime> onChanged;
  final String? serverError;

  @override
  Widget build(BuildContext context) {
    return FormField<DateTime>(
      initialValue: value,
      validator: (v) => v == null ? 'Enter your date of birth' : null,
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
          child: field.value == null ? null : Text(FriendlyDate.date(field.value!)),
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
    this.serverErrors,
  });

  final TextEditingController controller;
  final String label;
  final bool enabled;
  final String? Function(String?)? validator;
  final TextInputType? keyboardType;
  final List<String>? serverErrors;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: TextFormField(
        controller: controller,
        enabled: enabled,
        keyboardType: keyboardType,
        decoration: InputDecoration(
          labelText: label,
          errorText: _firstError(serverErrors),
        ),
        validator: validator,
      ),
    );
  }
}
