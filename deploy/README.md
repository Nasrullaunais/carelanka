# Hosting CareLanka on DigitalOcean

One small server runs everything: the database, the API, and the web app.
Caddy sits in front, gets a free `https://` certificate, and sends `/api`
requests to the API and everything else to the web app.

Cost: the 2 GB server is about $12 a month, billed by the hour, so a one-week
demo is about $3. Delete the server afterwards and billing stops.

## 1. Create the server

In DigitalOcean, **Create → Droplets**:

- **Region:** Bangalore (closest to Sri Lanka)
- **Image:** Ubuntu 24.04
- **Size:** Basic → Regular → **2 GB / 1 CPU**. 1 GB runs out of memory while building.
- **Authentication:** SSH key (add your laptop's public key)

Note the server's IP address, for example `203.0.113.5`.

## 2. Pick the web address

Write the IP with dashes and add `.sslip.io`:

```
203.0.113.5  →  203-0-113-5.sslip.io
```

No signup needed. If Caddy can't get a certificate for it (see step 6), make a
free `yourname.duckdns.org` at duckdns.org pointing at the IP and use that.

## 3. Set up the server

```sh
ssh root@203.0.113.5

# Extra memory on disk, so the build doesn't run out
fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
echo '/swapfile none swap sw 0 0' >> /etc/fstab

curl -fsSL https://get.docker.com | sh

git clone https://github.com/Nasrullaunais/carelanka.git
cd carelanka/deploy
```

## 4. Fill in the settings

```sh
cp .env.example .env
openssl rand -hex 32   # run twice: once for the database password, once for the sign-in key
nano .env
```

Fill in `SITE_ADDRESS`, the two random values and, if you want the AI agents to
use Gemini, `CARELANKA_GEMINI_API_KEY`.

## 5. Start it

```sh
docker compose up -d --build
```

The first build takes around 10 minutes. It sets up the database, loads the
demo data from `docs/seed/` and starts everything.

## 6. Check it

- Open `https://<SITE_ADDRESS>` and sign in with an account from `TEST_ACCOUNTS.md`.
- If the page doesn't load, look at Caddy's messages:
  `docker compose logs web`

## 7. Build the phone app

On your laptop:

```sh
cd mobile-ui
flutter build apk --release --dart-define=API_BASE_URL=https://<SITE_ADDRESS>/api
```

Share `build/app/outputs/flutter-apk/app-release.apk`.

## Updating after a change

```sh
cd carelanka && git pull && cd deploy && docker compose up -d --build
```

The demo data is only loaded once. To start from fresh demo data:

```sh
docker compose down
docker volume rm carelanka-hosting_carelanka_db
docker compose up -d
```

This keeps Caddy's certificate. Let's Encrypt only issues a few per week for
the same address, so avoid `down -v`.

## When the demo is over

Destroy the droplet in DigitalOcean. Everything on it is deleted and billing stops.
