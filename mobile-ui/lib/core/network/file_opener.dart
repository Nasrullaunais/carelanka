import 'dart:typed_data';

import 'file_opener_stub.dart'
    if (dart.library.html) 'file_opener_web.dart' as impl;

/// Opens a downloaded file's bytes for the user to view, in whatever way the
/// current platform supports. Web opens a new tab; other platforms have no
/// implementation yet, so [isSupported] tells the caller whether to try.
bool get isFileOpenerSupported => impl.isSupported;

Future<void> openFileBytes({
  required Uint8List bytes,
  required String contentType,
  required String fileName,
}) {
  return impl.openFileBytes(bytes: bytes, contentType: contentType, fileName: fileName);
}
