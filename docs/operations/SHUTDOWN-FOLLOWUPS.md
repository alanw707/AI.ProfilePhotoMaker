# Shutdown follow-ups: manual steps left

The automated shutdown finished on 2026-10-21 (see [`SHUTDOWN-CHECKLIST.md`](SHUTDOWN-CHECKLIST.md)). These steps need a person in each dashboard. None of them is urgent except the keys: the apps that used them are gone, but the keys themselves still work until they're revoked.

## 1. Revoke the app's API keys (15 min)

The backend is deleted, so nothing uses these any more.

- [ ] **OpenAI**: https://platform.openai.com/api-keys → delete the key used by the app.
- [ ] **Replicate**: https://replicate.com/account/api-tokens → revoke the app's token. (All four app-created models are already gone.)
- [ ] **Stripe**: https://dashboard.stripe.com/apikeys → roll the live secret key. Keep the account for future projects. Webhooks are already deleted, and there are no active subscriptions or products.
- [ ] **Google Cloud — do this first**: https://console.cloud.google.com/apis/credentials → delete **both** OAuth clients (client IDs starting `1169…` and `3319…`). Their client secrets (`GOCSPX-…`) are in the public git history and Google still accepted them on 2026-10-22. Deleting the clients is the fix; rewriting git history wouldn't help, because the repo is already public.
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

- [x] Budget `aipm-shutdown-watch`: $5/month on resource group `aiprofilemaker-v1` from 2026-11-01, emailing alanw707@hotmail.com at 80% actual, 100% actual and 100% forecast (set 2026-10-22).
- [ ] In 2–3 days, Cost analysis grouped by resource group should show about $0/day for `aiprofilemaker-v1`.

## 7. Data you're holding

- [ ] `~/.local/state/aipm-shutdown/backup/aipmdb-2026-10-21.bacpac` (unencrypted, mode 600, folder mode 700) has user emails and payment records. It's the only copy: put a second copy somewhere private (password manager attachment or encrypted cloud folder). Delete by 2027-10-31 unless tax records require longer (noted in `backup/README.txt`).
- [x] `user-emails.txt` shredded (2026-10-22).

## 8. Repo

- [x] Shutdown branch merged to `main` (PR #455). PRs #454 and #397 closed (2026-10-22).
- [x] History scanned with gitleaks (2,148 public commits, 145 hits, 2026-10-22). Results:
  - **Still valid:** the 2 Google OAuth client secrets. See step 1.
  - **Already dead:** the Replicate token (Replicate returns 401). The storage account key, SQL passwords, JWT and data-protection keys all belonged to deleted Azure resources. The JWTs had expired by 2024.
  - **Not secrets:** Stripe `pk_live` publishable key and the Turnstile site key (public by design), plus test and placeholder values.
  - If any of the old SQL passwords in history (for example the one starting `Vg7pK`) was reused anywhere else, change it there.
