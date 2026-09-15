import 'package:flutter/foundation.dart';

import '../../services/api_client/care_lanka_api.dart';
import '../../services/api_client/models/auth_tokens.dart';
import '../../services/api_client/models/current_principal.dart';
import '../../services/api_client/models/patient_login_request.dart';
import '../../services/api_client/models/patient_register_request.dart';
import '../../services/api_client/models/principal_type.dart';
import '../../services/api_client/models/refresh_token_request.dart';
import '../../services/api_client/models/staff_login_request.dart';
import '../network/api.dart';
import '../network/api_exception.dart';
import 'session_expiry.dart';
import 'token_store.dart';

enum AuthStatus { restoring, signedOut, signedIn }

class AuthController extends ChangeNotifier {
  AuthController({
    required CareLankaApi api,
    required TokenStore tokens,
    required SessionExpiry sessionExpiry,
  })  : _api = api,
        _tokens = tokens,
        _sessionExpiry = sessionExpiry {
    _sessionExpiry.addListener(_onSessionExpired);
  }

  final CareLankaApi _api;
  final TokenStore _tokens;
  final SessionExpiry _sessionExpiry;

  AuthStatus _status = AuthStatus.restoring;
  CurrentPrincipal? _principal;
  ApiException? _lastError;

  AuthStatus get status => _status;
  CurrentPrincipal? get principal => _principal;
  ApiException? get lastError => _lastError;

  bool get isPatient => _principal?.principalType == PrincipalType.patient;
  bool get isStaff => _principal?.principalType == PrincipalType.staff;

  // A stored token the API rejects is treated as no session, not an error.
  Future<void> restore() async {
    final token = await _tokens.readAccessToken();
    if (token == null) {
      _set(AuthStatus.signedOut, null);
      return;
    }

    try {
      _set(AuthStatus.signedIn, await callApi(_api.auth.getCurrentUser));
    } on ApiException {
      await _tokens.clear();
      _set(AuthStatus.signedOut, null);
    }
  }

  Future<bool> signInAsStaff({required String email, required String password}) {
    return _signIn(() => _api.auth.login(
          body: StaffLoginRequest(email: email, password: password),
        ));
  }

  Future<bool> signInAsPatient({required String username, required String password}) {
    return _signIn(() => _api.auth.loginPatient(
          body: PatientLoginRequest(username: username, password: password),
        ));
  }

  Future<bool> registerAsPatient({
    required String username,
    required String password,
  }) {
    return _signIn(() => _api.auth.registerPatientAccount(
          body: PatientRegisterRequest(username: username, password: password),
        ));
  }

  Future<void> signOut() async {
    final refreshToken = await _tokens.readRefreshToken();
    if (refreshToken != null) {
      try {
        await callApi(() => _api.auth.logout(
              body: RefreshTokenRequest(refreshToken: refreshToken),
            ));
      } on ApiException {
        // Local session drops either way; a failed server revoke can't strand the user signed in.
      }
    }
    await _tokens.clear();
    _set(AuthStatus.signedOut, null);
  }

  Future<bool> _signIn(Future<AuthTokens> Function() request) async {
    _lastError = null;
    try {
      final tokens = await callApi(request);
      await _tokens.save(
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken,
      );
      _set(AuthStatus.signedIn, tokens.principal);
      return true;
    } on ApiException catch (error) {
      _lastError = error;
      notifyListeners();
      return false;
    }
  }

  void _onSessionExpired() {
    _set(AuthStatus.signedOut, null);
  }

  void _set(AuthStatus status, CurrentPrincipal? principal) {
    _status = status;
    _principal = principal;
    notifyListeners();
  }

  @override
  void dispose() {
    _sessionExpiry.removeListener(_onSessionExpired);
    super.dispose();
  }
}
