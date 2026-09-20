import 'dart:typed_data';

const bool isSupported = false;

Future<void> openFileBytes({
  required Uint8List bytes,
  required String contentType,
  required String fileName,
}) async {
  throw UnsupportedError('Opening a downloaded file is only built for the web app so far.');
}
