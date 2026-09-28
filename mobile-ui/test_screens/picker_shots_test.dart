import 'package:carelanka_mobile/core/auth/auth_controller.dart';
import 'package:carelanka_mobile/core/auth/session_expiry.dart';
import 'package:carelanka_mobile/core/auth/token_store.dart';
import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/features/patient/screens/book_appointment_sheet.dart';
import 'package:carelanka_mobile/features/patient/screens/my_details_screen.dart';
import 'package:carelanka_mobile/features/patient/state/profile_controller.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import '../test/features/patient/fake_patient_service.dart';
import 'patient_shots_test.dart' show loadRealFonts, phone;

// The booking sheet and the details form with every picker open, for design review:
// `flutter test test_screens/picker_shots_test.dart --update-goldens`.
void main() {
  setUpAll(loadRealFonts);

  void phoneSize(WidgetTester tester) {
    tester.view.physicalSize = phone * 2;
    tester.view.devicePixelRatio = 2;
    addTearDown(tester.view.reset);
  }

  Future<void> shot(WidgetTester tester, String name) async {
    await tester.pumpAndSettle();
    await expectLater(find.byType(MaterialApp), matchesGoldenFile('shots/$name.png'));
  }

  for (final (mode, theme) in [('light', AppTheme.light), ('dark', AppTheme.dark)]) {
    testWidgets('booking sheet pickers, $mode', (tester) async {
      phoneSize(tester);
      await tester.pumpWidget(
        MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: theme,
          home: Builder(
            builder: (context) => Scaffold(
              body: Center(
                child: TextButton(
                  onPressed: () => showBookAppointmentSheet(context),
                  child: const Text('open'),
                ),
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.text('open'));
      await shot(tester, 'picker_book_sheet_$mode');

      await tester.tap(find.text('Calendar'));
      await shot(tester, 'picker_book_date_$mode');
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();

      await tester.ensureVisible(find.text('Another time'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Another time'));
      await shot(tester, 'picker_book_time_$mode');
    });

    testWidgets('details form pickers, $mode', (tester) async {
      phoneSize(tester);
      final service = FakePatientService()
        ..profileResult =
            const ApiException(message: 'not linked', statusCode: 404, code: 'cl_pat_033');
      final profile = ProfileController(service);
      await profile.load();
      final sessionExpiry = SessionExpiry();
      addTearDown(sessionExpiry.dispose);
      final auth = AuthController(
        api: CareLankaApi(Dio()),
        tokens: TokenStore(),
        sessionExpiry: sessionExpiry,
      );
      addTearDown(auth.dispose);

      await tester.pumpWidget(
        MultiProvider(
          providers: [
            ChangeNotifierProvider.value(value: profile),
            ChangeNotifierProvider.value(value: auth),
          ],
          child: MaterialApp(
            debugShowCheckedModeBanner: false,
            theme: theme,
            home: const MyDetailsScreen(),
          ),
        ),
      );
      await shot(tester, 'picker_details_$mode');

      await tester.tap(find.text('Date of birth').first);
      await shot(tester, 'picker_details_dob_$mode');
    });
  }
}
