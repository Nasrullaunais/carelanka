import 'dart:io';

import 'package:carelanka_mobile/core/auth/auth_controller.dart';
import 'package:carelanka_mobile/core/auth/session_expiry.dart';
import 'package:carelanka_mobile/core/auth/token_store.dart';
import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/theme/app_theme.dart';
import 'package:carelanka_mobile/features/equipment/services/prescription_service.dart';
import 'package:carelanka_mobile/features/patient/screens/patient_shell.dart';
import 'package:carelanka_mobile/features/patient/services/patient_service.dart';
import 'package:carelanka_mobile/features/patient/state/appointments_controller.dart';
import 'package:carelanka_mobile/features/patient/state/my_stay_controller.dart';
import 'package:carelanka_mobile/features/patient/state/profile_controller.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:carelanka_mobile/services/api_client/models/admission_status.dart';
import 'package:carelanka_mobile/services/api_client/models/appointment_status.dart';
import 'package:carelanka_mobile/services/api_client/models/gender.dart';
import 'package:carelanka_mobile/services/api_client/models/my_admission.dart';
import 'package:carelanka_mobile/services/api_client/models/my_appointment.dart';
import 'package:carelanka_mobile/services/api_client/models/my_profile.dart';
import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import '../test/features/equipment/fake_prescriptions.dart';
import '../test/features/patient/fake_patient_service.dart';

// Renders real screens to PNGs for design review: `flutter test test_screens --update-goldens`.
// Not part of the suite; the images land in test_screens/shots/.
const phone = Size(384, 832);

Future<void> loadRealFonts() async {
  Future<ByteData> bytes(String path) async =>
      ByteData.view(Uint8List.fromList(await File(path).readAsBytes()).buffer);

  final figtree = FontLoader('Figtree');
  for (final w in [400, 500, 600, 700, 800]) {
    figtree.addFont(bytes('assets/fonts/Figtree-$w.ttf'));
  }
  await figtree.load();

  final flutterRoot = Platform.environment['FLUTTER_ROOT'] ?? r'C:\src\flutter';
  final icons = FontLoader('MaterialIcons')
    ..addFont(bytes('$flutterRoot/bin/cache/artifacts/material_fonts/materialicons-regular.otf'));
  await icons.load();
}

void main() {
  setUpAll(loadRealFonts);

  final admission = MyAdmission(
    admissionId: 'a1',
    status: AdmissionStatus.admitted,
    statusText: 'You are in Ward A, bed 12.',
    wardName: 'Ward A',
    bedNumber: '12',
    admittedAt: DateTime.utc(2026, 9, 12, 8),
    detailsComplete: true,
    missingFields: const [],
  );

  final appointment = MyAppointment(
    appointmentId: 'p1',
    scheduledAt: DateTime.now().add(const Duration(days: 2, hours: 3)).toUtc(),
    status: AppointmentStatus.scheduled,
    statusText: 'Booked. You can still cancel this.',
    reason: 'Follow-up on chest pain',
    canCancel: true,
    cancelledByHospital: false,
  );

  final pastAppointment = MyAppointment(
    appointmentId: 'p0',
    scheduledAt: DateTime.utc(2026, 8, 30, 4),
    status: AppointmentStatus.completed,
    statusText: 'Seen by the doctor.',
    reason: 'Blood pressure check',
    canCancel: false,
    cancelledByHospital: false,
  );

  const profile = MyProfile(
    patientCode: 'PTKC7Y7V',
    fullName: 'Kasun Mendis',
    nic: '199012345678',
    gender: Gender.male,
    phone: '+94771234567',
    address: '14 Galle Road, Colombo 03',
    emergencyContactName: 'Nimali Mendis',
    emergencyContactPhone: '+94777654321',
    detailsComplete: true,
    missingFields: [],
  );

  Future<void> pumpShell(
    WidgetTester tester, {
    required ThemeData theme,
    MyAdmission? current,
    List<MyAppointment> appointments = const [],
  }) async {
    tester.view.physicalSize = phone * 2;
    tester.view.devicePixelRatio = 2;
    addTearDown(tester.view.reset);

    final service = FakePatientService()
      ..profileResult = profile
      ..admissionResult =
          current ??
          const ApiException(message: 'not admitted', statusCode: 404, code: 'cl_pat_034')
      ..appointmentsResult = appointmentPage(appointments);

    final profileController = ProfileController(service);
    final stay = MyStayController(service);
    final appts = AppointmentsController(service);
    await profileController.load();
    await stay.load();
    await appts.load();

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
          Provider<PatientService>.value(value: service),
          ChangeNotifierProvider.value(value: profileController),
          ChangeNotifierProvider.value(value: stay),
          ChangeNotifierProvider.value(value: appts),
          ChangeNotifierProvider.value(value: auth),
          Provider<PrescriptionService>.value(value: FakePrescriptionService()),
        ],
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: theme,
          home: const PatientShell(),
        ),
      ),
    );
    await tester.pumpAndSettle();
  }

  Future<void> shot(WidgetTester tester, String name) async {
    await tester.pumpAndSettle();
    await expectLater(find.byType(MaterialApp), matchesGoldenFile('shots/$name.png'));
  }

  for (final (mode, theme) in [('light', AppTheme.light), ('dark', AppTheme.dark)]) {
    testWidgets('patient tabs, not admitted, $mode', (tester) async {
      await pumpShell(tester, theme: theme, appointments: [appointment, pastAppointment]);
      await shot(tester, 'patient_home_$mode');
      for (final (label, file) in [
        ('My visits', 'visits'),
        ('My stay', 'stay_none'),
        ('Prescriptions', 'prescriptions'),
        ('Profile', 'profile'),
      ]) {
        await tester.tap(find.byTooltip(label));
        await shot(tester, 'patient_${file}_$mode');
      }
    });

    testWidgets('patient tabs, admitted, $mode', (tester) async {
      await pumpShell(tester, theme: theme, current: admission);
      await shot(tester, 'patient_home_admitted_$mode');
      await tester.tap(find.byTooltip('My stay'));
      await shot(tester, 'patient_stay_admitted_$mode');
    });
  }
}
