final class HandoverDraft {
  const HandoverDraft({this.patientCondition = '', this.notes = ''});

  static const empty = HandoverDraft();

  final String patientCondition;
  final String notes;

  bool get isEmpty => patientCondition.trim().isEmpty && notes.trim().isEmpty;

  String? get patientConditionOrNull => _blankToNull(patientCondition);

  String? get notesOrNull => _blankToNull(notes);

  static String? _blankToNull(String text) =>
      text.trim().isEmpty ? null : text.trim();
}
