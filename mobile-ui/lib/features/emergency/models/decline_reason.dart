enum DeclineReason {
  vehicleProblem('Vehicle problem'),
  crewNotComplete('Crew not complete'),
  alreadyBusy('Already busy'),
  other('Other');

  const DeclineReason(this.label);

  final String label;

  bool get needsDetails => this == DeclineReason.other;
}
