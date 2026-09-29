import '../../../services/api_client/models/scene_outcome.dart';

extension SceneOutcomeCrew on SceneOutcome {
  String get crewLabel => switch (this) {
    SceneOutcome.treatedAtScene => 'Treated at the scene',
    SceneOutcome.patientRefused => 'Patient refused to go',
    SceneOutcome.patientNotFound => 'Patient not found at the location',
    SceneOutcome.falseAlarm => 'False alarm',
    SceneOutcome.patientDeceased => 'Patient died at the scene',
    SceneOutcome.$unknown => 'Unknown',
  };
}
