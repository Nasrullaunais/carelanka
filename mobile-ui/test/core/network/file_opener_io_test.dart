import 'package:carelanka_mobile/core/network/file_opener_io.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('safeFileName', () {
    // fileName comes from whoever uploaded the report, so it is untrusted:
    // only the last path segment may survive, so nothing can escape the
    // opened-files folder via `/`, `\` or `..`.

    test('keeps only the last segment of a forward-slash path', () {
      expect(safeFileName('../../x.pdf', 'application/pdf'), 'x.pdf');
    });

    test('keeps only the last segment of a Windows-style path', () {
      expect(safeFileName(r'C:\Users\a\r.pdf', 'application/pdf'), 'r.pdf');
    });

    test('falls back to report.<ext> for an empty name', () {
      expect(safeFileName('', 'application/pdf'), 'report.pdf');
      expect(safeFileName('', 'image/jpeg'), 'report.jpg');
      expect(safeFileName('', 'image/png'), 'report.png');
    });

    test('falls back to a bare "report" for an empty name of an unknown type', () {
      expect(safeFileName('', 'application/octet-stream'), 'report');
    });

    test('keeps a name that has no extension, as-is', () {
      expect(safeFileName('Lipid Profile', 'application/pdf'), 'Lipid Profile');
    });

    test('keeps spaces and swaps unsafe characters for underscores', () {
      expect(safeFileName('Report (final)*.pdf', 'application/pdf'), 'Report _final__.pdf');
    });

    test('swaps non-ASCII characters rather than failing outright', () {
      expect(safeFileName('résumé café.pdf', 'application/pdf'), 'r_sum_ caf_.pdf');
    });

    test('falls back when the whole name is just dots', () {
      expect(safeFileName('..', 'image/png'), 'report.png');
    });
  });
}
