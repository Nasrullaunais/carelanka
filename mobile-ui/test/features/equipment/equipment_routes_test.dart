import 'package:carelanka_mobile/features/equipment/equipment_routes.dart';
import 'package:carelanka_mobile/services/api_client/models/principal_role.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('equipmentHomePathFor', () {
    // NAVIGATION testing: this decides which screen a staff member lands on when
    // they open the Equipment section. Per the comment above it in
    // equipment_routes.dart - uploading lab reports is for the equipment manager,
    // confirming registered items is for the hospital administrator - so the two
    // roles must never be routed to each other's screen, and every other role must
    // get no route at all (nothing in the Equipment section is theirs).

    test('sends the equipment manager to lab reports', () {
      expect(equipmentHomePathFor(PrincipalRole.equipmentManager), EquipmentPaths.labReports);
    });

    test('sends the hospital administrator to equipment confirmation', () {
      expect(
        equipmentHomePathFor(PrincipalRole.hospitalAdministrator),
        EquipmentPaths.confirmation,
      );
    });

    test('gives every other role no Equipment-section route at all', () {
      expect(equipmentHomePathFor(PrincipalRole.wardNurse), isNull);
      expect(equipmentHomePathFor(PrincipalRole.doctor), isNull);
      expect(equipmentHomePathFor(PrincipalRole.ambulanceCrew), isNull);
      expect(equipmentHomePathFor(PrincipalRole.generalStaff), isNull);
      expect(equipmentHomePathFor(PrincipalRole.dutyManager), isNull);
    });
  });
}
