import 'package:carelanka_mobile/core/auth/auth_controller.dart';
import 'package:carelanka_mobile/core/auth/patient_login_screen.dart';
import 'package:carelanka_mobile/core/auth/session_expiry.dart';
import 'package:carelanka_mobile/core/auth/token_store.dart';
import 'package:carelanka_mobile/core/auth/welcome_screen.dart';
import 'package:carelanka_mobile/core/routing/app_router.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:carelanka_mobile/services/api_client/models/current_principal.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

/// Where the app opens when nobody is signed in.
///
/// The patient area's first-run form is easy to mistake for the app's entry
/// point, because that is what you see on every launch once a token is stored.
/// A launch with no token must reach the welcome screen instead, or a new
/// patient can neither register nor sign in.
void main() {
  setUp(() => FlutterSecureStorage.setMockInitialValues({}));

  Future<GoRouter> pumpApp(WidgetTester tester) async {
    final sessionExpiry = SessionExpiry();
    addTearDown(sessionExpiry.dispose);

    final auth = AuthController(
      // Never reached: restore() returns before any request when there is no
      // stored token.
      api: CareLankaApi(Dio()),
      tokens: TokenStore(),
      sessionExpiry: sessionExpiry,
    );
    addTearDown(auth.dispose);

    final router = createAppRouter(
      auth: auth,
      routes: [
        GoRoute(
          path: '/me',
          builder: (_, __) => const Scaffold(body: Text('patient area')),
        ),
      ],
      homePathFor: (CurrentPrincipal _) => '/me',
    );
    addTearDown(router.dispose);

    await tester.pumpWidget(MaterialApp.router(routerConfig: router));
    await auth.restore();
    await tester.pumpAndSettle();

    return router;
  }

  testWidgets('a launch with no stored session opens on the welcome screen', (tester) async {
    await pumpApp(tester);

    expect(find.byType(WelcomeScreen), findsOneWidget);
    expect(find.text('Create an account'), findsOneWidget);
    expect(find.text('I already have an account'), findsOneWidget);
  });

  testWidgets('a signed-out deep link into the patient area lands on welcome, not a dead end',
      (tester) async {
    final router = await pumpApp(tester);

    router.go('/me');
    await tester.pumpAndSettle();

    expect(find.byType(WelcomeScreen), findsOneWidget);
    expect(find.text('patient area'), findsNothing);
  });

  testWidgets('sign-in is reachable from the welcome screen', (tester) async {
    await pumpApp(tester);

    await tester.tap(find.text('I already have an account'));
    await tester.pumpAndSettle();

    expect(find.byType(PatientLoginScreen), findsOneWidget);
    expect(find.widgetWithText(TextFormField, 'Username'), findsOneWidget);
  });
}
