import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../services/api_client/models/gender.dart';
import '../patient_routes.dart';
import '../state/pre_register_controller.dart';

class PreRegisterScreen extends StatefulWidget {
  const PreRegisterScreen({super.key});

  @override
  State<PreRegisterScreen> createState() => _PreRegisterScreenState();
}

class _PreRegisterScreenState extends State<PreRegisterScreen> {
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
    );
    if (picked != null) setState(() => _dateOfBirth = picked);
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;

    final controller = context.read<PreRegisterController>();
    final ok = await controller.submit(
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

    if (ok) {
      context.go(PatientPaths.myStay);
      return;
    }

    final message = controller.error?.message ?? 'Could not save your details.';
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(message)));
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<PreRegisterController>();
    final fieldErrors = controller.fieldErrors;

    return Scaffold(
      appBar: AppBar(title: const Text('Your details')),
      body: SafeArea(
        child: Form(
          key: _formKey,
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Text(
                'We need a few details before the hospital can treat you. '
                'This does not book a visit.',
                style: Theme.of(context).textTheme.bodyMedium,
              ),
              const SizedBox(height: 20),
              _Field(
                controller: _fullName,
                label: 'Full name',
                enabled: !controller.submitting,
                serverErrors: fieldErrors['full_name'],
                validator: (v) => (v == null || v.trim().isEmpty) ? 'Enter your full name' : null,
              ),
              _Field(
                controller: _nic,
                label: 'NIC',
                enabled: !controller.submitting,
                serverErrors: fieldErrors['nic'],
                validator: (v) => (v == null || v.trim().isEmpty) ? 'Enter your NIC' : null,
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<Gender>(
                initialValue: _gender,
                decoration: const InputDecoration(labelText: 'Gender', border: OutlineInputBorder()),
                items: Gender.$valuesDefined
                    .map((g) => DropdownMenuItem(value: g, child: Text(_genderLabel(g))))
                    .toList(),
                onChanged: controller.submitting ? null : (v) => setState(() => _gender = v ?? _gender),
              ),
              const SizedBox(height: 12),
              OutlinedButton.icon(
                onPressed: controller.submitting ? null : _pickDateOfBirth,
                icon: const Icon(Icons.calendar_today_outlined),
                label: Text(_dateOfBirth == null
                    ? 'Date of birth (optional)'
                    : 'Born ${_dateOfBirth!.day}/${_dateOfBirth!.month}/${_dateOfBirth!.year}'),
              ),
              const SizedBox(height: 12),
              _Field(
                controller: _phone,
                label: 'Phone (optional)',
                enabled: !controller.submitting,
                keyboardType: TextInputType.phone,
                serverErrors: fieldErrors['phone'],
              ),
              _Field(
                controller: _address,
                label: 'Address (optional)',
                enabled: !controller.submitting,
                serverErrors: fieldErrors['address'],
              ),
              _Field(
                controller: _emergencyName,
                label: 'Emergency contact name (optional)',
                enabled: !controller.submitting,
                serverErrors: fieldErrors['emergency_contact_name'],
              ),
              _Field(
                controller: _emergencyPhone,
                label: 'Emergency contact phone (optional)',
                enabled: !controller.submitting,
                keyboardType: TextInputType.phone,
                serverErrors: fieldErrors['emergency_contact_phone'],
              ),
              const SizedBox(height: 24),
              FilledButton(
                onPressed: controller.submitting ? null : _submit,
                child: controller.submitting
                    ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2))
                    : const Text('Save my details'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

String _genderLabel(Gender gender) => switch (gender) {
      Gender.male => 'Male',
      Gender.female => 'Female',
      Gender.other => 'Other',
      Gender.unknown => 'Prefer not to say',
      Gender.$unknown => 'Unknown',
    };

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
          errorText: serverErrors?.isNotEmpty == true ? serverErrors!.first : null,
        ),
        validator: validator,
      ),
    );
  }
}
