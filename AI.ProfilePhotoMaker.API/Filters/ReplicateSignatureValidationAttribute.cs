using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Cryptography;
using System.Text;

namespace AI.ProfilePhotoMaker.API.Filters;

/// <summary>
/// Action filter to validate Replicate webhook signatures without interfering with model binding
/// </summary>
public class ReplicateSignatureValidationAttribute : Attribute, IAsyncAuthorizationFilter
{
    private const int MaxTimestampAgeSeconds = 300; // 5 minutes

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<ReplicateSignatureValidationAttribute>>();
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();

        try
        {
            var request = context.HttpContext.Request;
            var secret = configuration["Replicate:WebhookSecret"];

            // If no secret is configured, skip validation (useful for development)
            if (string.IsNullOrEmpty(secret))
            {
                logger.LogWarning("Webhook secret not configured - skipping signature validation.");
                return;
            }

            // 1. Get the required headers - Replicate uses standard webhook headers
            var webhookId = request.Headers["webhook-id"].FirstOrDefault();
            var webhookTimestamp = request.Headers["webhook-timestamp"].FirstOrDefault();
            var webhookSignature = request.Headers["webhook-signature"].FirstOrDefault();

            logger.LogInformation("DEBUG: webhook-id: {WebhookId}", webhookId);
            logger.LogInformation("DEBUG: webhook-timestamp: {WebhookTimestamp}", webhookTimestamp);
            logger.LogInformation("DEBUG: webhook-signature: {WebhookSignature}", webhookSignature);

            if (string.IsNullOrEmpty(webhookSignature))
            {
                logger.LogWarning("Missing 'webhook-signature' header.");
                context.Result = new UnauthorizedResult();
                return;
            }

            if (string.IsNullOrEmpty(webhookId))
            {
                logger.LogWarning("Missing 'webhook-id' header.");
                context.Result = new UnauthorizedResult();
                return;
            }

            if (string.IsNullOrEmpty(webhookTimestamp))
            {
                logger.LogWarning("Missing 'webhook-timestamp' header.");
                context.Result = new UnauthorizedResult();
                return;
            }

            // 2. Parse signatures from webhook-signature header (format: "v1,base64sig1 v1,base64sig2")
            var signatures = new List<string>();
            var signatureParts = webhookSignature.Split(' ');
            
            foreach (var part in signatureParts)
            {
                if (part.StartsWith("v1,"))
                {
                    signatures.Add(part.Substring(3)); // Remove "v1," prefix
                }
            }

            if (!signatures.Any())
            {
                logger.LogWarning("Invalid signature header format. No v1 signatures found in header: {WebhookSignature}", webhookSignature);
                context.Result = new UnauthorizedResult();
                return;
            }

            // 3. Check the timestamp to protect against replay attacks
            if (!long.TryParse(webhookTimestamp, out var timestampSeconds))
            {
                logger.LogWarning("Invalid webhook timestamp format. Timestamp: {Timestamp}", webhookTimestamp);
                context.Result = new UnauthorizedResult();
                return;
            }

            var timestamp = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
            if (DateTimeOffset.UtcNow - timestamp > TimeSpan.FromSeconds(MaxTimestampAgeSeconds))
            {
                logger.LogWarning("Webhook timestamp is too old. Timestamp: {Timestamp}, CurrentTime: {CurrentTime}",
                    timestamp, DateTimeOffset.UtcNow);
                context.Result = new UnauthorizedResult();
                return;
            }

            // 4. Read the request body
            request.EnableBuffering();
            string bodyString;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true))
            {
                bodyString = await reader.ReadToEndAsync();
                request.Body.Position = 0; // Reset for model binding
            }

            logger.LogInformation("DEBUG: Request body: {Body}", bodyString);

            // 5. Construct the signed payload according to Replicate's spec: webhook_id.webhook_timestamp.body
            var signedPayload = $"{webhookId}.{webhookTimestamp}.{bodyString}";
            logger.LogInformation("DEBUG: Signed payload format: {SignedPayload}", signedPayload);

            // 6. Extract the secret key - Replicate uses base64 encoded secret after whsec_ prefix
            byte[] secretKeyBytes;
            if (secret.StartsWith("whsec_"))
            {
                var base64Secret = secret.Substring(6); // Remove "whsec_" prefix
                logger.LogInformation("DEBUG: Base64 secret after removing whsec_: {Base64Secret}", base64Secret);
                
                try
                {
                    secretKeyBytes = Convert.FromBase64String(base64Secret);
                    logger.LogInformation("DEBUG: Using decoded secret key bytes, length: {Length}", secretKeyBytes.Length);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("DEBUG: Failed to decode base64 secret, using UTF8 bytes of base64 string: {Error}", ex.Message);
                    secretKeyBytes = Encoding.UTF8.GetBytes(base64Secret);
                }
            }
            else
            {
                // Fallback to using the secret as UTF8 bytes
                secretKeyBytes = Encoding.UTF8.GetBytes(secret);
                logger.LogInformation("DEBUG: Using secret key as UTF8 bytes (no whsec_ prefix), length: {Length}", secretKeyBytes.Length);
            }

            // 7. Compute the expected signature
            using var hmac = new HMACSHA256(secretKeyBytes);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
            var computedSignature = Convert.ToBase64String(hash);

            logger.LogInformation("DEBUG: Computed signature: {Computed}", computedSignature);
            logger.LogInformation("DEBUG: Received signatures: {Received}", string.Join(",", signatures));

            // 8. Compare with received signatures
            var isValid = signatures.Any(receivedSignature =>
                string.Equals(receivedSignature, computedSignature, StringComparison.Ordinal));

            if (!isValid)
            {
                logger.LogWarning("Invalid webhook signature. Computed: {Computed}, Received: {Received}",
                    computedSignature, string.Join(",", signatures));
                context.Result = new UnauthorizedResult();
                return;
            }

            logger.LogInformation("Webhook signature validation successful.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during webhook signature validation.");
            context.Result = new StatusCodeResult(500);
        }
    }
}