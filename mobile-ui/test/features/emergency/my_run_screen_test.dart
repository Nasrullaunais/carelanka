import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/features/emergency/screens/my_run_screen.dart';
import 'package:carelanka_mobile/features/emergency/services/crew_location_reporter.dart';
import 'package:carelanka_mobile/features/emergency/state/my_run_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_detail.dart';
import 'package:carelanka_mobile/services/api_client/models/dispatch_status.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import '../../fake_inbox.dart';
import 'crew_location_reporter_test.dart';
import 'my_run_controller_test.dart';

void main() {
  late FakeRunService service;
  late List<String> opened;

  setUp(() {
    service = FakeRunService();
    opened = [];
  });

  Future<void> openScreen(WidgetTester tester, DispatchStatus status) async {
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
      const MethodChannel('plugins.flutter.io/url_launcher'),
      (call) async {
        if (call.method == 'launch') {
          opened.add((call.arguments as Map)['url'] as String);
        }
        return true;
      },
    );
    addTearDown(
      () => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(
        const MethodChannel('plugins.flutter.io/url_launcher'),
        null,
      ),
    );
    service.active = DispatchDetail(id: 'run-1', status: status);
    await tester.pumpWidget(
      MultiProvider(
        providers: [
          fakeInboxProvider(),
          ChangeNotifierProvider(
            create: (_) => CrewLocationReporter(
              dispatches: FakeDispatchGateway('ambulance-1'),
              location: FakeLocationGateway(),
            ),
          ),
          ChangeNotifierProvider(create: (_) => MyRunController(service)),
        ],
        child: const MaterialApp(home: MyRunScreen()),
      ),
    );
    await tester.pumpAndSettle();
    addTearDown(() => tester.pumpWidget(const SizedBox()));
  }

  group('with no run', () {
    Future<void> openIdle(WidgetTester tester) async {
      await openScreen(tester, DispatchStatus.assigned);
      service.active = null;
      await tester.pump(const Duration(seconds: 10));
      await tester.pumpAndSettle();
    }

    testWidgets('names the ambulance the crew is on', (tester) async {
      service.ambulance = 'AMB-3';
      await openIdle(tester);

      expect(find.text("You're on AMB-3"), findsOneWidget);
      expect(find.textContaining('Ready for the next run'), findsOneWidget);
    });

    testWidgets('tells a crew member with no ambulance to ask the desk', (
      tester,
    ) async {
      await openIdle(tester);

      expect(
        find.text(
          "You're not on an ambulance crew right now. Ask the duty manager.",
        ),
        findsOneWidget,
      );
    });

    testWidgets('does not claim "not assigned" when the lookup failed', (
      tester,
    ) async {
      service.ambulanceError = const ApiException(message: 'offline');
      await openIdle(tester);

      expect(find.text('No run right now'), findsOneWidget);
      expect(find.textContaining('not on an ambulance crew'), findsNothing);
    });
  });

  group('Google Maps after a step', () {
    testWidgets('opens to the scene once driving has started', (tester) async {
      await openScreen(tester, DispatchStatus.acknowledged);

      await tester.tap(find.text('Start driving to the scene'));
      await tester.pumpAndSettle();

      expect(service.calls, ['advance:en_route_to_scene']);
      expect(opened, ['https://maps']);
    });

    testWidgets('opens to the hospital once the patient is on board', (
      tester,
    ) async {
      await openScreen(tester, DispatchStatus.atScene);

      await tester.tap(find.text('Patient on board, going to hospital'));
      await tester.pump();
      expect(opened, isEmpty);
      await tester.tap(find.text('Tap again to confirm'));
      await tester.pumpAndSettle();

      expect(service.calls, ['advance:transporting_to_hospital']);
      expect(opened, ['https://maps']);
    });

    testWidgets('stays closed after the other steps', (tester) async {
      await openScreen(tester, DispatchStatus.assigned);
      await tester.tap(find.text('Accept this run'));
      await tester.pumpAndSettle();
      expect(service.calls, ['acknowledge']);

      service.active = const DispatchDetail(
        id: 'run-1',
        status: DispatchStatus.enRouteToScene,
      );
      await tester.pump(const Duration(seconds: 10));
      await tester.pumpAndSettle();
      await tester.tap(find.text('I have reached the scene'));
      await tester.pump();
      await tester.tap(find.text('Tap again to confirm'));
      await tester.pumpAndSettle();

      expect(service.calls.last, 'advance:at_scene');
      expect(opened, isEmpty);
    });

    testWidgets('does not open when the step was not saved', (tester) async {
      await openScreen(tester, DispatchStatus.acknowledged);
      service.nextError = const ApiException(message: 'offline');

      await tester.tap(find.text('Start driving to the scene'));
      await tester.pumpAndSettle();

      expect(opened, isEmpty);
    });
  });

  group('with no signal', () {
    testWidgets('says the step was not saved and offers Try again', (
      tester,
    ) async {
      await openScreen(tester, DispatchStatus.acknowledged);
      service.nextError = const ApiException(message: 'offline');

      await tester.tap(find.text('Start driving to the scene'));
      await tester.pumpAndSettle();

      expect(
        find.text('No connection — this step was not saved.'),
        findsOneWidget,
      );
      expect(find.text('Try again'), findsOneWidget);
      expect(find.text('Dismiss'), findsOneWidget);
    });

    testWidgets('Try again repeats the step and then opens Maps', (
      tester,
    ) async {
      await openScreen(tester, DispatchStatus.acknowledged);
      service.nextError = const ApiException(message: 'offline');
      await tester.tap(find.text('Start driving to the scene'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Try again'));
      await tester.pumpAndSettle();

      expect(service.calls, [
        'advance:en_route_to_scene',
        'advance:en_route_to_scene',
      ]);
      expect(find.text('Try again'), findsNothing);
      expect(find.textContaining('No connection'), findsNothing);
      expect(opened, ['https://maps']);
    });

    testWidgets('a server refusal shows its own message, without Try again', (
      tester,
    ) async {
      await openScreen(tester, DispatchStatus.acknowledged);
      service.nextError = const ApiException(
        message: 'You are not on this crew',
        statusCode: 403,
      );

      await tester.tap(find.text('Start driving to the scene'));
      await tester.pumpAndSettle();

      expect(find.text('You are not on this crew'), findsOneWidget);
      expect(find.text('Try again'), findsNothing);
    });
  });
}
