import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/widgets/async_data.dart';
import 'package:carelanka_mobile/features/patient/state/profile_controller.dart';
import 'package:carelanka_mobile/services/api_client/models/gender.dart';
import 'package:carelanka_mobile/services/api_client/models/my_profile.dart';
import 'package:flutter_test/flutter_test.dart';

import 'fake_patient_service.dart';

const _profile = MyProfile(
  patientCode: 'PQ2957CP',
  fullName: 'Test Patient',
  gender: Gender.male,
  detailsComplete: true,
  missingFields: [],
);

void main() {
  test('a fresh account reads as linked-to-nothing, not as an error', () async {
    final service = FakePatientService()
      ..profileResult =
          const ApiException(message: 'not linked', statusCode: 404, code: 'cl_pat_033');
    final controller = ProfileController(service);

    await controller.load();

    expect(controller.profile, isA<AsyncReady<MyProfile?>>());
    expect(controller.isLinked, isFalse);
  });

  test('an existing record reads as linked', () async {
    final controller = ProfileController(FakePatientService()..profileResult = _profile);

    await controller.load();

    expect(controller.isLinked, isTrue);
    expect(controller.profile.valueOrNull?.patientCode, 'PQ2957CP');
  });

  test('a real failure is not mistaken for a missing record', () async {
    final service = FakePatientService()
      ..profileResult = const ApiException(message: 'server fell over', statusCode: 500);
    final controller = ProfileController(service);

    await controller.load();

    expect(controller.profile, isA<AsyncFailed<MyProfile?>>());
    expect(controller.isLinked, isFalse);
  });

  test('saving details links the account without a reload', () async {
    final controller = ProfileController(FakePatientService()..profileResult = _profile);

    final saved = await controller.save(
      nic: '638197257V',
      fullName: 'Test Patient',
      gender: Gender.male,
    );

    expect(saved, isTrue);
    expect(controller.isLinked, isTrue);
  });

  test('a rejected save keeps the field errors for the form', () async {
    final service = FakePatientService()
      ..profileResult = const ApiException(
        message: 'validation failed',
        statusCode: 400,
        fieldErrors: {'nic': ['NIC is already in use.']},
      );
    final controller = ProfileController(service);

    final saved = await controller.save(nic: 'x', fullName: 'x', gender: Gender.male);

    expect(saved, isFalse);
    expect(controller.fieldErrors['nic'], ['NIC is already in use.']);
    expect(controller.saving, isFalse);
  });
}
