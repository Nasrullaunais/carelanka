import 'package:carelanka_mobile/core/auth/auth_controller.dart';
import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/features/equipment/screens/equipment_home_screen.dart';
import 'package:carelanka_mobile/features/equipment/screens/maintenance_confirmation_screen.dart';
import 'package:carelanka_mobile/features/equipment/state/equipment_confirmation_controller.dart';
import 'package:carelanka_mobile/features/equipment/state/maintenance_confirmation_controller.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'fake_confirmations.dart';

Widget maintenanceApp(MaintenanceConfirmationController controller) {
  return ChangeNotifierProvider.value(
    value: controller,
    child: MaterialApp(theme: AppTheme.light, home: const MaintenanceConfirmationScreen()),
  );
}

Future<void> unlockWith(WidgetTester tester, String code) async {
  await tester.enterText(find.byType(TextField), code);
  await tester.tap(find.text('Unlock'));
  await tester.pumpAndSettle();
}

class _SignedOutAuth extends ChangeNotifier implements AuthController {
  @override
  dynamic noSuchMethod(Invocation invocation) => super.noSuchMethod(invocation);
}

void main() {
  group('the maintenance confirmation controller', () {
    test('a wrong code keeps the jobs hidden', () async {
      final controller = MaintenanceConfirmationController(
        FakeMaintenanceConfirmationService(jobs: [openJob()]),
      );

      expect(await controller.unlock('guess'), isFalse);
      expect(controller.isUnlocked, isFalse);
      expect(controller.unlockProblem, 'That confirmation code is not correct.');
    });

    test('confirming takes the job off the list and the count', () async {
      final service = FakeMaintenanceConfirmationService(
        jobs: [openJob(), openJob(id: 'job-2', label: 'Monitor (asset tag EQ-0102)')],
      );
      final controller = MaintenanceConfirmationController(service);

      await controller.unlock(confirmationCode);
      await controller.confirm(controller.items.valueOrNull!.first);

      expect(service.confirmed, ['job-1']);
      expect(controller.items.valueOrNull!.map((job) => job.id), ['job-2']);
      expect(controller.awaitingCount.valueOrNull, 1);
    });
  });

  group('the maintenance confirmation screen', () {
    testWidgets('shows open jobs only after the code, and confirms after asking', (tester) async {
      final service = FakeMaintenanceConfirmationService(jobs: [openJob()]);
      await tester.pumpWidget(maintenanceApp(MaintenanceConfirmationController(service)));

      expect(find.text('Alarm will not silence.'), findsNothing);

      await unlockWith(tester, confirmationCode);

      expect(find.text('Ventilator (asset tag EQ-0101)'), findsOneWidget);
      expect(find.text('Routine service'), findsOneWidget);
      expect(find.text('Alarm will not silence.'), findsOneWidget);

      await tester.tap(find.text('Confirm done'));
      await tester.pumpAndSettle();
      expect(find.text('Confirm this job is done?'), findsOneWidget);

      await tester.tap(find.text('Cancel'));
      await tester.pumpAndSettle();
      expect(service.confirmed, isEmpty);

      await tester.tap(find.text('Confirm done'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Confirm done').last);
      await tester.pumpAndSettle();

      expect(service.confirmed, ['job-1']);
      expect(
        find.text('Ventilator (asset tag EQ-0101) is confirmed and back in service.'),
        findsOneWidget,
      );
      expect(find.text('Nothing to confirm'), findsOneWidget);
    });
  });

  testWidgets('the administrator home shows both queues with what is waiting', (tester) async {
    await tester.pumpWidget(MultiProvider(
      providers: [
        ChangeNotifierProvider<AuthController>.value(value: _SignedOutAuth()),
        ChangeNotifierProvider(
          create: (_) => EquipmentConfirmationController(
            FakeEquipmentConfirmationService(items: [pendingItem()]),
          ),
        ),
        ChangeNotifierProvider(
          create: (_) => MaintenanceConfirmationController(
            FakeMaintenanceConfirmationService(jobs: [openJob(), openJob(id: 'job-2')]),
          ),
        ),
      ],
      child: MaterialApp(theme: AppTheme.light, home: const EquipmentHomeScreen()),
    ));
    await tester.pumpAndSettle();

    expect(find.text('Equipment confirmation'), findsOneWidget);
    expect(find.text('1 new item is waiting for confirmation.'), findsOneWidget);
    expect(find.text('Maintenance confirmation'), findsOneWidget);
    expect(find.text('2 open maintenance jobs are waiting for confirmation.'), findsOneWidget);
  });
}
