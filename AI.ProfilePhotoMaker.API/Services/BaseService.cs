using AI.ProfilePhotoMaker.API.Data;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Services;

/// <summary>
/// Base service class that provides common database and logging patterns
/// </summary>
public abstract class BaseService<T> where T : class
{
    protected readonly ApplicationDbContext Context;
    protected readonly ILogger<T> Logger;

    protected BaseService(ApplicationDbContext context, ILogger<T> logger)
    {
        Context = context;
        Logger = logger;
    }

    /// <summary>
    /// Safely saves changes to the database with error handling and logging
    /// </summary>
    protected async Task<bool> SaveChangesAsync(string operation = "database operation")
    {
        try
        {
            var changes = await Context.SaveChangesAsync();
            if (changes > 0)
            {
                Logger.LogDebug("Successfully completed {Operation}: {Changes} changes saved", operation, changes);
            }
            return true;
        }
        catch (DbUpdateException ex)
        {
            Logger.LogError(ex, "Database update failed during {Operation}: {Error}", operation, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during {Operation}: {Error}", operation, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Safely saves changes and returns the number of affected entities
    /// </summary>
    protected async Task<int> SaveChangesWithCountAsync(string operation = "database operation")
    {
        try
        {
            var changes = await Context.SaveChangesAsync();
            Logger.LogDebug("Successfully completed {Operation}: {Changes} changes saved", operation, changes);
            return changes;
        }
        catch (DbUpdateException ex)
        {
            Logger.LogError(ex, "Database update failed during {Operation}: {Error}", operation, ex.Message);
            return -1;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unexpected error during {Operation}: {Error}", operation, ex.Message);
            return -1;
        }
    }

    /// <summary>
    /// Executes a database operation within a transaction with proper error handling
    /// </summary>
    protected async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> operation, 
        string operationName = "transaction operation")
    {
        using var transaction = await Context.Database.BeginTransactionAsync();
        try
        {
            Logger.LogDebug("Starting transaction for {Operation}", operationName);
            
            var result = await operation();
            
            await transaction.CommitAsync();
            Logger.LogDebug("Successfully completed transaction for {Operation}", operationName);
            
            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Transaction failed for {Operation}: {Error}", operationName, ex.Message);
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Validates that a user ID is not null or empty
    /// </summary>
    protected bool ValidateUserId(string? userId, string operation = "operation")
    {
        if (string.IsNullOrEmpty(userId))
        {
            Logger.LogWarning("Invalid or missing user ID for {Operation}", operation);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Validates that an entity exists
    /// </summary>
    protected bool ValidateEntityExists<TEntity>(TEntity? entity, string entityName, object? id = null)
        where TEntity : class
    {
        if (entity == null)
        {
            var idText = id != null ? $" with ID {id}" : "";
            Logger.LogWarning("{EntityName}{IdText} not found", entityName, idText);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Logs the start of an operation with user context
    /// </summary>
    protected void LogOperationStart(string operation, string? userId = null, object? additionalContext = null)
    {
        if (userId != null)
        {
            if (additionalContext != null)
            {
                Logger.LogInformation("Starting {Operation} for user {UserId}: {@Context}", 
                    operation, userId, additionalContext);
            }
            else
            {
                Logger.LogInformation("Starting {Operation} for user {UserId}", operation, userId);
            }
        }
        else
        {
            Logger.LogInformation("Starting {Operation}", operation);
        }
    }

    /// <summary>
    /// Logs the completion of an operation with results
    /// </summary>
    protected void LogOperationComplete(string operation, string? userId = null, object? result = null)
    {
        if (userId != null)
        {
            if (result != null)
            {
                Logger.LogInformation("Completed {Operation} for user {UserId}: {@Result}", 
                    operation, userId, result);
            }
            else
            {
                Logger.LogInformation("Completed {Operation} for user {UserId}", operation, userId);
            }
        }
        else
        {
            Logger.LogInformation("Completed {Operation}", operation);
        }
    }

    /// <summary>
    /// Safely disposes resources if the service implements IDisposable
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Dispose managed resources if needed
            // Context is managed by DI container, so we don't dispose it here
        }
    }
}