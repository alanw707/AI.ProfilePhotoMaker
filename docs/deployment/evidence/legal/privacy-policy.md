# Privacy Policy (Draft)

Last updated: 2025-12-23  
Status: Signed off (legal). Aligns to PRD v1.2 and current API retention behavior.

## Evidence (production)
- URL: https://aiprofilephotomaker.com/legal/privacy
- Screenshot: docs/deployment/evidence/legal/privacy-policy-production.png
- Captured: 2025-12-23T20:35:36Z

## 1. Scope
This Privacy Policy explains how AI Profile Photo Maker ("we", "us", "our") collects, uses, shares, and retains personal information when you use our website and services.

## 2. Information We Collect
We collect the following categories of information:
- Account information: email address, name, profile attributes you provide (e.g., gender, ethnicity).
- Photos and outputs: uploaded photos, generated images, and enhancement results.
- Usage and activity: credit usage, feature usage, timestamps, and actions.
- Device and technical data: IP address, browser type, and basic diagnostics.
- Payment data: payment method details are processed by Stripe; we receive limited transaction metadata (e.g., payment status, receipt ID).
- Support communications: messages you send to support.

## 3. How We Use Information
We use your information to:
- Provide the service (upload, instant headshot generation, optional training, generation, enhancement, and delivery of results).
- Authenticate users and secure accounts.
- Process payments and manage credits.
- Maintain service reliability, security, and fraud prevention.
- Comply with legal obligations and enforce terms.
- Improve product performance and user experience.

## 4. Legal Bases (EEA/UK)
If you are in the EEA/UK, we process personal data under one or more of these legal bases:
- Contract: to provide the service you request.
- Consent: to process photos and, where required by law, biometric data.
- Legitimate interests: to secure and improve the service.
- Legal obligation: to meet compliance requirements.

## 5. AI Processing and Biometric Information
We process user photos to create AI-generated profile images. In some jurisdictions, facial photos may be considered biometric information. We process such data only to provide the service at your direction and, where required, based on consent.

## 6. Sharing and Third Parties
We share data with service providers that help us run the service, including:
- AI model providers (OpenAI for instant headshot generation and select enhancements; Replicate for optional/advanced custom model training and styled generation).
- Payment processor (Stripe).
- OAuth provider (Google, if you sign in with Google).
- Hosting and storage providers (local storage or cloud storage, if configured).

See `docs/deployment/evidence/legal/subprocessors.md` for the current list.

We do not sell personal information or share it for cross-context behavioral advertising.

## 7. Retention
We retain data as follows (target behavior per PRD and current API policy):
- Input photos: retained up to 30 days after upload, unless you delete earlier.
- Generated images: retained up to 30 days after creation, unless you delete earlier.
- Model artifacts and training files: retained while your headshot images remain available; deleted after those images expire or if you delete the model or account.
- Account and billing records: retained as needed to provide the service, comply with law, and resolve disputes.

Backups may retain data for a limited period. If cloud storage soft-delete is enabled, deleted blobs may be retained for up to 7 days.

Third-party providers may retain inputs or outputs as needed to provide their services or meet legal and security obligations. Their retention practices are governed by their own policies.

## 8. Your Rights and Choices
Depending on your location, you may have rights to:
- Access, correct, or delete your data.
- Export your data (current export includes metadata and usage records, not photo files).
- Object to or restrict certain processing.

You can delete photos, models, or your account in **Settings**. For other requests, contact us at `privacy@aiprofilephotomaker.com`.

## 9. International Transfers
We may process data in countries where we or our service providers operate. When required, we use appropriate safeguards for international transfers.

## 10. Security
We use technical and organizational measures to protect data. No system is 100% secure, and we cannot guarantee absolute security.

## 11. Children's Privacy
Our service is not directed to children under 13. If we learn that we have collected personal information from a child under 13, we will delete it.

## 12. Changes
We may update this policy. If changes are material, we will provide notice in the app or on our website.

## 13. Contact
Questions about privacy? Email `privacy@aiprofilephotomaker.com`.
