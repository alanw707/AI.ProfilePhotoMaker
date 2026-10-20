# Shutdown checklist: AI Profile Photo Maker

Goal: stop the roughly $100/month spend, close the site cleanly, and handle user data properly. The repo stays.

**Automated:** `scripts/shutdown/shutdown-wizard.sh` walks through every step below. It runs the API work itself (GitHub, Azure, Stripe, Replicate, Cloudflare, Postmark), opens the right page for each manual step, and asks before anything that deletes data. Try `DRY_RUN=1 scripts/shutdown/shutdown-wizard.sh` first. Needs Docker (the Azure CLI runs in a container), `gh`, `jq`, `gpg` and `npx`. State and the encrypted backup go to `~/.local/state/aipm-shutdown/`, never the repo. The project page it publishes is `scripts/shutdown/showcase/index.html`.

Work top to bottom. Order matters: step 1 stops the deploy pipeline from recreating everything, and step 5 has to happen before step 7.

Run `az` commands in **Azure Cloud Shell** (portal, top bar `>_`). The Azure CLI isn't installed on this machine.

What's running (from `infrastructure/simple-deploy.bicep`), all in resource group **`aiprofilemaker-v1`**:

| Resource | Name pattern | Notes |
|---|---|---|
| Container Apps environment | `aipm-env-v1-6j74jubocuukg` | Holds both apps and the managed TLS certs |
| API container app | `aipm-api-v1` | Always on (`minReplicas: 1`), likely the biggest cost |
| Web container app | `aipm-web-v1` | |
| Container registry (Basic) | `aipmcrv1…` | |
| SQL server + DB (Basic) | `aipm-sql-v1-…` / `aipmdb` | User accounts, credits, payments, career data |
| Storage account | `aipmstv1…` | Container `profile-images`: users' face photos |
| Key Vault | `aipm-kv-v1-…` | Soft delete, 7 days |
| Log Analytics + App Insights | `aipm-logs-v1`, `aipm-ai-v1` | |
| Managed identity | `aipm-api-identity-v1-…` | |

Outside Azure: Replicate (trained per-user models), OpenAI, Stripe, Google sign-in, Postmark, Cloudflare (DNS + Turnstile), USAJOBS, GitHub Actions.

---

## 0. Before you start (5 min)

- [ ] Azure portal → **Cost Management → Cost analysis**, grouped by resource. Note last month's total so you can confirm it drops later.
- [ ] Check whether anyone has paid, using Stripe dashboard → **Payments** and **Customers**:
  - If nobody has paid, skip the refund part of step 2.
  - If someone has unused credits, decide whether to refund them (recommended: refund anyone who bought in the last ~60 days).
- [ ] Stripe → **Subscriptions**: confirm there are **no active subscriptions**. Cancel any that exist so nobody gets billed after the site is gone.

## 1. Stop the deploy pipeline (2 min) — do this first

`.github/workflows/simple-deploy.yml` runs on every push to `main` and **creates the resource group if it's missing**. If it stays on, it will rebuild everything.

- [ ] GitHub → repo → **Actions → 🚀 Simple Deploy → ⋯ → Disable workflow**
  or: `gh workflow disable "🚀 Simple Deploy (Local Build to MVP environment)" -R alanw707/AI.ProfilePhotoMaker`
- [ ] Optional: close or leave open PR #454 (the funnel report). It doesn't need merging if you're shutting down.

## 2. Tell users (15 min, optional if you have no real users)

- [ ] Send a short email through Postmark to everyone with an account, saying:
  - the site closes on <date>,
  - all photos, trained models and account data will be deleted,
  - refunds (if any) are being issued,
  - a contact email for questions.
- [ ] Issue any refunds in Stripe.

## 3. Stop new sign-ups and purchases (5 min)

- [ ] Stripe → **Developers → Webhooks**: delete the endpoint pointing at `api.aiprofilephotomaker.com`.
- [ ] Stripe → **Product catalog**: archive the $9.99 / $19.99 packages and deactivate any payment links.

## 4. Back up the database (15–20 min)

The database has user emails and payment records, so keep the backup **encrypted and private**, never in the git repo.

- [ ] Portal → SQL database `aipmdb` → **Export** → BACPAC, saved to the existing storage account in a new container named `backup`.
- [ ] Download the `.bacpac` to your machine (Storage browser in the portal → `backup` → Download).
- [ ] Store it encrypted, for example an encrypted archive in your password manager or cloud drive.
- [ ] Decide how long you keep it and write the date down, for example delete after 12 months unless there's a tax or legal reason to keep payment records.

**Photos: recommend not backing them up.** The `profile-images` container holds people's face photos. The published retention policy says photos are deleted after 30 days, and the biometric-consent terms (BIPA and similar laws) expect biometric data to be destroyed once the purpose ends. Deleting it with the resource group is the policy-consistent choice. Only copy the sample or marketing images you made yourself.

## 5. Delete trained models at Replicate (10 min)

Each user's training creates a model at Replicate under your account (`CreateModelTrainingWithDestinationAsync`). These are derived from face photos and **won't be deleted** when you delete Azure.

- [ ] replicate.com → **Models**: delete every model created by the app. Keep only models you made yourself.
- [ ] Replicate → **Account → API tokens**: revoke the token the app used.
- [ ] Remove any saved payment method or spending limit you don't need any more.

## 6. Turn the domain into a project showcase page and update DNS (30 min)

DNS is on Cloudflare. The registrar is GoDaddy, and the domain is paid through 2027-05-19 (RDAP lookup, 2026-10-20). The domain is linked from LinkedIn, so it should show a project page, not a bare "closed" notice.

- [ ] Create a one-page site on **Cloudflare Pages** (free) at `aiprofilephotomaker.com` (and `www` if used). It needs:
  - what the app did, plus 3–4 screenshots,
  - the tech stack and the hard parts you solved,
  - dates ("built and ran 2025–2026") and what you learned,
  - a link to the GitHub repo,
  - one short line saying the service is closed and all user photos and data have been deleted, with a contact email.
- [ ] Make sure the page has no sign-in or upload buttons and no prices, so nobody thinks the service still runs.
- [ ] Delete the DNS record for `api.aiprofilephotomaker.com` and any Azure validation records (`asuid.*` TXT).
- [ ] Keep the Postmark DKIM/SPF records only until the closing email in step 2 has gone out, then remove them.
- [ ] **Keep the domain and set it to auto-renew** (about $10–25/year). If it lapses, someone else can buy it, and then your LinkedIn link points to their site.
- [ ] Optional: move registration to Cloudflare Registrar, which charges at cost (about $10/year) and keeps DNS and billing in one place.
- [ ] Set up **Cloudflare Email Routing** (free) to forward `privacy@`, `support@`, `legal@` and `security@` to your own inbox for at least 12 months. Former users may still send data requests to these addresses.
- [ ] On LinkedIn, change the entry to past tense with an end date (for example a Projects entry: "AI Profile Photo Maker, 2025–2026"). The link can stay as it is.

## 7. Delete the Azure resources (5 min + waiting) — can't be undone

Check that step 4 (backup downloaded) and step 5 (Replicate cleaned) are done first.

```bash
# Look first
az resource list -g aiprofilemaker-v1 -o table

# Delete everything in the group
az group delete -n aiprofilemaker-v1 --yes

# Confirm it's gone (should say ResourceGroupNotFound)
az group show -n aiprofilemaker-v1
```

- [ ] Key Vault: soft-deleted for 7 days and then purged automatically, at no cost. To purge now: `az keyvault list-deleted -o table`, then `az keyvault purge -n <name>`.
- [ ] Look for anything outside that group: `az group list -o table`. A leftover `NetworkWatcherRG` is free. Delete anything else that belongs to this app.
- [ ] Delete the app registration GitHub used to sign in (OIDC): **Entra ID → App registrations** → the one whose client ID matches the `AZURE_CLIENT_ID` GitHub secret.

## 8. Revoke the remaining keys (10 min)

The services are gone now, so these are just cleanup.

- [ ] **OpenAI**: delete the API key the app used (platform.openai.com → API keys).
- [ ] **Stripe**: roll the secret key, or leave the account dormant. Don't delete it if you might reuse it for the next idea.
- [ ] **Google Cloud**: delete the OAuth client (APIs & Services → Credentials).
- [ ] **Postmark**: pause or delete the server after the closing email.
- [ ] **Cloudflare Turnstile**: delete the widget.
- [ ] **GitHub**: remove the Actions secrets (`AZURE_*`, Stripe, OpenAI, Replicate and so on) under repo → Settings → Secrets and variables.

## 9. Confirm the spend stopped (2–3 days later)

- [ ] Azure Cost Management: daily cost should be $0. If the subscription isn't used for anything else, also set a **$5 budget alert** so you'd notice anything that gets recreated.
- [ ] OpenAI, Replicate: usage pages show no new spend.
- [ ] `https://aiprofilephotomaker.com` shows the closed page; `https://api.aiprofilephotomaker.com` doesn't resolve.

## 10. Keep the repo

- [ ] Leave it as is. Reusable parts for the next project include sign-in (Google OAuth + JWT), Stripe checkout and webhooks with idempotency, the OpenAI integration with cost caps, the Bicep and GitHub Actions deploy, and the accessibility and test setup.
- [ ] Add a line to `README.md`: "Closed <date>. Kept for reference."
- [ ] The repo is **already public**, and the showcase page links to it. Scan the git history for leaked secrets (for example `gitleaks detect`). Revoking every key in step 8 makes any old leak harmless either way.
