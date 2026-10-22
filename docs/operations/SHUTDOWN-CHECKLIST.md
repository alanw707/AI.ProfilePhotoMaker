# Shutdown checklist: AI Profile Photo Maker

Goal: stop the spend, close the site cleanly, and handle user data properly. The domain keeps serving a static project page (it's linked from LinkedIn), and the repo stays.

> **Status, 2026-10-21: done.** Steps 0–7 were carried out. The database backup is at `~/.local/state/aipm-shutdown/backup/` (unencrypted, mode 600, by choice). The 4 Replicate models were already deleted by the app's retention job. `aiprofilephotomaker.com` now serves `scripts/shutdown/showcase/` from the web Container App (step 6). Nothing else in `aiprofilemaker-v1` remains. The manual leftovers are in [`SHUTDOWN-FOLLOWUPS.md`](SHUTDOWN-FOLLOWUPS.md).
>
> Measured spend before the shutdown (30 days to 2026-10-21): about $41 for this app, plus about $40 for an unrelated VM in `walfordgroup-rg`, which was left alone. Expected spend after: about $0–1/month (the web app scales to zero).

**Automated:** `scripts/shutdown/shutdown-wizard.sh` walks through every step below. It runs the API work itself (GitHub, Azure, Stripe, Replicate, Cloudflare, Postmark), opens the right page for each manual step, and asks before anything that deletes data. Try `DRY_RUN=1 scripts/shutdown/shutdown-wizard.sh` first. Needs the Azure CLI (falls back to a Docker container if it isn't installed), Docker (for the database queries), `gh`, `jq` and `gpg`. State and the backup go to `~/.local/state/aipm-shutdown/`, never the repo. The project page is `scripts/shutdown/showcase/index.html`, published by `scripts/shutdown/publish-showcase.sh`.

Work top to bottom. Order matters: step 1 stops the deploy pipeline from recreating everything, and steps 4–6 have to happen before step 7.

Run `az` commands locally (Azure CLI is installed via `mise use -g azure-cli`) or in **Azure Cloud Shell** (portal, top bar `>_`).

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

## 6. Keep the domain: serve the project page from the existing web app (10 min)

DNS is on Cloudflare, and the registrar is GoDaddy (paid through 2027-05-19, per an RDAP lookup on 2026-10-20). The domain is linked from LinkedIn, so it shows a project page instead of a bare "closed" notice.

The page is served by the **existing** `aipm-web-v1` Container App, so the `aiprofilephotomaker.com` custom domain, its Azure-managed certificate (which auto-renews) and the Cloudflare DNS records all stay as they are. No DNS change is needed.

- [ ] Edit `scripts/shutdown/showcase/index.html` if needed. Rules: no sign-in or upload buttons and no prices, so nobody thinks the service still runs.
- [ ] Preview it: `LOCAL=1 scripts/shutdown/publish-showcase.sh` (serves on http://localhost:18080).
- [ ] Publish it: `scripts/shutdown/publish-showcase.sh`. This rewrites `aipm-web-v1` to run Microsoft's public nginx image (`mcr.microsoft.com/azurelinux/base/nginx:1.25`) with the site files mounted from Container App secrets: no registry, no env vars, 0.25 vCPU, scale to zero. Then it checks `https://aiprofilephotomaker.com` and every image. Re-run it after any page edit.
- [ ] Domain renewal, email forwarding and LinkedIn: see [`SHUTDOWN-FOLLOWUPS.md`](SHUTDOWN-FOLLOWUPS.md).

Why not a custom image on GHCR? The available GitHub token can't publish packages, and GHCR has no API for making a package public. Mounting the files as secrets avoids any registry and any runtime dependency besides MCR.

## 7. Delete everything else in Azure (10 min + waiting) — can't be undone

Check that step 4 (backup downloaded), step 5 (Replicate cleaned) and step 6 (page live) are done first. Don't delete the resource group: the web app, its environment and the domain certificate stay.

```bash
RG=aiprofilemaker-v1; ENV=aipm-env-v1-6j74jubocuukg
az containerapp env update -g $RG -n $ENV --logs-destination none   # detach Log Analytics first
az containerapp delete -g $RG -n aipm-api-v1 --yes
az containerapp job delete -g $RG -n aipm-migration-job --yes
az containerapp env certificate delete -g $RG -n $ENV --certificate mc-aiprofilemaker-api-aiprofilepho-8101 --yes
az sql server delete -g $RG -n aipm-sql-v1-6j74jubocuukg --yes
az storage account delete -g $RG -n aipmstv16j74jubocuukg --yes      # user photos go with it
az acr delete -g $RG -n aipmcrv16j74jubocuukg --yes
az keyvault delete -g $RG -n aipm-kv-v1-6j74jubocuukg && az keyvault purge -n aipm-kv-v1-6j74jubocuukg
az resource delete -g $RG -n aipm-ai-v1 --resource-type Microsoft.Insights/components
az monitor log-analytics workspace delete -g $RG -n aipm-logs-v1 --force true --yes
# action group, activity-log alert, dashboard, managed identity (names can contain spaces):
az resource list -g $RG --query "[?type=='microsoft.insights/actiongroups' || type=='Microsoft.Insights/activityLogAlerts' || type=='Microsoft.Portal/dashboards' || type=='Microsoft.ManagedIdentity/userAssignedIdentities'].id" -o tsv \
  | while IFS= read -r id; do az resource delete --ids "$id"; done

# Expect only: managedEnvironments, containerApps (aipm-web-v1), managedCertificates (aiprofilephotomaker.com)
az resource list -g $RG --query '[].[type,name]' -o tsv
```

- [ ] Delete the Entra app registration GitHub used to deploy (OIDC), **`ai-profile-photo-maker-deploy`**. First check that its federated credentials only reference `repo:alanw707/AI.ProfilePhotoMaker:…`. It held Contributor and User Access Administrator on the whole subscription, so remove those role assignments too: `az role assignment list --assignee <appId> --all`.
- [ ] Leave every other resource group alone (`walfordgroup-rg` is a separate, still-used project; `NetworkWatcherRG` is free).

## 8. Revoke the remaining keys (10 min)

The services are gone now, so these are just cleanup.

- [ ] **OpenAI**: delete the API key the app used (platform.openai.com → API keys).
- [ ] **Stripe**: roll the secret key, or leave the account dormant. Don't delete it if you might reuse it for the next idea.
- [ ] **Google Cloud**: delete the OAuth client (APIs & Services → Credentials).
- [ ] **Postmark**: pause or delete the server after the closing email.
- [ ] **Cloudflare Turnstile**: delete the widget.
- [ ] **GitHub**: remove all Actions secrets and variables (`gh secret list` / `gh variable list`, then `delete`), including any on the `staging` environment.

## 9. Confirm the spend stopped (2–3 days later)

- [ ] Azure Cost Management: daily cost should be $0. If the subscription isn't used for anything else, also set a **$5 budget alert** so you'd notice anything that gets recreated.
- [ ] OpenAI, Replicate: usage pages show no new spend.
- [ ] `https://aiprofilephotomaker.com` shows the project page; `https://api.aiprofilephotomaker.com` no longer serves the app (remove its DNS record, see follow-ups).

## 10. Keep the repo

- [ ] Leave it as is. Reusable parts for the next project include sign-in (Google OAuth + JWT), Stripe checkout and webhooks with idempotency, the OpenAI integration with cost caps, the Bicep and GitHub Actions deploy, and the accessibility and test setup.
- [x] Add a line to `README.md`: "Closed October 2026. Kept for reference."
- [ ] The repo is **already public**, and the showcase page links to it. Scan the git history for leaked secrets (for example `gitleaks detect`). Revoking every key in step 8 makes any old leak harmless either way.
