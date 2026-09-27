import 'package:carelanka_mobile/core/auth/auth_controller.dart';
import 'package:carelanka_mobile/core/auth/session_expiry.dart';
import 'package:carelanka_mobile/core/auth/token_store.dart';
import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/routing/app_router.dart';
import 'package:carelanka_mobile/features/patient/screens/change_password_screen.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

class _FakeAuth extends AuthController {
  _FakeAuth(SessionExpiry sessionExpiry)
      : super(api: CareLankaApi(Dio()), tokens: TokenStore(), sessionExpiry: sessionExpiry);

  ApiException? failWith;
  final calls = <(String, String)>[];

  @override
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    calls.add((currentPassword, newPassword));
    if (failWith != null) throw failWith!;
  }
}

void main() {
  late _FakeAuth auth;

  Future<void> pumpScreen(WidgetTester tester) async {
    final sessionExpiry = SessionExpiry();
    addTearDown(sessionExpiry.dispose);
    auth = _FakeAuth(sessionExpiry);
    addTearDown(auth.dispose);

    final router = GoRouter(routes: [
      GoRoute(path: '/', builder: (_, __) => const ChangePasswordScreen()),
      GoRoute(
        path: AppRoutes.patientLogin,
        builder: (_, __) => const Scaffold(body: Text('sign-in screen')),
      ),
    ]);
    addTearDown(router.dispose);

    await tester.pumpWidget(ChangeNotifierProvider<AuthController>.value(
      value: auth,
      child: MaterialApp.router(routerConfig: router),
    ));
  }

  Future<void> fillAndSubmit(
    WidgetTester tester, {
    required String current,
    required String next,
    required String confirm,
  }) async {
    await tester.enterText(find.widgetWithText(TextField, 'Current password'), current);
    await tester.enterText(find.widgetWithText(TextField, 'New password'), next);
    await tester.enterText(find.widgetWithText(TextField, 'Confirm new password'), confirm);
    await tester.tap(find.widgetWithText(FilledButton, 'Change password'));
    await tester.pumpAndSettle();
  }

  testWidgets('a mismatched confirmation is caught before anything is sent', (tester) async {
    await pumpScreen(tester);

    await fillAndSubmit(tester, current: 'OldPass123', next: 'NewPass123', confirm: 'NewPass124');

    expect(find.text('Passwords do not match'), findsOneWidget);
    expect(auth.calls, isEmpty);
  });

  testWidgets('reusing the current password is caught before anything is sent', (tester) async {
    await pumpScreen(tester);

    await fillAndSubmit(tester, current: 'SamePass123', next: 'SamePass123', confirm: 'SamePass123');

    expect(find.text('Choose a password different from your current one'), findsOneWidget);
    expect(auth.calls, isEmpty);
  });

  testWidgets('a wrong current password shows under that box and stays on the screen',
      (tester) async {
    await pumpScreen(tester);
    auth.failWith = const ApiException(
      message: 'Your current password is not correct.',
      statusCode: 400,
      code: AuthController.currentPasswordIncorrectCode,
    );

    await fillAndSubmit(tester, current: 'WrongPass123', next: 'NewPass123', confirm: 'NewPass123');

    expect(find.text('This is not your current password'), findsOneWidget);
    expect(find.text('sign-in screen'), findsNothing);
  });

  testWidgets('a successful change says so and goes to the sign-in screen', (tester) async {
    await pumpScreen(tester);

    await fillAndSubmit(tester, current: 'OldPass123', next: 'NewPass123', confirm: 'NewPass123');

    expect(auth.calls, [('OldPass123', 'NewPass123')]);
    expect(find.text('sign-in screen'), findsOneWidget);
    expect(find.text('Password changed. Sign in with your new password.'), findsOneWidget);
  });
}
