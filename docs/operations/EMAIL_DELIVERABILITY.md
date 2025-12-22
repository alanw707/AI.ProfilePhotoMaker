# Email Deliverability (Postmark + Outlook/Hotmail)

This guide documents the operational steps needed to improve deliverability of transactional emails to Outlook/Hotmail and reduce suspicious warnings in Gmail.

## Objectives

- Align SPF/DKIM/DMARC with the transactional sender domain.
- Use a dedicated transactional subdomain.
- Ensure plain-text parts are included in every transactional email.
- Route sends through the Postmark API for primary transactional delivery.

## Recommended Domain Setup

Use a dedicated transactional subdomain for sending:

- Sender: `no-reply@mail.aiprofilephotomaker.com`
- API base URLs stay unchanged.

## DNS Records

Create/verify the following records for the transactional subdomain.

### SPF (mail subdomain)

```
mail.aiprofilephotomaker.com TXT "v=spf1 include:spf.mtasv.net -all"
```

### DKIM (Postmark-provided CNAMEs)

Postmark will provide the DKIM selectors for the subdomain. Example (use the exact records Postmark gives you):

```
postmark1._domainkey.mail.aiprofilephotomaker.com CNAME <postmark-provided>
postmark2._domainkey.mail.aiprofilephotomaker.com CNAME <postmark-provided>
```

### DMARC (mail subdomain)

Start relaxed while validating, then tighten after Inbox placement stabilizes:

```
_dmarc.mail.aiprofilephotomaker.com TXT "v=DMARC1; p=none; adkim=r; aspf=r; rua=mailto:dmarc_rua@onsecureserver.net;"
```

After validation:

```
_dmarc.mail.aiprofilephotomaker.com TXT "v=DMARC1; p=quarantine; adkim=r; aspf=r; rua=mailto:dmarc_rua@onsecureserver.net;"
```

## Postmark Configuration (Primary)

1. Create a Postmark account and a Server (capture the Server API token).
2. Verify the sender domain `mail.aiprofilephotomaker.com`.
3. Add Postmark DKIM/SPF records in Cloudflare (DNS only).
4. Confirm a Message Stream (defaults to `outbound`).

## Application Configuration

- Set `Email__UseApi=true` and provide `Email__PostmarkServerToken` in production.
- Set `Email__FromEmail=no-reply@mail.aiprofilephotomaker.com`.
- Optionally set `Email__PostmarkMessageStream` (defaults to `outbound`).
- Keep `Email__FrontendBaseUrl` set to `https://app.aiprofilephotomaker.com`.

## Verification Checklist

- Send a verification email and a welcome email to:
  - Outlook/Hotmail
  - Gmail
- Confirm Inbox placement and no "suspicious" banner.
- Inspect headers:
  - SPF: pass
  - DKIM: pass
  - DMARC: pass
  - Authentication alignment with the From domain
- Confirm Postmark MessageID is logged on successful sends.

## Notes

- If root domain (`aiprofilephotomaker.com`) is used as the From domain, update SPF to include Postmark.
