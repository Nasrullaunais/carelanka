import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'core/auth/auth_controller.dart';
import 'core/auth/session_expiry.dart';
import 'core/auth/token_store.dart';
import 'core/network/api.dart';
import 'core/push/device_registrar.dart';
import 'core/push/push_gateway.dart';
import 'core/push/push_registration.dart';
import 'core/routing/app_router.dart';
import 'core/theme/app_theme.dart';
import 'features/equipment/equipment_routes.dart';
import 'features/patient/patient_routes.dart';
import 'features/emergency/emergency_routes.dart';
import 'services/api_client/care_lanka_api.dart';
import 'services/api_client/models/current_principal.dart';
import 'services/api_client/models/principal_role.dart';

const _noScreensPath = '/not-built-yet';

/// Each member plugs their feature in by adding one entry here and one to
/// [_homePathFor]. Nothing else in this file should need to change.
final List<RouteBase> _featureRoutes = [
  ...emergencyRoutes,
  ...equipmentRoutes,
  ...patientRoutes,
];

String _homePathFor(CurrentPrincipal principal) =>
    emergencyHomePathFor(principal.role) ??
    patientHomePathFor(principal.role) ??
    equipmentHomePathFor(principal.role) ??
    _noScreensPath;

/// Root widget of the CareLanka mobile app.
class CareLankaApp extends StatefulWidget {
  const CareLankaApp({super.key, this.push});

  final PushGateway? push;

  @override
  State<CareLankaApp> createState() => _CareLankaAppState();
}

class _CareLankaAppState extends State<CareLankaApp> {
  late final TokenStore _tokens;
  late final SessionExpiry _sessionExpiry;
  late final CareLankaApi _api;
  late final Dio _dio;
  late final AuthController _auth;
  late final GoRouter _router;
  PushRegistration? _pushRegistration;

  @override
  void initState() {
    super.initState();
    _tokens = TokenStore();
    _sessionExpiry = SessionExpiry();
    final built = buildApi(tokens: _tokens, sessionExpiry: _sessionExpiry);
    _api = built.api;
    _dio = built.dio;
    _auth = AuthController(
      api: _api,
      tokens: _tokens,
      sessionExpiry: _sessionExpiry,
      beforeSignOut: () async => _pushRegistration?.unregister(),
    );

    // Built once. The router watches _auth itself through refreshListenable, so
    // rebuilding it on every auth change would throw away the navigation stack.
    _router = createAppRouter(
      auth: _auth,
      routes: [
        ..._featureRoutes,
        GoRoute(path: _noScreensPath, builder: (_, __) => const _NoScreensYet()),
      ],
      homePathFor: _homePathFor,
    );

    final push = widget.push;
    if (push != null) {
      _pushRegistration = PushRegistration(
        gateway: push,
        registrar: GeneratedDeviceRegistrar(_api),
        auth: _auth,
        onNotificationOpened: _openMyRun,
      )..start();
    }

    _auth.restore();
  }

  // Only the crew see My run; anyone else who taps a stray alert stays where they are.
  void _openMyRun() {
    if (_auth.principal?.role == PrincipalRole.ambulanceCrew) _router.go(EmergencyPaths.myRun);
  }

  @override
  void dispose() {
    _pushRegistration?.stop();
    _auth.dispose();
    _sessionExpiry.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        Provider<CareLankaApi>.value(value: _api),
        Provider<Dio>.value(value: _dio),
        ChangeNotifierProvider<AuthController>.value(value: _auth),
      ],
      child: MaterialApp.router(
        title: 'CareLanka',
        theme: AppTheme.light,
        darkTheme: AppTheme.dark,
        routerConfig: _router,
      ),
    );
  }
}

class _NoScreensYet extends StatelessWidget {
  const _NoScreensYet();

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthController>();
    return Scaffold(
      appBar: AppBar(
        title: const Text('CareLanka'),
        actions: [
          IconButton(
            icon: const Icon(Icons.logout),
            tooltip: 'Sign out',
            onPressed: auth.signOut,
          ),
        ],
      ),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text(
            'No mobile screens have been built for the '
            '${auth.principal?.role.json ?? 'unknown'} role yet.',
            textAlign: TextAlign.center,
          ),
        ),
      ),
    );
  }
}
