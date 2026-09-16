import 'dart:io';

import 'package:file_picker/file_picker.dart';
import 'package:image_picker/image_picker.dart';

class PickedReport {
  const PickedReport({required this.file, required this.name});

  final File file;
  final String name;

  static const maxBytes = 10 * 1024 * 1024;

  static const allowedExtensions = ['pdf', 'jpg', 'jpeg', 'png'];

  int get byteSize => file.lengthSync();

  bool get isTooBig => byteSize > maxBytes;

  bool get hasAllowedType {
    final dot = name.lastIndexOf('.');
    if (dot < 0 || dot == name.length - 1) return false;
    return allowedExtensions.contains(name.substring(dot + 1).toLowerCase());
  }
}

abstract class ReportFileSource {
  Future<PickedReport?> capturePhoto();

  Future<PickedReport?> pickDocument();
}

class DeviceReportFileSource implements ReportFileSource {
  DeviceReportFileSource({ImagePicker? picker}) : _picker = picker ?? ImagePicker();

  final ImagePicker _picker;

  @override
  Future<PickedReport?> capturePhoto() async {
    // A full-resolution phone photo of an A4 page can exceed the API's 10 MB limit.
    final shot = await _picker.pickImage(
      source: ImageSource.camera,
      maxWidth: 2400,
      imageQuality: 85,
    );
    if (shot == null) return null;

    return PickedReport(file: File(shot.path), name: shot.name);
  }

  @override
  Future<PickedReport?> pickDocument() async {
    final result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: PickedReport.allowedExtensions,
    );

    final picked = result?.files.singleOrNull;
    final path = picked?.path;
    if (picked == null || path == null) return null;

    return PickedReport(file: File(path), name: picked.name);
  }
}
