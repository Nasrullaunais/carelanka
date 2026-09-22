import 'dart:async';

import 'package:carelanka_mobile/core/auth/auth_controller.dart';
import 'package:carelanka_mobile/core/auth/session_expiry.dart';
import 'package:carelanka_mobile/core/auth/token_store.dart';
import 'package:carelanka_mobile/core/network/api_exception.dart';
import 'package:carelanka_mobile/core/push/device_registrar.dart';
import 'package:carelanka_mobile/core/push/push_gateway.dart';
import 'package:carelanka_mobile/core/push/push_registration.dart';
import 'package:carelanka_mobile/services/api_client/care_lanka_api.dart';
import 'package:carelanka_mobile/services/api_client/models/current_principal.dart';
import 'package:carelanka_mobile/services/api_client/models/principal_role.dart';
import 'package:carelanka_mobile/services/api_client/models/principal_type.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

final class FakeGateway implements PushGateway {
  bool allowed = true;
  String? current = 'phone-token';
  final refreshes = StreamController<String>.broadcast();
  final taps = StreamController<void>.broadcast();

  @override
  Future<bool> requestPermission() async => allowed;

  @override
  Future<String?> token() async => current;

  @override
  Stream<String> get tokenRefreshes => refreshes.stream;

  @override
  Stream<void> get opened => taps.stream;
}

final class FakeRegistrar implements DeviceRegistrar {
  final registered = <String>[];
  final removed = <String>[];
  Object? nextError;

  @override
  Future<String> register(String token) async {
    final error = nextError;
    if (error != null) {
      nextError = null;
      throw error;
    }
    registered.add(token);
    return 'registration-${registered.length}';
  }

  @override
  Future<void> unregister(String id) async => removed.add(id);
}

final class FakeAuth extends AuthController {
  FakeAuth()
      : super(
          api: CareLankaApi(Dio()),
          tokens: TokenStore(),
          sessionExpiry: SessionExpiry(),
        );

  AuthStatus fakeStatus = AuthStatus.signedOut;
  CurrentPrincipal? fakePrincipal;

  @override
  AuthStatus get status => fakeStatus;

  @override
  CurrentPrincipal? get principal => fakePrincipal;

  @override
  bool get isStaff => fakePrincipal?.principalType == PrincipalType.staff;

  void signInAs(PrincipalType type) {
    fakeStatus = AuthStatus.signedIn;
    fakePrincipal = CurrentPrincipal(
      id: 'person-1',
      principalType: type,
      role: type == PrincipalType.staff ? PrincipalRole.ambulanceCrew : PrincipalRole.patient,
      displayName: 'Test',
    );
    notifyListeners();
  }

  void signOutNow() {
    fakeStatus = AuthStatus.signedOut;
    fakePrincipal = null;
    notifyListeners();
  }
}

void main() {
  late FakeGateway gateway;
  late FakeRegistrar registrar;
  late FakeAuth auth;
  late int opened;
  late PushRegistration registration;

  setUp(() {
    gateway = FakeGateway();
    registrar = FakeRegistrar();
    auth = FakeAuth();
    opened = 0;
    registration = PushRegistration(
      gateway: gateway,
      registrar: registrar,
      auth: auth,
      onNotificationOpened: () => opened++,
    )..start();
  });

  tearDown(() => registration.stop());

  Future<void> settle() => Future<void>.delayed(Duration.zero);

  test('a staff member signing in registers this phone once', () async {
    auth.signInAs(PrincipalType.staff);
    await settle();
    auth.signInAs(PrincipalType.staff);
    await settle();

    expect(registrar.registered, ['phone-token']);
  });

  test('a patient signing in registers nothing', () async {
    auth.signInAs(PrincipalType.patient);
    await settle();

    expect(registrar.registered, isEmpty);
  });

  test('without permission to show alerts nothing is registered', () async {
    gateway.allowed = false;
    auth.signInAs(PrincipalType.staff);
    await settle();

    expect(registrar.registered, isEmpty);
  });

  test('a new token from the push service is registered', () async {
    auth.signInAs(PrincipalType.staff);
    await settle();
    gateway.refreshes.add('fresh-token');
    await settle();

    expect(registrar.registered, ['phone-token', 'fresh-token']);
  });

  test('signing out stops pushes for the phone that was registered', () async {
    auth.signInAs(PrincipalType.staff);
    await settle();
    await registration.unregister();
    await registration.unregister();

    expect(registrar.removed, ['registration-1']);
  });

  test('a server error on registering is swallowed and the app carries on', () async {
    registrar.nextError = const ApiException(statusCode: 503, message: 'down');
    auth.signInAs(PrincipalType.staff);
    await settle();

    expect(registrar.registered, isEmpty);
  });

  test('opening a notification tells the app to show the run', () async {
    gateway.taps.add(null);
    await settle();

    expect(opened, 1);
  });
}
