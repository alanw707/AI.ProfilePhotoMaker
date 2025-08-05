# API Reference

## Overview

The AI Profile Photo Maker API is a RESTful API built with .NET 8 that provides endpoints for authentication, image processing, style selection, credit management, and more. All endpoints return JSON responses and require JWT authentication unless otherwise specified.

## Base URL

- Development: `http://localhost:5035/api`
- Ngrok: `https://awlocaldev-api.ngrok.app/api`
- Production: `https://api.aiprofilephotomaker.com/api`

## Authentication

Most endpoints require authentication via JWT bearer token:

```
Authorization: Bearer <your-jwt-token>
```

## Common Response Format

### Success Response
```json
{
  "success": true,
  "data": { ... },
  "message": "Operation completed successfully"
}
```

### Error Response
```json
{
  "success": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "Human readable error message",
    "details": { ... }
  }
}
```

## Endpoints

### Authentication

#### Register New User
```http
POST /auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "firstName": "John",
  "lastName": "Doe"
}
```

**Response:**
```json
{
  "token": "eyJ...",
  "user": {
    "id": "user-id",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe"
  }
}
```

#### Login
```http
POST /auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePass123!"
}
```

#### OAuth Login
```http
POST /auth/external-login
Content-Type: application/json

{
  "provider": "Google|Facebook|Apple",
  "idToken": "oauth-provider-token"
}
```

#### Get User Info
```http
GET /auth/user-info
Authorization: Bearer <token>
```

### User Profile

#### Get Profile
```http
GET /profile
Authorization: Bearer <token>
```

**Response:**
```json
{
  "id": "user-id",
  "email": "user@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "profileImageUrl": "/uploads/profile.jpg",
  "purchasedCredits": 100,
  "weeklyFreeCreditsUsed": 2,
  "totalCredits": 101,
  "hasTrainedModel": true,
  "modelStatus": "completed"
}
```

#### Update Profile
```http
PUT /profile
Authorization: Bearer <token>
Content-Type: application/json

{
  "firstName": "John",
  "lastName": "Smith",
  "phoneNumber": "+1234567890"
}
```

### Image Upload

#### Upload Selfies
```http
POST /image/upload-selfies
Authorization: Bearer <token>
Content-Type: multipart/form-data

files: [binary data]
```

**Response:**
```json
{
  "uploadedCount": 5,
  "totalSelfies": 10,
  "uploadedFiles": [
    {
      "id": "image-id",
      "url": "/uploads/user-id/image.jpg",
      "uploadedAt": "2025-01-14T10:30:00Z"
    }
  ]
}
```

#### Delete Selfie
```http
DELETE /image/selfie/{imageId}
Authorization: Bearer <token>
```

### Model Training

#### Start Training
```http
POST /image/train-model
Authorization: Bearer <token>
```

**Response:**
```json
{
  "trainingId": "replicate-training-id",
  "status": "starting",
  "estimatedTime": 1800,
  "message": "Model training started"
}
```

#### Check Training Status
```http
GET /model-creation-status
Authorization: Bearer <token>
```

**Response:**
```json
{
  "status": "training|completed|failed",
  "progress": 75,
  "trainedModelUrl": "https://...",
  "completedAt": "2025-01-14T11:00:00Z"
}
```

### Style Selection

#### Get Available Styles
```http
GET /style
Authorization: Bearer <token>
```

**Response:**
```json
[
  {
    "id": 1,
    "name": "linkedin",
    "displayName": "LinkedIn Professional",
    "description": "Perfect for professional networking",
    "previewUrl": "/style-previews/linkedin.jpg"
  },
  ...
]
```

#### Select User Styles
```http
POST /style/select
Authorization: Bearer <token>
Content-Type: application/json

{
  "styleIds": [1, 3, 5, 7]
}
```

#### Get User Selected Styles
```http
GET /style/user-selections
Authorization: Bearer <token>
```

### Image Generation

#### Generate Images
```http
POST /image/generate
Authorization: Bearer <token>
Content-Type: application/json

{
  "styleIds": [1, 3, 5]
}
```

**Response:**
```json
{
  "generationId": "gen-id",
  "stylesQueued": 3,
  "estimatedCredits": 30,
  "message": "Image generation started"
}
```

#### Enhance Photo
```http
POST /image/enhance
Authorization: Bearer <token>
Content-Type: multipart/form-data

file: [binary data]
```

**Response:**
```json
{
  "enhancedImageUrl": "/enhanced/user-id/image.jpg",
  "creditsUsed": 1,
  "remainingCredits": 99
}
```

### Gallery Management

#### Get Gallery Images
```http
GET /image/gallery?page=1&pageSize=12&styles=linkedin,corporate&startDate=2025-01-01
Authorization: Bearer <token>
```

**Query Parameters:**
- `page` (number): Page number (default: 1)
- `pageSize` (number): Items per page (default: 12)
- `styles` (string): Comma-separated style names
- `startDate` (date): Filter by generation date start
- `endDate` (date): Filter by generation date end
- `imageType` (string): selfie|generated|enhanced

**Response:**
```json
{
  "items": [
    {
      "id": 123,
      "imageUrl": "/generated/user-id/linkedin_123.png",
      "styleName": "linkedin",
      "generatedAt": "2025-01-14T12:00:00Z",
      "imageType": "generated"
    }
  ],
  "totalItems": 50,
  "totalPages": 5,
  "currentPage": 1
}
```

#### Delete Image
```http
DELETE /image/{imageId}
Authorization: Bearer <token>
```

#### Download Image
```http
GET /image/download/{imageId}
Authorization: Bearer <token>
```

#### Reconcile Images
```http
POST /image/reconcile
Authorization: Bearer <token>
```

**Response:**
```json
{
  "filesystemImages": 40,
  "databaseImages": 35,
  "repairedCount": 5,
  "errors": []
}
```

### Credit Management

#### Get Credit Balance
```http
GET /credit/balance
Authorization: Bearer <token>
```

**Response:**
```json
{
  "purchasedCredits": 100,
  "freeCreditsRemaining": 1,
  "totalCredits": 101,
  "weeklyResetDate": "2025-01-20T00:00:00Z"
}
```

#### Get Credit Packages
```http
GET /credit/packages
```

**Response:**
```json
[
  {
    "id": 1,
    "name": "Starter Pack",
    "credits": 50,
    "price": 4.99,
    "pricePerCredit": 0.10
  },
  {
    "id": 2,
    "name": "Popular Pack",
    "credits": 200,
    "price": 14.99,
    "pricePerCredit": 0.075,
    "popular": true
  }
]
```

#### Purchase Credits
```http
POST /credit/purchase
Authorization: Bearer <token>
Content-Type: application/json

{
  "packageId": 2
}
```

**Response:**
```json
{
  "clientSecret": "pi_stripe_secret",
  "amount": 1499,
  "currency": "usd"
}
```

#### Simulate Purchase (Dev Only)
```http
POST /credit/simulate-purchase
Authorization: Bearer <token>
Content-Type: application/json

{
  "packageId": 2
}
```

### Webhooks

#### Replicate Webhook
```http
POST /replicate-webhook
X-Webhook-ID: webhook-id
X-Webhook-Timestamp: timestamp
X-Webhook-Signature: signature
Content-Type: application/json

{
  "id": "prediction-id",
  "status": "succeeded",
  "output": { ... }
}
```

#### Stripe Webhook
```http
POST /stripe-webhook
Stripe-Signature: stripe-signature
Content-Type: application/json

{
  "type": "payment_intent.succeeded",
  "data": { ... }
}
```

### Configuration

#### Get App Configuration
```http
GET /config/app-settings
```

**Response:**
```json
{
  "maxSelfies": 20,
  "weeklyFreeCredits": 3,
  "creditCosts": {
    "generation": 10,
    "enhancement": 1
  },
  "features": {
    "paymentEnabled": true,
    "enhancementEnabled": true
  }
}
```

### Debug Endpoints (Development Only)

#### Health Check
```http
GET /debug/health
```

#### Fix Generated Images
```http
POST /debug/fix-generated-images
Authorization: Bearer <token>
```

#### Clear Cache
```http
POST /debug/clear-cache
Authorization: Bearer <token>
```

## Error Codes

| Code | Description |
|------|-------------|
| `AUTH_INVALID_CREDENTIALS` | Invalid email or password |
| `AUTH_EMAIL_TAKEN` | Email already registered |
| `AUTH_TOKEN_EXPIRED` | JWT token has expired |
| `INSUFFICIENT_CREDITS` | Not enough credits for operation |
| `MAX_SELFIES_REACHED` | Upload limit exceeded |
| `TRAINING_IN_PROGRESS` | Model training already running |
| `STYLE_LIMIT_EXCEEDED` | Too many styles selected |
| `IMAGE_NOT_FOUND` | Requested image doesn't exist |
| `INVALID_IMAGE_FORMAT` | Unsupported image format |
| `PAYMENT_FAILED` | Payment processing error |

## Rate Limiting

API implements rate limiting per user:
- Authentication endpoints: 5 requests per minute
- Image upload: 10 requests per minute
- Image generation: 20 requests per hour
- General API: 100 requests per minute

## Pagination

Paginated endpoints support:
- `page`: Page number (1-based)
- `pageSize`: Items per page (max 100)

Response includes:
- `totalItems`: Total count
- `totalPages`: Total pages
- `currentPage`: Current page number

## File Upload Limits

- Maximum file size: 10MB per image
- Supported formats: JPG, JPEG, PNG, WebP
- Maximum selfies: 20 per user
- Minimum dimensions: 512x512 pixels

## Webhooks Security

### Replicate Webhook Validation
```csharp
var timestamp = Request.Headers["X-Webhook-Timestamp"];
var id = Request.Headers["X-Webhook-ID"];
var signature = Request.Headers["X-Webhook-Signature"];

var payload = $"{id}.{timestamp}.{body}";
var expected = HMAC-SHA256(payload, webhook_secret);

if (signature != expected) {
    return Unauthorized();
}
```

### Stripe Webhook Validation
Uses Stripe SDK for signature validation.

## Best Practices

1. **Authentication**
   - Store JWT tokens securely
   - Implement token refresh logic
   - Handle 401 responses gracefully

2. **Error Handling**
   - Check response status codes
   - Parse error messages for user display
   - Implement retry logic for transient failures

3. **File Uploads**
   - Validate files client-side first
   - Show upload progress
   - Handle network interruptions

4. **Webhooks**
   - Implement idempotency
   - Process asynchronously
   - Store webhook events for debugging