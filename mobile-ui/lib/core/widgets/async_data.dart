import '../network/api_exception.dart';

sealed class AsyncData<T> {
  const AsyncData();

  const factory AsyncData.loading() = AsyncLoading<T>;
  const factory AsyncData.ready(T value) = AsyncReady<T>;
  const factory AsyncData.failed(ApiException error) = AsyncFailed<T>;

  T? get valueOrNull => switch (this) {
        AsyncReady<T>(:final value) => value,
        _ => null,
      };
}

final class AsyncLoading<T> extends AsyncData<T> {
  const AsyncLoading();
}

final class AsyncReady<T> extends AsyncData<T> {
  const AsyncReady(this.value);
  final T value;
}

final class AsyncFailed<T> extends AsyncData<T> {
  const AsyncFailed(this.error);
  final ApiException error;
}
