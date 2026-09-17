import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/my_prescription.dart';
import '../services/prescription_service.dart';
import '../services/report_file_source.dart';

class MyPrescriptionsController extends ChangeNotifier {
  MyPrescriptionsController(this._service, this._files);

  final PrescriptionService _service;
  final ReportFileSource _files;

  AsyncData<List<MyPrescription>> _prescriptions = const AsyncData.loading();
  PickedReport? _attachment;
  bool _uploading = false;
  ApiException? _uploadFailure;

  AsyncData<List<MyPrescription>> get prescriptions => _prescriptions;
  PickedReport? get attachment => _attachment;
  bool get uploading => _uploading;
  ApiException? get uploadFailure => _uploadFailure;

  Future<void> load({bool showLoading = true}) async {
    if (showLoading) {
      _prescriptions = const AsyncData.loading();
      notifyListeners();
    }

    try {
      _prescriptions = AsyncData.ready(await _service.listMine());
    } on ApiException catch (error) {
      _prescriptions = AsyncData.failed(error);
    }
    notifyListeners();
  }

  Future<void> capturePhoto() => _attach(_files.capturePhoto);

  Future<void> pickDocument() => _attach(_files.pickDocument);

  Future<void> _attach(Future<PickedReport?> Function() pick) async {
    final picked = await pick();
    if (picked == null) return;

    _attachment = picked;
    _uploadFailure = null;
    notifyListeners();
  }

  void removeAttachment() {
    _attachment = null;
    notifyListeners();
  }

  void clearForm() {
    _attachment = null;
    _uploadFailure = null;
  }

  String? validate({required String note}) {
    if (note.trim().length > 500) return 'The note must be 500 characters or fewer.';

    final file = _attachment;
    if (file == null) return 'Take a photo of the prescription or attach a PDF.';
    if (!file.hasAllowedType) return 'Only a photo (JPEG or PNG) or a PDF can be sent.';
    if (file.isTooBig) return 'The file is larger than 10 MB.';

    return null;
  }

  Future<MyPrescription?> upload({required String note}) async {
    final file = _attachment;
    if (file == null || _uploading) return null;

    _uploading = true;
    _uploadFailure = null;
    notifyListeners();

    final written = note.trim();

    try {
      final sent = await _service.upload(file: file, note: written.isEmpty ? null : written);

      _attachment = null;
      final current = _prescriptions.valueOrNull ?? const <MyPrescription>[];
      _prescriptions = AsyncData.ready([sent, ...current]);
      return sent;
    } on ApiException catch (error) {
      _uploadFailure = error;
      return null;
    } finally {
      _uploading = false;
      notifyListeners();
    }
  }
}
