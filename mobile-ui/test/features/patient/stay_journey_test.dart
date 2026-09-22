import 'package:carelanka_mobile/features/patient/widgets/stay_journey.dart';
import 'package:carelanka_mobile/services/api_client/models/admission_status.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('the seven stored states fold onto four the patient can follow', () {
    // The grouping published in patient-spec.yaml as WorklistStatus. If the API
    // regroups them, this is the test that should fail.
    expect(StayJourney.of(AdmissionStatus.awaitingBed).reached, JourneyStep.waitingForBed);
    expect(StayJourney.of(AdmissionStatus.awaitingApproval).reached, JourneyStep.waitingForBed);
    expect(StayJourney.of(AdmissionStatus.bedReserved).reached, JourneyStep.bedReady);
    expect(StayJourney.of(AdmissionStatus.admitted).reached, JourneyStep.inHospital);
    expect(StayJourney.of(AdmissionStatus.readyForDischarge).reached, JourneyStep.inHospital);
    expect(StayJourney.of(AdmissionStatus.discharged).reached, JourneyStep.home);
  });

  test('waiting for approval is not a step of its own', () {
    // From the patient's side nothing has changed: they are still standing
    // there without a bed.
    expect(
      StayJourney.of(AdmissionStatus.awaitingApproval).reached,
      StayJourney.of(AdmissionStatus.awaitingBed).reached,
    );
  });

  test('ready for discharge still counts as being in hospital', () {
    // The flag says they could go home, not that they have.
    expect(
      StayJourney.of(AdmissionStatus.readyForDischarge).reached,
      StayJourney.of(AdmissionStatus.admitted).reached,
    );
  });

  test('a cancelled stay is off the path, not at the end of it', () {
    final journey = StayJourney.of(AdmissionStatus.cancelled);
    expect(journey.cancelled, isTrue);
    expect(journey.reached, isNull);
  });

  test('a status this build has never heard of shows no progress at all', () {
    // Better a blank rail than a confident wrong one.
    final journey = StayJourney.of(AdmissionStatus.$unknown);
    expect(journey.reached, isNull);
    expect(journey.cancelled, isFalse);
  });

  test('earlier steps read as done and later ones do not', () {
    final journey = StayJourney.of(AdmissionStatus.admitted);

    expect(journey.isDone(JourneyStep.waitingForBed), isTrue);
    expect(journey.isDone(JourneyStep.bedReady), isTrue);
    expect(journey.isCurrent(JourneyStep.inHospital), isTrue);
    expect(journey.isDone(JourneyStep.inHospital), isFalse);
    expect(journey.isDone(JourneyStep.home), isFalse);
  });

  test('a cancelled stay marks nothing as done', () {
    final journey = StayJourney.of(AdmissionStatus.cancelled);
    for (final step in JourneyStep.values) {
      expect(journey.isDone(step), isFalse);
      expect(journey.isCurrent(step), isFalse);
    }
  });
}
