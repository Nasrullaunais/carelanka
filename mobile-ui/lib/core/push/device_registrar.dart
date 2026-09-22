import '../../services/api_client/care_lanka_api.dart';
import '../../services/api_client/models/device_platform.dart';
import '../../services/api_client/models/register_device_request.dart';
import '../network/api.dart';

abstract interface class DeviceRegistrar {
  Future<String> register(String token);
  Future<void> unregister(String id);
}

final class GeneratedDeviceRegistrar implements DeviceRegistrar {
  GeneratedDeviceRegistrar(CareLankaApi api) : _api = api;

  final CareLankaApi _api;

  @override
  Future<String> register(String token) async {
    final registration = await callApi(() => _api.deviceTokens.registerDevice(
          body: RegisterDeviceRequest(token: token, platform: DevicePlatform.android),
        ));
    return registration.id!;
  }

  @override
  Future<void> unregister(String id) => callApi(() => _api.deviceTokens.unregisterDevice(id: id));
}
