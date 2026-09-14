import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../services/api_client/models/gender.dart';
import '../state/profile_controller.dart';

/// The details the hospital needs before it can treat you.
///
/// Shown full-screen the first time, because nothing else in the patient area
/// works until the account has a record. Afterwards it is reached from Profile
/// to correct something.
class MyDetailsScreen extends StatefulWidget {
  const MyDetailsScreen({super.key, this.firstTime = false});

  final bool firstTime;

  @override
  State<MyDetailsScreen> createState() => _MyDetailsScreenState();
}

class _MyDetailsScreenState extends State<MyDetailsScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nic = TextEditingController();
  final _fullName = TextEditingController();
  final _phone = TextEditingController();
  final _address = TextEditingController();
  final _emergencyName = TextEditingController();
  final _emergencyPhone = TextEditingController();

  Gender _gender = Gender.unknown;
  DateTime? _dateOfBirth;

  @override
  void initState() {
    super.initState();
    final existing = context.read<ProfileController>().profile.valueOrNull;
    if (existing != null) {
      _nic.text = existing.nic ?? '';
      _fullName.text = existing.fullName;
      _phone.text = existing.phone ?? '';
      _address.text = existing.address ?? '';
      _emergencyName.text = existing.emergencyContactName ?? '';
      _emergencyPhone.text = existing.emergencyContactPhone ?? '';
      _gender = existing.gender;
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

  Future<void> _pickDateOfBirth() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _dateOfBirth ?? DateTime(now.year - 30),
      firstDate: DateTime(now.year - 120),
      lastDate: now,
      helpText: 'Date of birth',
    );
    if (picked != null) setState(() => _dateOfBirth = picked);
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    final controller = context.read<ProfileController>();
    final saved = await controller.save(
      nic: _nic.text.trim(),
      fullName: _fullName.text.trim(),
      gender: _gender,
      dateOfBirth: _dateOfBirth,
      phone: _phone.text,
      address: _address.text,
      emergencyContactName: _emergencyName.text,
      emergencyContactPhone: _emergencyPhone.text,
    );

    if (!mounted) return;

    if (!saved) {
      final message = controller.saveError?.message ?? 'Could not save your details.';
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
      return;
    }

    if (widget.firstTime) return;

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

    return Scaffold(
      appBar: AppBar(
        title: Text(widget.firstTime ? 'Finish setting up' : 'My details'),
        automaticallyImplyLeading: !widget.firstTime,
      ),
      body: SafeArea(
        child: Form(
          key: _formKey,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              if (widget.firstTime)
                Card(
                  color: theme.colorScheme.secondaryContainer,
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Text(
                      'Your account is ready. The hospital needs a few details '
                      'before you can book a visit or follow a stay.',
                      style: theme.textTheme.bodyMedium
                          ?.copyWith(color: theme.colorScheme.onSecondaryContainer),
                    ),
                  ),
                ),
              const SizedBox(height: 8),
              _Field(
                controller: _fullName,
                label: 'Full name',
                enabled: !controller.saving,
                serverErrors: errors['full_name'],
                validator: (v) => (v == null || v.trim().isEmpty) ? 'Enter your full name' : null,
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
                decoration: const InputDecoration(labelText: 'Gender', border: OutlineInputBorder()),
                items: Gender.$valuesDefined
                    .map((g) => DropdownMenuItem(value: g, child: Text(genderLabel(g))))
                    .toList(),
                onChanged:
                    controller.saving ? null : (v) => setState(() => _gender = v ?? _gender),
              ),
              const SizedBox(height: 12),
              OutlinedButton.icon(
                onPressed: controller.saving ? null : _pickDateOfBirth,
                style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
                icon: const Icon(Icons.calendar_today_outlined),
                label: Text(_dateOfBirth == null
                    ? 'Date of birth (optional)'
                    : 'Born ${formatDate(_dateOfBirth!)}'),
              ),
              _Field(
                controller: _phone,
                label: 'Phone (optional)',
                enabled: !controller.saving,
                keyboardType: TextInputType.phone,
                serverErrors: errors['phone'],
              ),
              _Field(
                controller: _address,
                label: 'Address (optional)',
                enabled: !controller.saving,
                serverErrors: errors['address'],
              ),
              const SizedBox(height: 12),
              Text('Emergency contact', style: theme.textTheme.titleSmall),
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
              ),
              const SizedBox(height: 24),
              FilledButton(
                onPressed: controller.saving ? null : _submit,
                style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
                child: controller.saving
                    ? const SizedBox(
                        height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                    : Text(widget.firstTime ? 'Continue' : 'Save changes'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

String genderLabel(Gender gender) => switch (gender) {
      Gender.male => 'Male',
      Gender.female => 'Female',
      Gender.other => 'Other',
      Gender.unknown => 'Prefer not to say',
      Gender.$unknown => 'Unknown',
    };

String formatDate(DateTime value) {
  final local = value.toLocal();
  return '${local.day.toString().padLeft(2, '0')}/'
      '${local.month.toString().padLeft(2, '0')}/${local.year}';
}

String formatDateTime(DateTime value) {
  final local = value.toLocal();
  return '${formatDate(local)} at ${local.hour.toString().padLeft(2, '0')}:'
      '${local.minute.toString().padLeft(2, '0')}';
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
      padding: const EdgeInsets.only(top: 12),
      child: TextFormField(
        controller: controller,
        enabled: enabled,
        keyboardType: keyboardType,
        decoration: InputDecoration(
          labelText: label,
          border: const OutlineInputBorder(),
          errorText: (serverErrors?.isNotEmpty ?? false) ? serverErrors!.first : null,
        ),
        validator: validator,
      ),
    );
  }
}
