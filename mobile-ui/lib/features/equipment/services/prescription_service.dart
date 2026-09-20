import 'package:dio/dio.dart';

import '../../../core/network/api.dart';
import '../../../services/api_client/care_lanka_api.dart';
import '../../../services/api_client/models/my_prescription.dart';
import 'report_file_source.dart';

class PrescriptionService {
  const PrescriptionService(this._api, this._dio);

  final CareLankaApi _api;
  final Dio _dio;

  Future<List<MyPrescription>> listMine() {
    return callApi(() => _api.pharmacy.listMyPrescriptions());
  }

  Future<MyPrescription> upload({required PickedReport file, String? note}) {
    final bytes = file.bytes;
    if (bytes == null) {
      return callApi(() => _api.pharmacy.uploadMyPrescription(file: file.file!, note: note));
    }

    // Mirrors the generated uploadMyPrescription, which only accepts a dart:io File.
    return callApi(() async {
      final response = await _dio.post<Map<String, Object?>>(
        '/me/prescriptions',
        data: FormData.fromMap({
          'File': MultipartFile.fromBytes(bytes, filename: file.name),
          if (note != null) 'Note': note,
        }),
      );
      return MyPrescription.fromJson(response.data!);
    });
  }
}
