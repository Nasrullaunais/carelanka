import 'dart:typed_data';

import 'file_opener_stub.dart'
    if (dart.library.io) 'file_opener_io.dart'
    if (dart.library.js_interop) 'file_opener_web.dart' as impl;

/// Opens a downloaded file's bytes for the user to view: the phone's own
/// PDF/image viewer on Android and iOS, a new browser tab on web.
Future<void> openFileBytes({
  required Uint8List bytes,
  required String contentType,
  required String fileName,
}) {
  return impl.openFileBytes(bytes: bytes, contentType: contentType, fileName: fileName);
}

/// Deletes any files this app has opened onto the device. These are medical
/// records, so they shouldn't outlive the session on a shared phone.
Future<void> clearOpenedFiles() => impl.clearOpenedFiles();
