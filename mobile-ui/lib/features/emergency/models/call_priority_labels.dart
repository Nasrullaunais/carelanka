import '../../../services/api_client/models/call_priority.dart';

extension CallPriorityCrew on CallPriority {
  String get crewLabel => switch (this) {
    CallPriority.critical => 'Critical',
    CallPriority.high => 'High',
    CallPriority.medium => 'Medium',
    CallPriority.low => 'Low',
    CallPriority.$unknown => 'Unknown',
  };
}
