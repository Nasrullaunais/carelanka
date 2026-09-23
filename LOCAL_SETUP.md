# Run CareLanka locally

## API and demo database in Docker

Install Docker with Compose, then run from the repository root:

```bash
# One time: create a private local signing key (the file is gitignored).
printf 'CARELANKA_JWT_SIGNING_KEY=%s\n' "$(openssl rand -hex 32)" > .env
chmod 600 .env
docker compose up -d --build
curl http://localhost:5231/api/health
```

Compose starts PostgreSQL, applies EF migrations, loads the demo SQL on the first run, and starts the .NET 8 API. Swagger is at http://localhost:5231/swagger. Later starts only need `docker compose up -d`. Use `docker compose logs -f api` to watch the server and `docker compose down` to stop it. `docker compose down -v` deletes the local demo database.

## Web UI

In a second terminal:

```bash
cd web-ui
npm ci
npm run dev
```

Open http://localhost:5174. Vite proxies `/api` to the API container through the host's port 5231. For a staff login, use `nurse.perera@carelanka.lk` and `CareLanka#2026`.

## Flutter UI on a USB connected Android phone

Enable USB debugging, connect the phone, and accept the debugging prompt. Keep the USB cable connected while using the app:

```bash
adb devices
adb reverse tcp:5231 tcp:5231
cd mobile-ui
flutter pub get
flutter run -d YOUR_DEVICE_ID --dart-define=API_BASE_URL=http://127.0.0.1:5231/api
```

Get `YOUR_DEVICE_ID` from `adb devices`. The `adb reverse` command routes the phone's port 5231 through USB to the API on this machine. Run it again after reconnecting the phone. The URL override is needed because Flutter's default Android address, `10.0.2.2`, is for an emulator. The debug Android manifest permits local HTTP. For a patient login, use `+94771234567` and `Patient#2026`.

To confirm the phone can reach the API before launching Flutter, open `http://127.0.0.1:5231/api/health` in the phone's browser after `adb reverse`. The response should report `"database":"up"`.
