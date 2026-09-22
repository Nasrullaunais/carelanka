import 'package:flutter/material.dart';

// Mirrors PatientIdentifierFormats on the server and types/identifiers.ts in the web app — all three must agree.
abstract final class PatientFieldLimits {
  static const fullName = 200;
  static const nic = 20;
  static const phone = 20;
  static const address = 300;
  static const contactName = 200;
  static const visitReason = 300;
  static const careReportMin = 5;
  static const careReportMax = 2000;
}

const _nicPattern = r'^(\d{9}[VvXx]|\d{12}|(?=.*[A-Za-z])[A-Za-z0-9]{6,15})$';

String? validateNic(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) return 'Enter your NIC';

  if (!RegExp(_nicPattern).hasMatch(text)) {
    return 'Nine digits and a V (199534501V), twelve digits (199745600321), '
        'or a passport number.';
  }
  return null;
}

String? validateFullName(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) return 'Enter your full name';

  return _withinLimit(value, PatientFieldLimits.fullName);
}

String? validateAddress(String? value) =>
    _withinLimit(value, PatientFieldLimits.address);

String? validateContactName(String? value) =>
    _withinLimit(value, PatientFieldLimits.contactName);

String? validateVisitReason(String? value) =>
    _withinLimit(value, PatientFieldLimits.visitReason);

String? validateCareReport(String? value) {
  final text = value?.trim() ?? '';
  if (text.length < PatientFieldLimits.careReportMin) {
    return 'Say a bit more — at least ${PatientFieldLimits.careReportMin} characters.';
  }
  return _withinLimit(value, PatientFieldLimits.careReportMax);
}

String? _withinLimit(String? value, int limit) =>
    (value != null && value.length > limit) ? 'Use $limit characters or fewer' : null;

Widget? Function(BuildContext, {required int currentLength, required bool isFocused, required int? maxLength})
    nearLimitCounter() {
  return (context, {required currentLength, required isFocused, required maxLength}) {
    if (maxLength == null || currentLength < maxLength * 0.75) return null;

    final left = maxLength - currentLength;
    final scheme = Theme.of(context).colorScheme;

    return Text(
      left == 0 ? 'Limit reached - $maxLength characters.' : '$left characters left.',
      style: Theme.of(context).textTheme.bodySmall?.copyWith(
            color: left == 0 ? scheme.error : scheme.onSurfaceVariant,
          ),
    );
  };
}
