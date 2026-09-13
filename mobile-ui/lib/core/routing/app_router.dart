import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../services/api_client/models/current_principal.dart';
import '../auth/auth_controller.dart';
import '../auth/patient_login_screen.dart';
import '../auth/patient_register_screen.dart';
import '../auth/staff_login_screen.dart';
import '../auth/welcome_screen.dart';

class AppRoutes {
  const AppRoutes._();

  static const splash = '/';
  static const welcome = '/welcome';
  static const register = '/register';
  static const patientLogin = '/sign-in';
  static const staffLogin = '/staff/sign-in';

  /// Reachable without a session. Anything else bounces to [welcome].
  static const signedOut = {welcome, register, patientLogin, staffLogin};
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
      GoRoute(path: AppRoutes.splash, builder: (_, __) => const _SplashScreen()),
      GoRoute(path: AppRoutes.welcome, builder: (_, __) => const WelcomeScreen()),
      GoRoute(path: AppRoutes.register, builder: (_, __) => const PatientRegisterScreen()),
      GoRoute(path: AppRoutes.patientLogin, builder: (_, __) => const PatientLoginScreen()),
      GoRoute(path: AppRoutes.staffLogin, builder: (_, __) => const StaffLoginScreen()),
      ...routes,
    ],
    redirect: (context, state) {
      final location = state.matchedLocation;

      switch (auth.status) {
        case AuthStatus.restoring:
          return location == AppRoutes.splash ? null : AppRoutes.splash;

        case AuthStatus.signedOut:
          // Signing up and signing in are separate screens, so this cannot pin
          // the user to one path the way a single login screen could.
          return AppRoutes.signedOut.contains(location) ? null : AppRoutes.welcome;

        case AuthStatus.signedIn:
          final onSignedOutScreen =
              AppRoutes.signedOut.contains(location) || location == AppRoutes.splash;
          return onSignedOutScreen ? homePathFor(auth.principal!) : null;
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
