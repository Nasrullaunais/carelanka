import 'package:flutter/material.dart';

import '../../../core/widgets/async_data.dart';
import '../../../core/widgets/async_view.dart';

class IdleRunView extends StatelessWidget {
  const IdleRunView({super.key, required this.ambulance});

  final AsyncData<String?> ambulance;

  @override
  Widget build(BuildContext context) => switch (ambulance) {
    AsyncReady<String?>(value: final registration?) => EmptyView(
      icon: Icons.local_hospital_outlined,
      title: "You're on $registration",
      message: 'Ready for the next run. It will appear here when it is sent.',
    ),
    AsyncReady<String?>() => const EmptyView(
      icon: Icons.local_hospital_outlined,
      title: 'No ambulance assigned',
      message:
          "You're not on an ambulance crew right now. Ask the duty manager.",
    ),
    _ => const EmptyView(
      icon: Icons.local_hospital_outlined,
      title: 'No run right now',
      message: 'When the duty manager sends you to a call it will appear here.',
    ),
  };
}
