import 'dart:typed_data';

Future<void> openFileBytes({
  required Uint8List bytes,
  required String contentType,
  required String fileName,
}) async {
  throw UnsupportedError('Opening a downloaded file is not supported on this platform.');
}

Future<void> clearOpenedFiles() async {}
