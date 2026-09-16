import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import '../services/report_file_source.dart';

class ReportAttachment extends StatelessWidget {
  const ReportAttachment({
    super.key,
    required this.attachment,
    required this.enabled,
    required this.onPhotograph,
    required this.onAttachPdf,
    required this.onRemove,
    this.uploadSupported = !kIsWeb,
  });

  final PickedReport? attachment;
  final bool enabled;
  final VoidCallback onPhotograph;
  final VoidCallback onAttachPdf;
  final VoidCallback onRemove;

  // The generated upload call takes a dart:io File, which a browser cannot provide.
  final bool uploadSupported;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final picked = attachment;

    if (picked == null) {
      final canPick = enabled && uploadSupported;

      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (!uploadSupported) ...[
            Text(
              'Uploading a report works in the Android or iOS app, not in a browser.',
              style: TextStyle(color: theme.colorScheme.error),
            ),
            const SizedBox(height: 8),
          ],
          FilledButton.tonalIcon(
            onPressed: canPick ? onPhotograph : null,
            icon: const Icon(Icons.photo_camera_outlined),
            label: const Text('Photograph the report'),
            style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
          ),
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: canPick ? onAttachPdf : null,
            icon: const Icon(Icons.attach_file),
            label: const Text('Attach a PDF'),
            style: OutlinedButton.styleFrom(minimumSize: const Size.fromHeight(48)),
          ),
        ],
      );
    }

    final problem = !picked.hasAllowedType
        ? 'Only PDF, JPEG or PNG'
        : picked.isTooBig
            ? '${_megabytes(picked.byteSize)} — over the 10 MB limit'
            : null;

    return Card(
      child: ListTile(
        leading: Icon(
          problem == null ? Icons.description_outlined : Icons.error_outline,
          color: problem == null ? theme.colorScheme.primary : theme.colorScheme.error,
        ),
        title: Text(picked.name, maxLines: 1, overflow: TextOverflow.ellipsis),
        subtitle: Text(
          problem ?? _megabytes(picked.byteSize),
          style: problem == null ? null : TextStyle(color: theme.colorScheme.error),
        ),
        trailing: IconButton(
          icon: const Icon(Icons.close),
          tooltip: 'Remove',
          onPressed: enabled ? onRemove : null,
        ),
      ),
    );
  }

  static String _megabytes(int bytes) => '${(bytes / (1024 * 1024)).toStringAsFixed(1)} MB';
}
