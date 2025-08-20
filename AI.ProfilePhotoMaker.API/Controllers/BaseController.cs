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
        protected IActionResult SuccessResponse(object? data, string? message = null)
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

        /// <summary>
        /// Returns a standardized validation error response
        /// </summary>
        /// <param name="message">Validation error message</param>
        /// <returns>Standardized validation error response</returns>
        protected IActionResult ValidationError(string message)
        {
            return ErrorResponse("ValidationError", message, 400);
        }

        /// <summary>
        /// Returns a standardized not found response
        /// </summary>
        /// <param name="resourceName">Name of the resource not found</param>
        /// <param name="id">Optional ID of the resource</param>
        /// <returns>Standardized not found response</returns>
        protected IActionResult NotFoundResponse(string resourceName, object? id = null)
        {
            var message = id != null
                ? $"{resourceName} with ID {id} not found"
                : $"{resourceName} not found";
            return ErrorResponse("NotFound", message, 404);
        }

        /// <summary>
        /// Returns a standardized internal server error response
        /// </summary>
        /// <param name="exception">Exception that caused the error</param>
        /// <param name="userMessage">User-friendly error message</param>
        /// <returns>Standardized internal server error response</returns>
        protected IActionResult InternalServerErrorResponse(Exception exception, string userMessage = "An internal error occurred")
        {
            LogError(exception, userMessage);
            return ErrorResponse("InternalError", userMessage, 500);
        }

        /// <summary>
        /// Returns a standardized invalid input response
        /// </summary>
        /// <param name="inputName">Name of the invalid input</param>
        /// <param name="reason">Reason why the input is invalid</param>
        /// <returns>Standardized invalid input response</returns>
        protected IActionResult InvalidInputResponse(string inputName, string reason)
        {
            return ErrorResponse("InvalidInput", $"Invalid {inputName}: {reason}", 400);
        }

        /// <summary>
        /// Validates model state and returns error response if invalid
        /// </summary>
        /// <returns>BadRequest response if model state is invalid, otherwise null</returns>
        protected IActionResult? ValidateModelState()
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value?.Errors?.Count > 0)
                    .SelectMany(x => x.Value?.Errors?.Select(e => e.ErrorMessage) ?? [])
                    .ToList();

                var message = string.Join("; ", errors);
                return ValidationError(message);
            }
            return null;
        }

        /// <summary>
        /// Executes an async operation with standardized error handling
        /// </summary>
        /// <typeparam name="T">Return type</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <returns>Operation result or error response</returns>
        protected async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> operation, string operationName)
        {
            try
            {
                var result = await operation();
                return SuccessResponse(result);
            }
            catch (ArgumentException ex)
            {
                return ValidationError(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ErrorResponse("InvalidOperation", ex.Message, 400);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ErrorResponse("Unauthorized", ex.Message, 401);
            }
            catch (Exception ex)
            {
                return InternalServerErrorResponse(ex, $"Error executing {operationName}");
            }
        }
    }
}
