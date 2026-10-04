import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../services/api_client/care_lanka_api.dart';
import '../emergency_routes.dart';
import '../models/patient_call_text.dart';
import '../services/patient_emergency_service.dart';
import '../state/patient_emergency_controller.dart';

/// The patient home entry point. Points back to a request that is still open
/// instead of offering a fresh one, so a worried caller does not send two.
class AmbulanceRequestCard extends StatelessWidget {
  const AmbulanceRequestCard({super.key});

  @override
  Widget build(BuildContext context) => ChangeNotifierProvider(
    create: (context) => PatientEmergencyController(
      context.read<PatientEmergencyService?>() ??
          GeneratedPatientEmergencyService(context.read<CareLankaApi>()),
    )..load(),
    child: const _AmbulanceRequestTile(),
  );
}

class _AmbulanceRequestTile extends StatefulWidget {
  const _AmbulanceRequestTile();

  @override
  State<_AmbulanceRequestTile> createState() => _AmbulanceRequestTileState();
}

class _AmbulanceRequestTileState extends State<_AmbulanceRequestTile>
    with WidgetsBindingObserver {
  GoRouter? _router;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
  }

  // A request sent or cancelled on another screen must show here as soon as
  // the patient comes back, however they got back.
  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final router = GoRouter.maybeOf(context);
    if (router == _router) return;
    _router?.routerDelegate.removeListener(_onNavigation);
    _router = router?..routerDelegate.addListener(_onNavigation);
  }

  void _onNavigation() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && ModalRoute.of(context)?.isCurrent == true) {
        context.read<PatientEmergencyController>().load();
      }
    });
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      context.read<PatientEmergencyController>().load();
    }
  }

  @override
  void dispose() {
    _router?.routerDelegate.removeListener(_onNavigation);
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final open = context.watch<PatientEmergencyController>().openCall;
    final openId = open?.id;
    final (title, subtitle, path) = openId == null
        ? (
            'Request ambulance',
            'Share your location',
            EmergencyPaths.patientReport,
          )
        : (
            callStatusLabel(open!.status),
            'Your request is open. Tap to follow it.',
            '${EmergencyPaths.patientTracking}/$openId',
          );

    return Material(
      color: scheme.errorContainer,
      borderRadius: BorderRadius.circular(22),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.push(path),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Row(
            children: [
              Container(
                width: 46,
                height: 46,
                decoration: BoxDecoration(
                  color: scheme.error,
                  shape: BoxShape.circle,
                ),
                child: Icon(
                  openId == null
                      ? Icons.emergency_outlined
                      : Icons.local_shipping_outlined,
                  color: scheme.onError,
                ),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: theme.textTheme.titleMedium?.copyWith(
                        color: scheme.onErrorContainer,
                      ),
                    ),
                    Text(
                      subtitle,
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: scheme.onErrorContainer.withValues(alpha: 0.8),
                      ),
                    ),
                  ],
                ),
              ),
              Icon(Icons.chevron_right_rounded, color: scheme.onErrorContainer),
            ],
          ),
        ),
      ),
    );
  }
}
