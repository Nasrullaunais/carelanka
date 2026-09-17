import 'package:flutter/foundation.dart';

import '../../../core/network/api_exception.dart';
import '../../../core/widgets/async_data.dart';
import '../../../services/api_client/models/my_admission.dart';
import '../../../services/api_client/models/my_bill.dart';
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
  const MyStayCurrent(this.admission, {this.bill});
  final MyAdmission admission;

  /// Null when the billing desk has not raised one, which is most of a stay.
  final MyBill? bill;
}

class MyStayController extends ChangeNotifier {
  MyStayController(this._service);

  final PatientService _service;

  AsyncData<MyStay> _state = const AsyncData.loading();

  AsyncData<MyStay> get state => _state;

  Future<void> load({bool showLoading = true}) async {
    if (showLoading) {
      _state = const AsyncData.loading();
      notifyListeners();
    }

    try {
      final admission = await _service.loadMyAdmission();

      _state = AsyncData.ready(
        MyStayCurrent(admission, bill: await _loadBill(admission.admissionId)),
      );
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

  // The bill is an extra panel on the stay, not the stay itself. A stay that renders
  // without its bill beats a stay that fails to render because the bill would not load.
  Future<MyBill?> _loadBill(String admissionId) async {
    try {
      return await _service.loadMyBill(admissionId);
    } on ApiException {
      return null;
    }
  }
}
