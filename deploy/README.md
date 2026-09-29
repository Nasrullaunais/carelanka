# Hosting CareLanka on Azure

One small server runs everything: the database, the API, and the web app.
Caddy sits in front, gets a free `https://` certificate, and sends `/api` and
`/swagger` to the API and everything else to the web app.

**The server never builds anything.** GitHub builds the images; the server only
downloads and runs them.

```
push to main
  → CI checks: API build + tests, web app, phone app, API contract
  → only if all pass: build 4 images, push to ghcr.io
  → deploy: copy compose.yaml to the server, download the new images, restart
  → check https://<server>/api/health answers
```

A pull request runs the checks and builds the images, but publishes and deploys nothing.

## What runs on the server

| Service | Image | What it does |
| :--- | :--- | :--- |
| `db` | `postgres:16` | The database. Data lives in the `carelanka_db` volume |
| `migrate` | `carelanka-migrate` | Updates the tables to the latest migration, then exits |
| `db-setup` | `carelanka-db-setup` | Gives the API its limited database login, loads the demo data the first time only, then exits |
| `api` | `carelanka-api` | The ASP.NET API. Logs in as `carelanka_app`, which can read and write rows but cannot change or drop tables |
| `web` | `carelanka-web` | Caddy plus the built React app |

Every deploy runs `migrate` and `db-setup` again. Both are safe to repeat.

---

## One-time setup

### 1. The server

In the Azure portal, create a virtual machine:

- **Region:** one your subscription allows. Student subscriptions only allow some regions;
  a `RequestDisallowedByAzure` error means pick another. Malaysia West works.
- **Image:** Ubuntu Server 24.04 LTS, **x64**
- **Size:** 2 vCPU / 4 GB, e.g. `Standard_B2als_v2`. Not Spot — Azure can switch Spot off mid-demo.
- **Authentication:** SSH public key, username `azureuser`. Download the `.pem`; Azure won't offer it again.
- **Inbound ports:** SSH (22), HTTP (80), HTTPS (443). GitHub deploys over SSH, so 22 stays open.
- **Disks:** Standard SSD is enough.

Then on the VM's **Overview**, set a **DNS name label** (e.g. `carelanka-demo`). The address
becomes `carelanka-demo.<region>.cloudapp.azure.com` and survives stopping the VM.

### 2. Docker

```sh
ssh -i carelanka-vm_key.pem azureuser@<address>

sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab

curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker azureuser
exit
```

### 3. The settings file

Log in again, then:

```sh
mkdir -p ~/carelanka && cd ~/carelanka
nano .env
```

Paste the contents of `deploy/.env.example` and fill it in. Make each password with
`openssl rand -hex 32`. `SITE_ADDRESS` is the address from step 1.

This file is the only thing you create on the server. CI copies `compose.yaml` in on every deploy.

### 4. A key for GitHub to log in with

On your laptop, make a key used only for deploying:

```sh
ssh-keygen -t ed25519 -f ~/.ssh/carelanka-deploy -N "" -C "github-actions-deploy"
```

Let it log in to the server. `restrict` blocks everything a deploy doesn't need,
such as port forwarding:

```sh
echo "restrict $(cat ~/.ssh/carelanka-deploy.pub)" \
  | ssh -i carelanka-vm_key.pem azureuser@<address> 'cat >> ~/.ssh/authorized_keys'
```

### 5. Tell GitHub about the server

Deploy settings live in a GitHub **environment** called `production`, limited to the `main` branch:

```sh
gh api -X PUT repos/Nasrullaunais/carelanka/environments/production --input - <<'EOF'
{"deployment_branch_policy": {"protected_branches": false, "custom_branch_policies": true}}
EOF
gh api -X POST repos/Nasrullaunais/carelanka/environments/production/deployment-branch-policies -f name=main

gh variable set DEPLOY_HOST --env production --body "<address>"
gh variable set DEPLOY_USER --env production --body "azureuser"
ssh-keygen -F <address> | grep -v '^#' | gh variable set DEPLOY_KNOWN_HOSTS --env production
gh secret set DEPLOY_SSH_KEY --env production < ~/.ssh/carelanka-deploy
```

`DEPLOY_KNOWN_HOSTS` is the server's fingerprint from your own first login, so GitHub refuses
to deploy to anything pretending to be the server.

### 6. Make the images public

The first push to `main` creates four packages on GitHub, private by default. The first deploy
fails with `denied` until they are public:

GitHub → your profile → **Packages** → for each of `carelanka-api`, `carelanka-migrate`,
`carelanka-db-setup`, `carelanka-web`: **Package settings → Change visibility → Public**.

Then re-run the failed **Deploy to Azure** job. The images hold no passwords — those stay in `.env`.

---

## Day to day

**Deploying:** merge to `main`. Watch it under the repository's **Actions** tab.

**Deploying again without a code change:** Actions → **CI** → **Run workflow** on `main`.

**Going back to an earlier version:** every image is labelled with its commit. On the server:

```sh
cd ~/carelanka
CARELANKA_IMAGE_TAG=sha-<full commit hash> docker compose up -d
```

Or revert the commit on `main`, which deploys the reverted code the normal way.
Neither undoes a migration: the tables stay at the newer version.

**Looking at problems:**

```sh
cd ~/carelanka
docker compose ps -a              # what is running, what exited
docker compose logs api           # API errors
docker compose logs web           # certificate or page problems
docker compose logs migrate db-setup
```

**Starting from fresh demo data:**

```sh
cd ~/carelanka
docker compose down
docker volume rm carelanka-hosting_carelanka_db
docker compose up -d
```

This keeps Caddy's certificate. Let's Encrypt only issues a few per week for the same
address, so avoid `down -v`.

**Saving credit:** in the portal, **Stop** the VM and check it says **Stopped (deallocated)**.
Everything comes back on **Start**. A deploy while it is stopped fails; re-run it after starting.

## The phone app

```sh
cd mobile-ui
flutter build apk --release --dart-define=API_BASE_URL=https://<address>/api
```

Share `build/app/outputs/flutter-apk/app-release.apk`.

## When the project is over

Delete the resource group in the Azure portal. Everything in it goes, and billing stops.
