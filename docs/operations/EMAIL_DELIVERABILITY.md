# Email Deliverability (Brevo + Outlook/Hotmail)

This guide documents the operational steps needed to improve deliverability of transactional emails to Outlook/Hotmail and reduce suspicious warnings in Gmail.

## Objectives

- Align SPF/DKIM/DMARC with the transactional sender domain.
- Use a dedicated transactional subdomain and custom tracking domain.
- Ensure plain-text parts are included in every transactional email.
- Route sends through the Brevo API with optional dedicated IP.

## Recommended Domain Setup

Use a dedicated transactional subdomain for sending:

- Sender: `no-reply@mail.aiprofilephotomaker.com`
- Tracking domain: `links.aiprofilephotomaker.com`
- API base URLs stay unchanged.

## DNS Records

Create/verify the following records for the transactional subdomain.

### SPF (mail subdomain)

```
mail.aiprofilephotomaker.com TXT "v=spf1 include:spf.brevo.com -all"
```

### DKIM (Brevo-provided CNAMEs)

Brevo will provide the DKIM selectors for the subdomain. Example:

```
brevo1._domainkey.mail.aiprofilephotomaker.com CNAME b1.aiprofilephotomaker-com.dkim.brevo.com
brevo2._domainkey.mail.aiprofilephotomaker.com CNAME b2.aiprofilephotomaker-com.dkim.brevo.com
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

## Brevo Configuration

1. Verify the transactional subdomain in Brevo.
2. Set the sender (From address) to `no-reply@mail.aiprofilephotomaker.com`.
3. Configure a custom tracking domain (`links.aiprofilephotomaker.com`).
4. If using a dedicated IP, configure it in Brevo and set `Email__DedicatedIp` in the API.

## Application Configuration

- Set `Email__UseApi=true` and provide `Email__ApiKey` in production.
- Set `Email__FromEmail=no-reply@mail.aiprofilephotomaker.com`.
- Optionally set `Email__DedicatedIp` to enable Brevo dedicated IP sending.
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
- Confirm Brevo messageId is logged on successful sends.

## Notes

- If root domain (`aiprofilephotomaker.com`) is used as the From domain, update SPF to include Brevo.
- Avoid shared tracking domains; custom tracking domains improve reputation and alignment.
