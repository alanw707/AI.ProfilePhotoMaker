# Shutdown follow-ups: manual steps left

The automated shutdown finished on 2026-10-21 (see [`SHUTDOWN-CHECKLIST.md`](SHUTDOWN-CHECKLIST.md)). These steps need a person in each dashboard. None of them is urgent except the keys: the apps that used them are gone, but the keys themselves still work until they're revoked.

## 1. Revoke the app's API keys (15 min)

The backend is deleted, so nothing uses these any more.

- [ ] **OpenAI**: https://platform.openai.com/api-keys → delete the key used by the app.
- [ ] **Replicate**: https://replicate.com/account/api-tokens → revoke the app's token. (All four app-created models are already gone.)
- [ ] **Stripe**: https://dashboard.stripe.com/apikeys → roll the live secret key. Keep the account for future projects. Webhooks are already deleted, and there are no active subscriptions or products.
- [ ] **Google Cloud**: https://console.cloud.google.com/apis/credentials → delete the OAuth client for `aiprofilephotomaker.com`.
- [ ] **Postmark**: https://account.postmarkapp.com/servers → pause or delete the server. No closing email was sent (decided 2026-10-21).
- [ ] **Cloudflare Turnstile**: Cloudflare dashboard → Turnstile → delete the widget.
- [ ] **USAJOBS** (optional): the API key was only used by the hidden Jobs page.

## 2. DNS clean-up at Cloudflare (2 min)

- [ ] Delete the `api.aiprofilephotomaker.com` CNAME. It points at `aipm-api-v1.bravehill-124f6a57.eastus2.azurecontainerapps.io`, which no longer exists. The takeover risk is low (the address belongs to this subscription's Container Apps environment), but a record pointing at nothing should go.
- [ ] Delete the `asuid.api` TXT record if there is one.
- [ ] **Keep** the records for `aiprofilephotomaker.com` itself (and its `asuid` TXT): the project page and its auto-renewing certificate depend on them. Keep `www` (it redirects to the apex) and the Cloudflare MX records.

## 3. Keep the domain (5 min)

- [ ] GoDaddy → My Products → Domains → `aiprofilephotomaker.com` → **Auto-renew: On**. It's paid through 2027-05-19. If it lapses, someone else can buy it and your LinkedIn link would point to their site.
- [ ] Optional: transfer the registration to Cloudflare Registrar (at cost, about $10/year).

## 4. Forward the site's email addresses (5 min)

The privacy policy and legal pages listed `privacy@`, `support@`, `legal@` and `security@`. MX already points at Cloudflare Email Routing.

- [ ] Cloudflare → `aiprofilephotomaker.com` → Email → Email Routing: make sure a catch-all (or one rule per address) forwards to your own inbox, and keep it for at least 12 months.
- [ ] Send a test to `privacy@aiprofilephotomaker.com`.

## 5. LinkedIn (2 min)

- [ ] Change the entry to past tense with an end date, for example a Projects entry: "AI Profile Photo Maker, 2025–2026". The link can stay as it is.

## 6. Watch the bill (2 min now, 1 min in 3 days)

- [ ] Azure portal → Cost Management → Budgets → Add: $5/month on the subscription, alert at 100% actual. Note that `walfordgroup-rg` (about $40/month) shares this subscription, so either scope the budget to resource group `aiprofilemaker-v1` or set the amount above the walfordgroup baseline.
- [ ] In 2–3 days, Cost analysis grouped by resource group should show about $0/day for `aiprofilemaker-v1`.

## 7. Data you're holding

- [ ] `~/.local/state/aipm-shutdown/backup/aipmdb-2026-10-21.bacpac` (unencrypted, mode 600) has user emails and payment records. Keep it off the repo and off shared drives. Decide on a deletion date, for example 2027-10, unless tax records require longer.
- [ ] `~/.local/state/aipm-shutdown/user-emails.txt` is no longer needed (no closing email was sent): `shred -u ~/.local/state/aipm-shutdown/user-emails.txt`.

## 8. Repo (optional)

- [ ] The repo is public and the project page links to it. Scan history for old leaks: `docker run --rm -v "$PWD":/r zricethezav/gitleaks detect -s /r`. Revoking the keys in step 1 makes any old leak harmless.
- [ ] PR #454 (funnel report) can be closed. The deploy workflow is disabled and no secrets remain, so nothing would deploy anyway.
