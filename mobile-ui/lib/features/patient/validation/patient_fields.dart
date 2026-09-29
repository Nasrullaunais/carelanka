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

const _nicPattern = r'^(\d{9}[VvXx]|\d{12}|[A-Za-z][A-Za-z0-9]{5,14})$';

const _twelveDigitsOnlyFromYear = 2000;
const _maxAgeYears = 120;

String? validateNic(String? value) {
  final text = value?.trim() ?? '';
  if (text.isEmpty) return 'Enter your NIC';

  if (!RegExp(_nicPattern).hasMatch(text)) {
    return 'Nine digits and a V (199534501V), twelve digits (199745600321), '
        'or a passport number starting with a letter.';
  }

  final year = nicBirthYear(text);
  final thisYear = DateTime.now().year;
  if (year != null && (year > thisYear || year < thisYear - _maxAgeYears)) {
    return 'An NIC starts with the birth year, and $year is not a possible one. '
        'Check the NIC.';
  }
  return null;
}

final _oldNicPattern = RegExp(r'^(\d{2})\d{7}[VvXx]$');
final _newNicPattern = RegExp(r'^(\d{4})\d{8}$');

// Mirrors SriLankanNic on the server and nicBirthYear in web-ui/src/types/identifiers.ts. The
// nine-digit form gives the last two digits of a 19xx year and was never issued to anyone born
// from 2000 on; the twelve-digit form gives the full year. A passport number carries no year.
int? nicBirthYear(String nic) {
  final text = nic.trim();

  final oldMatch = _oldNicPattern.firstMatch(text);
  if (oldMatch != null) {
    return 1900 + int.parse(oldMatch.group(1)!);
  }

  final newMatch = _newNicPattern.firstMatch(text);
  if (newMatch != null) {
    return int.parse(newMatch.group(1)!);
  }

  return null;
}

String? validateDateOfBirthAgainstNic(DateTime? dateOfBirth, String nic) {
  if (dateOfBirth == null || nic.trim().isEmpty || validateNic(nic) != null) return null;

  final expected = nicBirthYear(nic);
  if (expected == null) return null;

  if (_oldNicPattern.hasMatch(nic.trim()) && dateOfBirth.year >= _twelveDigitsOnlyFromYear) {
    return 'Someone born in 2000 or later has a twelve-digit NIC, not nine digits '
        'and a V or X. Check the NIC and the date of birth.';
  }

  if (expected == dateOfBirth.year) return null;

  return 'The NIC gives a birth year of $expected, but the date of birth is in '
      '${dateOfBirth.year}. Check both.';
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
