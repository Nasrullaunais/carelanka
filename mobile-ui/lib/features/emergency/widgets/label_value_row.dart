import 'package:flutter/material.dart';

class LabelValueRow extends StatelessWidget {
  const LabelValueRow({super.key, required this.label, required this.value});

  final String label;
  final String? value;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 4),
    child: Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label),
        const SizedBox(width: 16),
        Flexible(child: Text(value ?? 'Not set', textAlign: TextAlign.end)),
      ],
    ),
  );
}
