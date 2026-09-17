import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/features/equipment/services/prescription_service.dart';
import 'package:carelanka_mobile/features/equipment/services/report_file_source.dart';
import 'package:carelanka_mobile/services/api_client/models/my_prescription.dart';
import 'package:carelanka_mobile/services/api_client/models/prescription_status.dart';

class FakePrescriptionService implements PrescriptionService {
  FakePrescriptionService({List<MyPrescription>? prescriptions, this.listFailure, this.uploadFailure})
      : prescriptions = prescriptions ?? [];

  List<MyPrescription> prescriptions;
  ApiException? listFailure;
  ApiException? uploadFailure;

  int uploads = 0;
  String? uploadedNote;

  @override
  Future<List<MyPrescription>> listMine() async {
    final failure = listFailure;
    if (failure != null) throw failure;
    return List.of(prescriptions);
  }

  @override
  Future<MyPrescription> upload({required PickedReport file, String? note}) async {
    uploads++;
    uploadedNote = note;

    final failure = uploadFailure;
    if (failure != null) throw failure;

    final sent = myPrescription(id: 'sent-$uploads', note: note);
    prescriptions.insert(0, sent);
    return sent;
  }
}

MyPrescription myPrescription({
  String id = 'rx-1',
  PrescriptionStatus status = PrescriptionStatus.submitted,
  int? token,
  String? note,
  String? rejectionReason,
}) =>
    MyPrescription(
      id: id,
      note: note,
      fileName: 'prescription.jpg',
      contentType: 'image/jpeg',
      byteSize: 2048,
      status: status,
      tokenNumber: token,
      tokenDate: token == null ? null : DateTime(2026, 9, 17),
      readyAt: token == null ? null : DateTime.utc(2026, 9, 17, 5),
      deliveredAt: status == PrescriptionStatus.delivered ? DateTime.utc(2026, 9, 17, 7) : null,
      rejectionReason: rejectionReason,
      createdAt: DateTime.utc(2026, 9, 17, 3, 30),
    );
