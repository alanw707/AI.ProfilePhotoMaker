using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AI.ProfilePhotoMaker.API.Data;

namespace AI.ProfilePhotoMaker.API.Controllers
{
    /// <summary>
    /// Base controller that provides common functionality for all API controllers
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public abstract class BaseController : ControllerBase
    {
        protected readonly ILogger Logger;
        protected readonly ApplicationDbContext? Context;

        protected BaseController(ILogger logger, ApplicationDbContext? context = null)
        {
            Logger = logger;
            Context = context;
        }

        /// <summary>
        /// Gets the current user ID from JWT claims
        /// </summary>
        /// <returns>User ID or null if not authenticated</returns>
        protected string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        /// <summary>
        /// Validates user authentication and returns error response if not authenticated
        /// </summary>
        /// <returns>Unauthorized response if not authenticated, otherwise null</returns>
        protected IActionResult? ValidateAuthentication()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, error = new { code = "Unauthorized", message = "User not authenticated." } });
            }
            return null;
        }

        /// <summary>
        /// Returns a standardized success response
        /// </summary>
        /// <param name="data">Response data</param>
        /// <param name="message">Optional success message</param>
        /// <returns>Standardized success response</returns>
        protected IActionResult SuccessResponse(object data, string? message = null)
        {
            return Ok(new { success = true, data = data, message = message, error = (object?)null });
        }

        /// <summary>
        /// Returns a standardized error response
        /// </summary>
        /// <param name="code">Error code</param>
        /// <param name="message">Error message</param>
        /// <param name="statusCode">HTTP status code (default: 400)</param>
        /// <returns>Standardized error response</returns>
        protected IActionResult ErrorResponse(string code, string message, int statusCode = 400)
        {
            var response = new { success = false, error = new { code = code, message = message } };
            return statusCode switch
            {
                401 => Unauthorized(response),
                404 => NotFound(response),
                500 => StatusCode(500, response),
                _ => BadRequest(response)
            };
        }

        /// <summary>
        /// Logs an error with user context
        /// </summary>
        /// <param name="exception">Exception to log</param>
        /// <param name="message">Additional message</param>
        /// <param name="userId">User ID (optional, will get from claims if not provided)</param>
        protected void LogError(Exception exception, string message, string? userId = null)
        {
            userId ??= GetCurrentUserId();
            Logger.LogError(exception, "{Message} for user {UserId}", message, userId);
        }

        /// <summary>
        /// Logs information with user context
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="userId">User ID (optional, will get from claims if not provided)</param>
        protected void LogInfo(string message, string? userId = null)
        {
            userId ??= GetCurrentUserId();
            Logger.LogInformation("{Message} for user {UserId}", message, userId);
        }
    }
}