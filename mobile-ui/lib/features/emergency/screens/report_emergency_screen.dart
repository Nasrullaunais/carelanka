import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../../core/theme/app_theme.dart';
import '../emergency_routes.dart';
import '../state/patient_emergency_controller.dart';

class ReportEmergencyScreen extends StatefulWidget {
  const ReportEmergencyScreen({super.key});

  @override
  State<ReportEmergencyScreen> createState() => _ReportEmergencyScreenState();
}

class _ReportEmergencyScreenState extends State<ReportEmergencyScreen> {
  final _details = TextEditingController();
  bool _forMe = true;
  Position? _position;
  String? _locationError;
  bool _locating = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<PatientEmergencyController>().load();
    });
  }

  @override
  void dispose() {
    _details.dispose();
    super.dispose();
  }

  Future<void> _locate() async {
    setState(() {
      _locating = true;
      _locationError = null;
    });
    try {
      if (!await Geolocator.isLocationServiceEnabled()) {
        throw const LocationServiceDisabledException();
      }
      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.denied ||
          permission == LocationPermission.deniedForever) {
        throw const PermissionDeniedException(
          'Location permission is required to send help.',
        );
      }
      final position = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.high,
        ),
      );
      if (mounted) setState(() => _position = position);
    } catch (_) {
      if (mounted) {
        setState(
          () => _locationError =
              'We could not get your location. Enable precise location access, then try again.',
        );
      }
    } finally {
      if (mounted) setState(() => _locating = false);
    }
  }

  Future<void> _submit() async {
    final position = _position;
    if (position == null) {
      await _locate();
      return;
    }
    final controller = context.read<PatientEmergencyController>();
    final id = await controller.report(
      patientIsCaller: _forMe,
      latitude: position.latitude,
      longitude: position.longitude,
      accuracy: position.accuracy,
      capturedAt: position.timestamp,
      details: _details.text,
    );
    if (mounted && id != null) {
      context.go('${EmergencyPaths.patientTracking}/$id');
    }
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<PatientEmergencyController>();
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
          if (_position != null)
            const ListTile(
              contentPadding: EdgeInsets.zero,
              leading: Icon(Icons.location_on_outlined),
              title: Text('Precise location captured'),
            )
          else
            OutlinedButton.icon(
              onPressed: _locating ? null : _locate,
              icon: const Icon(Icons.my_location),
              label: Text(
                _locating ? 'Finding location…' : 'Use my current location',
              ),
            ),
          if (_locationError != null) ...[
            const SizedBox(height: 8),
            Text(
              _locationError!,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
          ],
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
            onPressed: controller.acting ? null : _submit,
            icon: const Icon(Icons.emergency_outlined),
            label: Text(controller.acting ? 'Sending…' : 'Request ambulance'),
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(56),
            ),
          ),
        ],
      ),
    );
  }
}
