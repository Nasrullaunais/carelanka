import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'core/auth/auth_controller.dart';
import 'core/auth/session_expiry.dart';
import 'core/auth/token_store.dart';
import 'core/network/api.dart';
import 'core/notifications/inbox_controller.dart';
import 'core/notifications/inbox_screen.dart';
import 'core/notifications/inbox_service.dart';
import 'core/notifications/notification_route.dart';
import 'core/push/device_registrar.dart';
import 'core/push/push_gateway.dart';
import 'core/push/push_registration.dart';
import 'core/routing/app_router.dart';
import 'core/theme/app_theme.dart';
import 'features/emergency/emergency_routes.dart';
import 'features/equipment/equipment_routes.dart';
import 'features/patient/patient_routes.dart';
import 'features/staff/staff_routes.dart';
import 'services/api_client/care_lanka_api.dart';
import 'services/api_client/models/current_principal.dart';

const _noScreensPath = '/not-built-yet';

/// Each member plugs their feature in by adding one entry here and one to
/// [_homePathFor]. Nothing else in this file should need to change.
final List<RouteBase> _featureRoutes = [
  ...emergencyRoutes,
  ...equipmentRoutes,
  ...patientRoutes,
  ...staffRoutes,
  GoRoute(path: InboxPaths.home, builder: (_, __) => const InboxScreen()),
];

String _homePathFor(CurrentPrincipal principal) =>
    emergencyHomePathFor(principal.role) ??
    patientHomePathFor(principal.role) ??
    equipmentHomePathFor(principal.role) ??
    staffHomePathFor(principal.role) ??
    _noScreensPath;

/// Root widget of the CareLanka mobile app.
class CareLankaApp extends StatefulWidget {
  const CareLankaApp({super.key, this.push});

  final PushGateway? push;

  @override
  State<CareLankaApp> createState() => _CareLankaAppState();
}

class _CareLankaAppState extends State<CareLankaApp> with WidgetsBindingObserver {
  late final TokenStore _tokens;
  late final SessionExpiry _sessionExpiry;
  late final CareLankaApi _api;
  late final Dio _dio;
  late final AuthController _auth;
  late final InboxController _inbox;
  late final GoRouter _router;
  final _messengerKey = GlobalKey<ScaffoldMessengerState>();
  PushRegistration? _pushRegistration;

  @override
  void initState() {
    super.initState();
    _tokens = TokenStore();
    _sessionExpiry = SessionExpiry();
    final built = buildApi(tokens: _tokens, sessionExpiry: _sessionExpiry);
    _api = built.api;
    _dio = built.dio;
    _inbox = InboxController(GeneratedInboxService(_api));
    _auth = AuthController(
      api: _api,
      tokens: _tokens,
      sessionExpiry: _sessionExpiry,
      beforeSignOut: () async => _pushRegistration?.unregister(),
    );
    _auth.addListener(_onAuthChanged);

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
        onNotificationOpened: _openNotification,
        onForegroundMessage: _showForegroundBanner,
      )..start();
    }

    _auth.restore();
    WidgetsBinding.instance.addObserver(this);
  }

  // Push already makes new items arrive instantly - this just catches up on anything the
  // phone missed while asleep, the same way a reconnect does for the web bell.
  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed && _auth.status == AuthStatus.signedIn) {
      _inbox.refreshUnreadCount();
    }
  }

  void _onAuthChanged() {
    if (_auth.status == AuthStatus.signedIn) _inbox.refreshUnreadCount();
  }

  void _openNotification(PushNotificationEvent event) {
    final id = event.data['id'];
    if (id != null) _inbox.markRead(id);

    final route = routeForPushData(event.data);
    if (route != null) _router.go(route);
  }

  void _showForegroundBanner(PushNotificationEvent event) {
    _inbox.refreshUnreadCount();
    final title = event.title;
    if (title == null) return;

    final messenger = _messengerKey.currentState;
    if (messenger == null) return;

    messenger
      ..clearMaterialBanners()
      ..showMaterialBanner(MaterialBanner(
        content: Text([title, event.body].whereType<String>().join('\n')),
        actions: [
          TextButton(
            onPressed: messenger.hideCurrentMaterialBanner,
            child: const Text('Dismiss'),
          ),
        ],
      ));
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _pushRegistration?.stop();
    _auth.removeListener(_onAuthChanged);
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
        ChangeNotifierProvider<InboxController>.value(value: _inbox),
      ],
      child: MaterialApp.router(
        title: 'CareLanka',
        theme: AppTheme.light,
        darkTheme: AppTheme.dark,
        scaffoldMessengerKey: _messengerKey,
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
