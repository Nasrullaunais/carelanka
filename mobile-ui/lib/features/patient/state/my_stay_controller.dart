import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../services/patient_service.dart';

sealed class MyStay {
  const MyStay();
}

final class MyStayNotLinked extends MyStay {
  const MyStayNotLinked();
}

final class MyStayNoAdmission extends MyStay {
  const MyStayNoAdmission();
}

final class MyStayCurrent extends MyStay {
  const MyStayCurrent(this.admission);
  final MyAdmission admission;
}

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
      // Both arrive as 404 but mean different things — branch on the code, not the status.
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
