import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'core/auth/auth_controller.dart';
import 'core/auth/session_expiry.dart';
import 'core/auth/token_store.dart';
import 'core/network/api.dart';
import 'core/routing/app_router.dart';
import 'core/theme/app_theme.dart';
import 'features/equipment/equipment_routes.dart';
import 'features/patient/patient_routes.dart';
import 'services/api_client/care_lanka_api.dart';
import 'services/api_client/models/current_principal.dart';

const _noScreensPath = '/not-built-yet';

/// Each member plugs their feature in by adding one entry here and one to
/// [_homePathFor]. Nothing else in this file should need to change.
final List<RouteBase> _featureRoutes = [
  ...equipmentRoutes,
  ...patientRoutes,
];

String _homePathFor(CurrentPrincipal principal) =>
    patientHomePathFor(principal.role) ??
    equipmentHomePathFor(principal.role) ??
    _noScreensPath;

/// Root widget of the CareLanka mobile app.
class CareLankaApp extends StatefulWidget {
  const CareLankaApp({super.key});

  @override
  State<CareLankaApp> createState() => _CareLankaAppState();
}

class _CareLankaAppState extends State<CareLankaApp> {
  late final TokenStore _tokens;
  late final SessionExpiry _sessionExpiry;
  late final CareLankaApi _api;
  late final AuthController _auth;
  late final GoRouter _router;

  @override
  void initState() {
    super.initState();
    _tokens = TokenStore();
    _sessionExpiry = SessionExpiry();
    _api = buildApi(tokens: _tokens, sessionExpiry: _sessionExpiry);
    _auth = AuthController(api: _api, tokens: _tokens, sessionExpiry: _sessionExpiry);

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

    _auth.restore();
  }

  @override
  void dispose() {
    _auth.dispose();
    _sessionExpiry.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        Provider<CareLankaApi>.value(value: _api),
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
