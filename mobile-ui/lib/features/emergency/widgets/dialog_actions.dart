import 'package:flutter/material.dart';

/// The app theme makes buttons full width, which breaks AlertDialog's side-by-side
/// actions row, so emergency dialogs stack the main action above the way back.
List<Widget> stackedDialogActions({
  required Widget confirm,
  required Widget back,
}) => [
  Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    mainAxisSize: MainAxisSize.min,
    children: [confirm, const SizedBox(height: 8), back],
  ),
];
