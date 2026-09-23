import 'dart:js_interop';
import 'dart:typed_data';

import 'package:web/web.dart' as web;

const bool isSupported = true;

/// A blob URL, not a data: URL — a 10 MB base64 string is a bad time for the
/// browser's address bar, and this never touches it. Left un-revoked on purpose:
/// the new tab still needs it after this function returns, and there is no
/// reliable "the tab is done with it now" moment to revoke on.
Future<void> openFileBytes({
  required Uint8List bytes,
  required String contentType,
  required String fileName,
}) async {
  final blob = web.Blob(
    [bytes.toJS].toJS,
    web.BlobPropertyBag(type: contentType),
  );
  final url = web.URL.createObjectURL(blob);
  web.window.open(url, '_blank');
}
