# Age Gate (Implementation Notes)

Last updated: 2025-12-19  
Status: Evidence captured (production); legal review pending.

## Evidence (production)
- URL: https://app.aiprofilephotomaker.com/auth/register
- Screenshot: docs/deployment/evidence/compliance/age-gate-register-production.png
- URL: https://app.aiprofilephotomaker.com/auth/login
- Screenshot: docs/deployment/evidence/compliance/age-gate-login-production.png
- Captured: 2025-12-23T20:53:46Z

## Where Age Gate Is Enforced
- Email registration form requires confirmation of age 13+.
- Google OAuth login is disabled until age confirmation is checked.
- API registration and profile completion endpoints reject requests without age confirmation.

## Behavior
- Registration form cannot submit unless the 13+ checkbox is checked.
- Google sign-in button is disabled until the user confirms 13+.
- API rejects register and complete-profile requests when age confirmation is false or missing.

## Code References
- `AI.ProfilePhotoMaker.UI/src/app/auth/register/register.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/auth/register/register.component.html`
- `AI.ProfilePhotoMaker.UI/src/app/auth/login/login.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/auth/login/login.component.html`
- `AI.ProfilePhotoMaker.UI/src/app/auth/complete-profile/complete-profile.component.ts`
- `AI.ProfilePhotoMaker.UI/src/app/auth/complete-profile/complete-profile.component.html`
- `AI.ProfilePhotoMaker.API/Models/DTOs/RegisterDto.cs`
- `AI.ProfilePhotoMaker.API/Controllers/AuthController.cs`
