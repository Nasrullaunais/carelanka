import 'package:carelanka_mobile/core/auth/auth_controller.dart';
import 'package:carelanka_mobile/core/auth/session_expiry.dart';
import 'package:carelanka_mobile/core/auth/token_store.dart';
import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/features/equipment/services/prescription_service.dart';
import 'package:carelanka_mobile/features/patient/screens/home_screen.dart';
import 'package:carelanka_mobile/features/patient/screens/my_stay_screen.dart';
import 'package:carelanka_mobile/features/patient/screens/profile_screen.dart';
import 'package:carelanka_mobile/features/patient/services/patient_service.dart';
import 'package:carelanka_mobile/features/patient/state/appointments_controller.dart';
import 'package:carelanka_mobile/features/patient/state/my_stay_controller.dart';
import 'package:carelanka_mobile/features/patient/state/profile_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/admission_status.dart';
import 'package:carelanka_mobile/services/api_client/models/appointment_status.dart';
import 'package:carelanka_mobile/services/api_client/models/gender.dart';
import 'package:carelanka_mobile/services/api_client/models/my_admission.dart';
import 'package:carelanka_mobile/services/api_client/models/my_appointment.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:carelanka_mobile/services/api_client/models/my_profile.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import '../equipment/fake_prescriptions.dart';
import 'fake_patient_service.dart';

/// Renders the patient screens at real phone sizes, in both themes.
///
/// A widget that overflows its box paints a yellow-and-black bar and fails the
/// test, which is the cheapest way to catch a card that fits in light mode and
/// bursts in dark, or on a small screen.
void main() {
  final admission = MyAdmission(
    admissionId: 'a1',
    status: AdmissionStatus.admitted,
    statusText: 'You are in Ward A, bed 12.',
    wardName: 'Ward A',
    bedNumber: '12',
    admittedAt: DateTime.utc(2026, 9, 12, 8),
    detailsComplete: false,
    missingFields: const ['emergency_contact_phone'],
  );

  final appointment = MyAppointment(
    appointmentId: 'p1',
    scheduledAt: DateTime.utc(2026, 9, 18, 3, 30),
    status: AppointmentStatus.scheduled,
    statusText: 'Booked. You can still cancel this.',
    reason: 'Follow-up on chest pain',
    canCancel: true,
    cancelledByHospital: false,
  );

  const incompleteProfile = MyProfile(
    patientCode: 'PTKC7Y7V',
    fullName: 'Chathura Wijesinghe',
    nic: '199012345678',
    gender: Gender.male,
    detailsComplete: false,
    missingFields: ['phone', 'address', 'emergency_contact_name', 'date_of_birth'],
  );

  const completeProfile = MyProfile(
    patientCode: 'PTKC7Y7V',
    fullName: 'Chathura Wijesinghe',
    nic: '199012345678',
    gender: Gender.male,
    phone: '+94771234567',
    address: '14 Galle Road, Colombo 03',
    emergencyContactName: 'Nimali Wijesinghe',
    emergencyContactPhone: '+94777654321',
    detailsComplete: true,
    missingFields: [],
  );

  /// The narrowest phone still in common use. If a layout survives this it
  /// survives anything wider.
  const smallPhone = Size(320, 640);
  const normalPhone = Size(390, 844);

  Future<void> pumpPatientScreen(
    WidgetTester tester,
    Widget screen, {
    required ThemeData theme,
    required Size size,
    MyProfile profile = completeProfile,
    MyAdmission? currentAdmission,
    List<MyAppointment> appointments = const [],
  }) async {
    tester.view.physicalSize = size;
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);

    final service = FakePatientService()
      ..profileResult = profile
      // No stay is a 404 carrying cl_pat_034, not a null. Handing the
      // controller a null would test a case the API cannot produce.
      ..admissionResult =
          currentAdmission ??
          const ApiException(message: 'not admitted', statusCode: 404, code: 'cl_pat_034')
      ..appointmentsResult = appointmentPage(appointments);

    final profileController = ProfileController(service);
    final stayController = MyStayController(service);
    final appointmentsController = AppointmentsController(service);

    await profileController.load();
    await stayController.load();
    await appointmentsController.load();

    // Profile reads this for its sign-out row. Nothing in these tests taps it,
    // so it is never asked to talk to the network or the keystore.
    final auth = AuthController(
      api: CareLankaApi(Dio()),
      tokens: TokenStore(),
      sessionExpiry: SessionExpiry(),
    );
    addTearDown(auth.dispose);

    await tester.pumpWidget(
      MultiProvider(
        providers: [
          Provider<PatientService>.value(value: service),
          ChangeNotifierProvider.value(value: profileController),
          ChangeNotifierProvider.value(value: stayController),
          ChangeNotifierProvider.value(value: appointmentsController),
          ChangeNotifierProvider.value(value: auth),
          Provider<PrescriptionService>.value(value: FakePrescriptionService()),
        ],
        child: MaterialApp(theme: theme, home: screen),
      ),
    );
    await tester.pump();
  }

  for (final (themeName, theme) in [('light', AppTheme.light), ('dark', AppTheme.dark)]) {
    group('$themeName theme', () {
      testWidgets('home lays out with a booking and an incomplete profile', (tester) async {
        await pumpPatientScreen(
          tester,
          HomeScreen(onOpenTab: (_) {}),
          theme: theme,
          size: normalPhone,
          profile: incompleteProfile,
          appointments: [appointment],
        );

        expect(find.text('Chathura'), findsOneWidget);
        expect(find.text('PTKC7Y7V'), findsOneWidget);
        // The server's own sentence, not one the app made up.
        expect(find.text('Booked. You can still cancel this.'), findsOneWidget);
        expect(tester.takeException(), isNull);
      });

      testWidgets('home lays out on a 320px phone', (tester) async {
        await pumpPatientScreen(
          tester,
          HomeScreen(onOpenTab: (_) {}),
          theme: theme,
          size: smallPhone,
          profile: incompleteProfile,
          appointments: [appointment],
        );

        expect(tester.takeException(), isNull);
      });

      testWidgets('home shows the stay instead of the diary when admitted', (tester) async {
        await pumpPatientScreen(
          tester,
          HomeScreen(onOpenTab: (_) {}),
          theme: theme,
          size: normalPhone,
          currentAdmission: admission,
          appointments: [appointment],
        );

        // Someone lying in a bed does not care what is in the diary next week.
        expect(find.text('You are in Ward A, bed 12.'), findsOneWidget);
        expect(find.text('YOUR NEXT VISIT'), findsNothing);
        expect(tester.takeException(), isNull);
      });

      testWidgets('the stay screen draws the journey without overflowing', (tester) async {
        await pumpPatientScreen(
          tester,
          const MyStayScreen(),
          theme: theme,
          size: smallPhone,
          currentAdmission: admission,
        );

        expect(find.text('You are here'), findsOneWidget);
        expect(find.text('In hospital'), findsOneWidget);
        expect(tester.takeException(), isNull);
      });

      testWidgets('the care card is there while admitted', (tester) async {
        await pumpPatientScreen(
          tester,
          const MyStayScreen(),
          theme: theme,
          size: normalPhone,
          currentAdmission: admission,
        );

        expect(find.text('How are you feeling?'), findsOneWidget);
        expect(tester.takeException(), isNull);
      });

      testWidgets('the care card is not offered while still waiting for a bed', (tester) async {
        await pumpPatientScreen(
          tester,
          const MyStayScreen(),
          theme: theme,
          size: normalPhone,
          currentAdmission: const MyAdmission(
            admissionId: 'a2',
            status: AdmissionStatus.awaitingBed,
            statusText: 'We are finding you a bed.',
            detailsComplete: true,
            missingFields: [],
          ),
        );

        // The server refuses a report until the patient is on the ward, so the card must not
        // invite one.
        expect(find.text('How are you feeling?'), findsNothing);
        expect(tester.takeException(), isNull);
      });

      testWidgets('profile lays out with every field filled in', (tester) async {
        await pumpPatientScreen(tester, const ProfileScreen(), theme: theme, size: smallPhone);

        // Off the bottom of a 320x640 screen, so the list has to be scrolled
        // before it is built at all.
        await tester.scrollUntilVisible(find.text('Nimali Wijesinghe'), 200);
        expect(find.text('Nimali Wijesinghe'), findsOneWidget);
        expect(find.text('+94777654321'), findsOneWidget);
        expect(tester.takeException(), isNull);
      });
    });
  }
}
