import 'dart:io';
import 'dart:typed_data';

import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';

import 'file_open_exception.dart';

const _folderName = 'opened_files';

Future<void> openFileBytes({
  required Uint8List bytes,
  required String contentType,
  required String fileName,
}) async {
  final folder = await _openedFilesFolder();
  final file = File('${folder.path}/${safeFileName(fileName, contentType)}');
  await file.writeAsBytes(bytes, flush: true);

  final result = await OpenFilex.open(file.path, type: contentType);
  if (result.type != ResultType.done) {
    throw _toException(result.type);
  }
}

Future<void> clearOpenedFiles() async {
  final folder = Directory('${(await getTemporaryDirectory()).path}/$_folderName');
  if (await folder.exists()) await folder.delete(recursive: true);
}

Future<Directory> _openedFilesFolder() async {
  final base = await getTemporaryDirectory();
  return Directory('${base.path}/$_folderName').create(recursive: true);
}

FileOpenException _toException(ResultType type) => switch (type) {
      ResultType.noAppToOpen => const FileOpenException(
          'No app on this phone can open this file. Install a PDF viewer and try again.'),
      ResultType.permissionDenied =>
        const FileOpenException('The phone did not allow the file to be opened.'),
      _ => const FileOpenException('The file could not be opened.'),
    };

/// Turns an untrusted, uploader-supplied file name into one safe to write to
/// disk: only the last path segment survives (no `/`, `\` or `..` can escape
/// the opened-files folder), unsafe characters become `_`, and the extension
/// is kept as-is so the phone's viewer can still tell what it is opening. A
/// name that collapses to nothing falls back to `report` plus the extension
/// [contentType] implies.
String safeFileName(String fileName, String contentType) {
  final segments = fileName.split(RegExp(r'[\\/]'));

  var base = '';
  for (final segment in segments.reversed) {
    if (segment.isNotEmpty && segment != '.' && segment != '..') {
      base = segment;
      break;
    }
  }

  final cleaned = base.replaceAll(RegExp(r'[^A-Za-z0-9._\- ]'), '_').trim();

  if (cleaned.isEmpty || RegExp(r'^\.+$').hasMatch(cleaned)) {
    return 'report${_extensionFor(contentType)}';
  }

  return cleaned;
}

String _extensionFor(String contentType) => switch (contentType) {
      'application/pdf' => '.pdf',
      'image/jpeg' => '.jpg',
      'image/png' => '.png',
      _ => '',
    };
