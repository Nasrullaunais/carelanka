import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../services/api_client/models/current_principal.dart';
import '../auth/auth_controller.dart';
import '../auth/login_screen.dart';

class AppRoutes {
  const AppRoutes._();

  static const login = '/login';
  static const splash = '/';
}

/// The app's one router.
///
/// [routes] is where each member plugs their feature's routes in, and
/// [homePathFor] decides where a principal lands after signing in — both are
/// passed from `app.dart` so this file never needs to know which features
/// exist.
GoRouter createAppRouter({
  required AuthController auth,
  required List<RouteBase> routes,
  required String Function(CurrentPrincipal principal) homePathFor,
}) {
  return GoRouter(
    initialLocation: AppRoutes.splash,
    refreshListenable: auth,
    routes: [
      GoRoute(
        path: AppRoutes.splash,
        builder: (_, __) => const _SplashScreen(),
      ),
      GoRoute(
        path: AppRoutes.login,
        builder: (_, __) => const LoginScreen(),
      ),
      ...routes,
    ],
    redirect: (context, state) {
      final location = state.matchedLocation;

      switch (auth.status) {
        case AuthStatus.restoring:
          return location == AppRoutes.splash ? null : AppRoutes.splash;

        case AuthStatus.signedOut:
          return location == AppRoutes.login ? null : AppRoutes.login;

        case AuthStatus.signedIn:
          if (location == AppRoutes.login || location == AppRoutes.splash) {
            return homePathFor(auth.principal!);
          }
          return null;
      }
    },
    errorBuilder: (_, state) => Scaffold(
      body: Center(child: Text('No screen at ${state.uri}')),
    ),
  );
}

class _SplashScreen extends StatelessWidget {
  const _SplashScreen();

  @override
  Widget build(BuildContext context) {
    return const Scaffold(body: Center(child: CircularProgressIndicator()));
  }
}
