import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../emergency_routes.dart';
import '../state/caller_location_controller.dart';
import '../state/patient_emergency_controller.dart';
import '../widgets/caller_location_card.dart';

class ReportEmergencyScreen extends StatefulWidget {
  const ReportEmergencyScreen({super.key});

  @override
  State<ReportEmergencyScreen> createState() => _ReportEmergencyScreenState();
}

class _ReportEmergencyScreenState extends State<ReportEmergencyScreen>
    with WidgetsBindingObserver {
  final _details = TextEditingController();
  bool _forMe = true;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      context.read<PatientEmergencyController>().load();
      context.read<CallerLocationController>().start();
    });
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    final location = context.read<CallerLocationController>();
    switch (state) {
      case AppLifecycleState.resumed:
        location.resume();
      case AppLifecycleState.hidden || AppLifecycleState.paused:
        location.pause();
      case AppLifecycleState.inactive || AppLifecycleState.detached:
        return;
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _details.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final fix = context.read<CallerLocationController>().fix;
    if (fix == null) return;
    final controller = context.read<PatientEmergencyController>();
    final id = await controller.report(
      patientIsCaller: _forMe,
      latitude: fix.latitude,
      longitude: fix.longitude,
      accuracy: fix.accuracyMetres,
      capturedAt: fix.capturedAt,
      details: _details.text,
    );
    if (mounted && id != null) {
      context.go('${EmergencyPaths.patientTracking}/$id');
    }
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<PatientEmergencyController>();
    final location = context.watch<CallerLocationController>();
    return Scaffold(
      appBar: AppBar(title: const Text('Request an ambulance')),
      body: ListView(
        padding: const EdgeInsets.all(AppTheme.gutter),
        children: [
          Text(
            'Is the ambulance for you?',
            style: Theme.of(context).textTheme.titleMedium,
          ),
          const SizedBox(height: 8),
          SegmentedButton<bool>(
            segments: const [
              ButtonSegment(value: true, label: Text('For me')),
              ButtonSegment(value: false, label: Text('For someone else')),
            ],
            selected: {_forMe},
            onSelectionChanged: (value) => setState(() => _forMe = value.first),
          ),
          if (!_forMe) ...[
            const SizedBox(height: 8),
            Text(
              'We send where this phone is. If you are not with the person, '
              'write their address below.',
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ],
          const SizedBox(height: 20),
          TextField(
            controller: _details,
            maxLength: 1000,
            minLines: 3,
            maxLines: 5,
            decoration: const InputDecoration(
              labelText: 'What happened? (optional)',
              hintText: 'Briefly describe what help is needed',
            ),
          ),
          const SizedBox(height: 12),
          const CallerLocationCard(),
          if (controller.error != null) ...[
            const SizedBox(height: 8),
            Text(
              controller.error!.message,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ],
          if (controller.calls.isNotEmpty) ...[
            const SizedBox(height: 20),
            Text(
              'Recent requests',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            ...controller.calls.map(
              (call) => Card(
                child: ListTile(
                  title: Text(
                    call.status == null
                        ? 'Ambulance request'
                        : call.status!.name.replaceAll('_', ' '),
                  ),
                  subtitle: call.createdAt == null
                      ? null
                      : Text(call.createdAt!.toLocal().toString()),
                  trailing: const Icon(Icons.chevron_right),
                  onTap: call.id == null
                      ? null
                      : () => context.go(
                          '${EmergencyPaths.patientTracking}/${call.id}',
                        ),
                ),
              ),
            ),
          ],
          const SizedBox(height: 20),
          FilledButton.icon(
            onPressed: controller.acting || !location.canSend ? null : _submit,
            icon: const Icon(Icons.emergency_outlined),
            label: Text(switch ((controller.acting, location.canSend)) {
              (true, _) => 'Sending…',
              (false, false) => 'Waiting for your location…',
              (false, true) => 'Request ambulance',
            }),
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(56),
            ),
          ),
        ],
      ),
    );
  }
}
