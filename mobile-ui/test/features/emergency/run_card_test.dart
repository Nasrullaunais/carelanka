import 'package:carelanka_mobile/features/emergency/widgets/run_card.dart';
import 'package:carelanka_mobile/services/api_client/models/call_priority.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_detail.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_status.dart';
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

  testWidgets('shows what the emergency is and lets the crew ring the caller', (
    tester,
  ) async {
    String? rang;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: RunCard(
            run: const DispatchDetail(
              status: DispatchStatus.enRouteToScene,
              callPriority: CallPriority.critical,
              callDetails: 'Chest pain, conscious',
              patientName: 'Nimal Perera',
              callerPhone: '0771234567',
              destinationLabel: 'CareLanka General, emergency entrance',
            ),
            busy: false,
            onStep: () {},
            onDecline: () {},
            onNavigate: () {},
            onEndAtScene: () {},
            onCallCaller: (phone) => rang = phone,
          ),
        ),
      ),
    );

    expect(find.text('Critical priority'), findsOneWidget);
    expect(find.text('Chest pain, conscious'), findsOneWidget);
    expect(find.text('Nimal Perera'), findsOneWidget);
    expect(find.text('CareLanka General, emergency entrance'), findsOneWidget);
    expect(find.text('End without going to hospital'), findsNothing);
    await tester.tap(find.textContaining('Call the caller'));
    expect(rang, '0771234567');
  });

  testWidgets('offers to end without transport only at the scene', (
    tester,
  ) async {
    var ended = false;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: RunCard(
            run: const DispatchDetail(status: DispatchStatus.atScene),
            busy: false,
            onStep: () {},
            onDecline: () {},
            onNavigate: () {},
            onEndAtScene: () => ended = true,
          ),
        ),
      ),
    );

    await tester.tap(find.text('End without going to hospital'));
    expect(ended, isTrue);
  });
}
