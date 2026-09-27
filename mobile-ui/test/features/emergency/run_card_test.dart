import 'package:carelanka_mobile/features/emergency/widgets/run_card.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_detail.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('shows the reverse-geocoded scene address when available', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: RunCard(
            run: const DispatchDetail(sceneAddressLabel: 'Galle Face, Colombo'),
            busy: false,
            onStep: () {},
            onDecline: () {},
            onNavigate: () {},
          ),
        ),
      ),
    );

    expect(find.text('Scene'), findsOneWidget);
    expect(find.text('Galle Face, Colombo'), findsOneWidget);
  });
}
