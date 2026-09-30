class FileOpenException implements Exception {
  const FileOpenException(this.message);

  final String message;

  @override
  String toString() => message;
}
