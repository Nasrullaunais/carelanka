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
  });

  final PickedReport? attachment;
  final bool enabled;
  final VoidCallback onPhotograph;
  final VoidCallback onAttachPdf;
  final VoidCallback onRemove;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final picked = attachment;

    if (picked == null) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          FilledButton.tonalIcon(
            onPressed: enabled ? onPhotograph : null,
            icon: const Icon(Icons.photo_camera_outlined),
            label: const Text('Photograph the report'),
            style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
          ),
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: enabled ? onAttachPdf : null,
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
