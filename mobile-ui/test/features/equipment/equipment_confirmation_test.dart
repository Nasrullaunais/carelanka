import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/features/equipment/screens/equipment_confirmation_screen.dart';
import 'package:carelanka_mobile/features/equipment/services/equipment_confirmation_service.dart';
import 'package:carelanka_mobile/features/equipment/state/equipment_confirmation_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/equipment_item.dart';
import 'package:carelanka_mobile/services/api_client/models/equipment_status.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

const _code = 'equipment2026';

const _wrongCode = ApiException(
  message: 'That confirmation code is not correct.',
  statusCode: 403,
  code: 'cl_equ_017',
);

class FakeEquipmentConfirmationService implements EquipmentConfirmationService {
  FakeEquipmentConfirmationService({List<EquipmentItem>? items, this.actionFailure})
      : items = items ?? [];

  List<EquipmentItem> items;
  ApiException? actionFailure;

  final codesSent = <String>[];
  final confirmed = <String>[];
  final rejected = <String>[];

  void _check(String code) {
    codesSent.add(code);
    if (code != _code) throw _wrongCode;
  }

  @override
  Future<int> countAwaiting() async => items.length;

  @override
  Future<List<EquipmentItem>> listAwaiting(String code) async {
    _check(code);
    return List.of(items);
  }

  @override
  Future<EquipmentItem> confirm(String id, String code) async {
    _check(code);
    final failure = actionFailure;
    if (failure != null) throw failure;

    confirmed.add(id);
    final item = items.firstWhere((i) => i.id == id);
    items.remove(item);
    return item;
  }

  @override
  Future<void> reject(String id, String code) async {
    _check(code);
    final failure = actionFailure;
    if (failure != null) throw failure;

    rejected.add(id);
    items.removeWhere((i) => i.id == id);
  }
}

EquipmentItem pendingItem({String id = 'item-1', String name = 'Ventilator', String tag = 'EQ-0101'}) =>
    EquipmentItem(
      id: id,
      name: name,
      categoryId: 'category-1',
      categoryName: 'Life support',
      model: 'V-100',
      manufacturer: 'Acme Medical',
      assetTag: tag,
      status: EquipmentStatus.available,
      purchaseDate: DateTime(2026, 9, 1),
      awaitingConfirmation: true,
      createdAt: DateTime.utc(2026, 9, 16, 4, 30),
      updatedAt: DateTime.utc(2026, 9, 16, 4, 30),
    );

Widget app(EquipmentConfirmationController controller) {
  return ChangeNotifierProvider.value(
    value: controller,
    child: MaterialApp(theme: AppTheme.light, home: const EquipmentConfirmationScreen()),
  );
}

Future<void> unlockWith(WidgetTester tester, String code) async {
  await tester.enterText(find.byType(TextField), code);
  await tester.tap(find.text('Unlock'));
  await tester.pumpAndSettle();
}

void main() {
  group('the confirmation controller', () {
    test('a wrong code keeps the list locked and shows the server message', () async {
      final controller = EquipmentConfirmationController(
        FakeEquipmentConfirmationService(items: [pendingItem()]),
      );

      final unlocked = await controller.unlock('guess');

      expect(unlocked, isFalse);
      expect(controller.isUnlocked, isFalse);
      expect(controller.unlockProblem, 'That confirmation code is not correct.');
    });

    test('a blank code is not sent to the server', () async {
      final service = FakeEquipmentConfirmationService();
      final controller = EquipmentConfirmationController(service);

      await controller.unlock('   ');

      expect(service.codesSent, isEmpty);
      expect(controller.unlockProblem, 'Enter the confirmation code.');
    });

    test('the right code shows what is waiting and is sent with every action', () async {
      final service = FakeEquipmentConfirmationService(
        items: [pendingItem(), pendingItem(id: 'item-2', name: 'Monitor', tag: 'EQ-0102')],
      );
      final controller = EquipmentConfirmationController(service);

      await controller.unlock(' $_code ');
      final refused = await controller.confirm(controller.items.valueOrNull!.first);

      expect(refused, isNull);
      expect(service.confirmed, ['item-1']);
      expect(service.codesSent, [_code, _code]);
      expect(controller.items.valueOrNull!.map((i) => i.name), ['Monitor']);
      expect(controller.awaitingCount.valueOrNull, 1);
    });

    test('rejecting removes the item from the list', () async {
      final service = FakeEquipmentConfirmationService(items: [pendingItem()]);
      final controller = EquipmentConfirmationController(service);

      await controller.unlock(_code);
      await controller.reject(controller.items.valueOrNull!.single);

      expect(service.rejected, ['item-1']);
      expect(controller.items.valueOrNull, isEmpty);
    });

    test('an item somebody else already confirmed drops out of the list', () async {
      final service = FakeEquipmentConfirmationService(
        items: [pendingItem()],
        actionFailure: const ApiException(
          message: 'Ventilator is not waiting for confirmation.',
          statusCode: 409,
        ),
      );
      final controller = EquipmentConfirmationController(service);

      await controller.unlock(_code);
      final refused = await controller.confirm(controller.items.valueOrNull!.single);

      expect(refused!.isConflict, isTrue);
      expect(controller.items.valueOrNull, isEmpty);
    });

    test('locking forgets the code', () async {
      final controller = EquipmentConfirmationController(
        FakeEquipmentConfirmationService(items: [pendingItem()]),
      );

      await controller.unlock(_code);
      controller.lock();

      expect(controller.isUnlocked, isFalse);
    });
  });

  group('the confirmation screen', () {
    testWidgets('asks for the code before showing anything', (tester) async {
      final controller = EquipmentConfirmationController(
        FakeEquipmentConfirmationService(items: [pendingItem()]),
      );
      await tester.pumpWidget(app(controller));

      expect(find.text('Enter the confirmation code'), findsOneWidget);
      expect(find.text('Ventilator'), findsNothing);

      await unlockWith(tester, 'guess');

      expect(find.text('That confirmation code is not correct.'), findsOneWidget);
      expect(find.text('Ventilator'), findsNothing);
    });

    testWidgets('confirming an item says it now shows on the web dashboard', (tester) async {
      final service = FakeEquipmentConfirmationService(items: [pendingItem()]);
      await tester.pumpWidget(app(EquipmentConfirmationController(service)));

      await unlockWith(tester, _code);

      expect(find.text('Ventilator'), findsOneWidget);
      expect(find.text('EQ-0101 · Life support'), findsOneWidget);

      await tester.tap(find.text('Confirm'));
      await tester.pumpAndSettle();

      expect(service.confirmed, ['item-1']);
      expect(find.text('Ventilator confirmed. It now shows on the web dashboard.'), findsOneWidget);
      expect(find.text('Nothing to confirm'), findsOneWidget);
    });

    testWidgets('rejecting asks first and does nothing when cancelled', (tester) async {
      final service = FakeEquipmentConfirmationService(items: [pendingItem()]);
      await tester.pumpWidget(app(EquipmentConfirmationController(service)));
      await unlockWith(tester, _code);

      await tester.tap(find.text('Reject'));
      await tester.pumpAndSettle();
      expect(find.text('Reject Ventilator?'), findsOneWidget);

      await tester.tap(find.text('Cancel'));
      await tester.pumpAndSettle();
      expect(service.rejected, isEmpty);

      await tester.tap(find.text('Reject'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Reject'));
      await tester.pumpAndSettle();

      expect(service.rejected, ['item-1']);
      expect(find.text('Ventilator rejected.'), findsOneWidget);
    });

    testWidgets('fits a phone-width display', (tester) async {
      tester.view.physicalSize = const Size(360, 740);
      tester.view.devicePixelRatio = 1.0;
      addTearDown(tester.view.reset);

      final service = FakeEquipmentConfirmationService(items: [pendingItem()]);
      await tester.pumpWidget(app(EquipmentConfirmationController(service)));
      await unlockWith(tester, _code);

      expect(tester.takeException(), isNull);
    });
  });
}
