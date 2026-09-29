import 'package:carelanka_mobile/core/auth/auth_controller.dart';
import 'package:carelanka_mobile/core/auth/session_expiry.dart';
import 'package:carelanka_mobile/core/auth/token_store.dart';
import 'package:carelanka_mobile/core/auth/welcome_screen.dart';
import 'package:carelanka_mobile/core/routing/app_router.dart';
import 'package:carelanka_mobile/core/routing/splash_screen.dart';
import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:carelanka_mobile/services/api_client/models/current_principal.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  setUp(() => FlutterSecureStorage.setMockInitialValues({}));

  Future<AuthController> pumpApp(WidgetTester tester, {ThemeMode mode = ThemeMode.light}) async {
    final sessionExpiry = SessionExpiry();
    addTearDown(sessionExpiry.dispose);

    final auth = AuthController(
      api: CareLankaApi(Dio()),
      tokens: TokenStore(),
      sessionExpiry: sessionExpiry,
    );
    addTearDown(auth.dispose);

    final router = createAppRouter(
      auth: auth,
      routes: const [],
      homePathFor: (CurrentPrincipal _) => '/',
    );
    addTearDown(router.dispose);

    await tester.pumpWidget(MaterialApp.router(
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      themeMode: mode,
      routerConfig: router,
    ));

    return auth;
  }

  testWidgets('the launch opens on the splash with the CareLanka name', (tester) async {
    final auth = await pumpApp(tester);

    expect(find.byType(SplashScreen), findsOneWidget);
    expect(find.text('CareLanka'), findsOneWidget);

    await auth.restore();
    await tester.pumpAndSettle();
  });

  testWidgets('the splash stays up until its animation ends, even once the session is known',
      (tester) async {
    final auth = await pumpApp(tester);
    await auth.restore();
    await tester.pump(const Duration(milliseconds: 600));

    expect(auth.status, AuthStatus.signedOut);
    expect(find.byType(SplashScreen), findsOneWidget);
    expect(find.byType(WelcomeScreen), findsNothing);

    await tester.pumpAndSettle();

    expect(find.byType(SplashScreen), findsNothing);
    expect(find.byType(WelcomeScreen), findsOneWidget);
  });

  testWidgets('the splash sits on the dark page colour in dark mode', (tester) async {
    final auth = await pumpApp(tester, mode: ThemeMode.dark);

    final scaffold = tester.widget<Scaffold>(
      find.descendant(of: find.byType(SplashScreen), matching: find.byType(Scaffold)),
    );
    final page = Theme.of(tester.element(find.byWidget(scaffold))).scaffoldBackgroundColor;

    expect(scaffold.backgroundColor, isNull);
    expect(page, const Color(0xFF0A0F1C));

    await auth.restore();
    await tester.pumpAndSettle();
  });
}
