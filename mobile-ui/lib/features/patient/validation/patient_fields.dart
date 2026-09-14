import 'package:flutter/material.dart';

/// The shapes the API enforces on a patient record, mirrored here so a typo is
/// caught before it costs a round trip.
///
/// `PatientIdentifierFormats` on the server is the original. The web app
/// carries the same pair in `types/identifiers.ts`, and all three have to agree
/// or one of them rejects what the others accept.
abstract final class PatientFieldLimits {
  static const fullName = 200;
  static const nic = 20;
  static const phone = 20;
  static const address = 300;
  static const contactName = 200;
  static const visitReason = 300;
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

/// Optional here, unlike the desk form in the web app. The server takes the
/// record without one and reports it under `missing_fields` instead, so a
/// patient who cannot type an address still gets an account.
String? validateAddress(String? value) =>
    _withinLimit(value, PatientFieldLimits.address);

String? validateContactName(String? value) =>
    _withinLimit(value, PatientFieldLimits.contactName);

String? validateVisitReason(String? value) =>
    _withinLimit(value, PatientFieldLimits.visitReason);

String? _withinLimit(String? value, int limit) =>
    (value != null && value.length > limit) ? 'Use $limit characters or fewer' : null;

/// A character count that stays out of the way until the limit is close, the
/// way the web app's `Counter` does. Material's own counter is always on, which
/// puts a number under every field on the form whether or not it is near full.
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
