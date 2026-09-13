import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../services/patient_service.dart';

/// What the patient's own-stay screen is showing.
sealed class MyStay {
  const MyStay();
}

/// The account has no hospital record behind it yet — the ordinary state for a
/// new signup, not an error.
final class MyStayNotLinked extends MyStay {
  const MyStayNotLinked();
}

/// Linked, but not currently admitted.
final class MyStayNoAdmission extends MyStay {
  const MyStayNoAdmission();
}

final class MyStayCurrent extends MyStay {
  const MyStayCurrent(this.admission);
  final MyAdmission admission;
}

/// The patient's own admission — read-only, and scoped by the `sub` claim, so
/// no patient id is ever sent.
class MyStayController extends ChangeNotifier {
  MyStayController(this._service);

  final PatientService _service;

  AsyncData<MyStay> _state = const AsyncData.loading();

  AsyncData<MyStay> get state => _state;

  Future<void> load() async {
    _state = const AsyncData.loading();
    notifyListeners();

    try {
      _state = AsyncData.ready(MyStayCurrent(await _service.loadMyAdmission()));
    } on ApiException catch (error) {
      // Both of these arrive as 404 and mean completely different things, so
      // the code decides and the status never does.
      _state = switch (error.code) {
        PatientService.notLinkedCode => const AsyncData.ready(MyStayNotLinked()),
        PatientService.noCurrentStayCode => const AsyncData.ready(MyStayNoAdmission()),
        _ when error.isNotFound => const AsyncData.ready(MyStayNoAdmission()),
        _ => AsyncData.failed(error),
      };
    }
    notifyListeners();
  }
}
