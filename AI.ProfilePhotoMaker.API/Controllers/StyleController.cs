using AI.ProfilePhotoMaker.API.Data;
using AI.ProfilePhotoMaker.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class StyleController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<StyleController> _logger;

    public StyleController(ApplicationDbContext context, ILogger<StyleController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets all available styles
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStyles()
    {
        try
        {
            var styles = await _context.Styles
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.IsActive
                })
                .ToListAsync();

            return Ok(new { success = true, data = styles, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving styles");
            return StatusCode(500, new { success = false, error = new { code = "DatabaseError", message = "Failed to retrieve styles." } });
        }
    }

    /// <summary>
    /// Gets a specific style by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetStyle(int id)
    {
        try
        {
            var style = await _context.Styles
                .Where(s => s.Id == id && s.IsActive)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Description,
                    s.IsActive
                })
                .FirstOrDefaultAsync();

            if (style == null)
            {
                return NotFound(new { success = false, error = new { code = "StyleNotFound", message = "Style not found." } });
            }

            return Ok(new { success = true, data = style, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving style {StyleId}", id);
            return StatusCode(500, new { success = false, error = new { code = "DatabaseError", message = "Failed to retrieve style." } });
        }
    }

    /// <summary>
    /// Gets the prompt template for a specific style (used internally by ReplicateApiClient)
    /// </summary>
    [HttpGet("{id}/template")]
    public async Task<IActionResult> GetStyleTemplate(int id)
    {
        try
        {
            var style = await _context.Styles
                .Where(s => s.Id == id && s.IsActive)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.PromptTemplate,
                    s.NegativePromptTemplate
                })
                .FirstOrDefaultAsync();

            if (style == null)
            {
                return NotFound(new { success = false, error = new { code = "StyleNotFound", message = "Style not found." } });
            }

            return Ok(new { success = true, data = style, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving style template {StyleId}", id);
            return StatusCode(500, new { success = false, error = new { code = "DatabaseError", message = "Failed to retrieve style template." } });
        }
    }

    /// <summary>
    /// Gets style template by name (used internally by ReplicateApiClient)
    /// </summary>
    [HttpGet("name/{name}/template")]
    public async Task<IActionResult> GetStyleTemplateByName(string name)
    {
        try
        {
            var style = await _context.Styles
                .Where(s => s.Name.ToLower() == name.ToLower() && s.IsActive)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.PromptTemplate,
                    s.NegativePromptTemplate
                })
                .FirstOrDefaultAsync();

            if (style == null)
            {
                return NotFound(new { success = false, error = new { code = "StyleNotFound", message = "Style not found." } });
            }

            return Ok(new { success = true, data = style, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving style template by name {StyleName}", name);
            return StatusCode(500, new { success = false, error = new { code = "DatabaseError", message = "Failed to retrieve style template." } });
        }
    }

    /// <summary>
    /// Admin endpoint to create a new style
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")] // Restrict to admin users only
    public async Task<IActionResult> CreateStyle([FromBody] StyleCreateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        try
        {
            // Check if style name already exists
            var existingStyle = await _context.Styles
                .FirstOrDefaultAsync(s => s.Name.ToLower() == dto.Name.ToLower());

            if (existingStyle != null)
            {
                return Conflict(new { success = false, error = new { code = "StyleExists", message = "A style with this name already exists." } });
            }

            var style = new Style
            {
                Name = dto.Name,
                Description = dto.Description,
                PromptTemplate = dto.PromptTemplate,
                NegativePromptTemplate = dto.NegativePromptTemplate,
                IsActive = dto.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Styles.Add(style);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStyle), new { id = style.Id }, 
                new { success = true, data = new { style.Id, style.Name, style.Description, style.IsActive }, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating style");
            return StatusCode(500, new { success = false, error = new { code = "DatabaseError", message = "Failed to create style." } });
        }
    }

    /// <summary>
    /// Admin endpoint to update a style
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")] // Restrict to admin users only
    public async Task<IActionResult> UpdateStyle(int id, [FromBody] StyleUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, error = new { code = "InvalidModel", message = "Invalid input." } });

        try
        {
            var style = await _context.Styles.FindAsync(id);
            if (style == null)
            {
                return NotFound(new { success = false, error = new { code = "StyleNotFound", message = "Style not found." } });
            }

            // Check if new name conflicts with existing style
            if (!string.IsNullOrEmpty(dto.Name) && dto.Name != style.Name)
            {
                var existingStyle = await _context.Styles
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == dto.Name.ToLower() && s.Id != id);

                if (existingStyle != null)
                {
                    return Conflict(new { success = false, error = new { code = "StyleExists", message = "A style with this name already exists." } });
                }
            }

            // Update fields
            if (!string.IsNullOrEmpty(dto.Name)) style.Name = dto.Name;
            if (!string.IsNullOrEmpty(dto.Description)) style.Description = dto.Description;
            if (!string.IsNullOrEmpty(dto.PromptTemplate)) style.PromptTemplate = dto.PromptTemplate;
            if (!string.IsNullOrEmpty(dto.NegativePromptTemplate)) style.NegativePromptTemplate = dto.NegativePromptTemplate;
            if (dto.IsActive.HasValue) style.IsActive = dto.IsActive.Value;
            style.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, data = new { style.Id, style.Name, style.Description, style.IsActive }, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating style {StyleId}", id);
            return StatusCode(500, new { success = false, error = new { code = "DatabaseError", message = "Failed to update style." } });
        }
    }

    /// <summary>
    /// Admin endpoint to deactivate a style (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")] // Restrict to admin users only
    public async Task<IActionResult> DeleteStyle(int id)
    {
        try
        {
            var style = await _context.Styles.FindAsync(id);
            if (style == null)
            {
                return NotFound(new { success = false, error = new { code = "StyleNotFound", message = "Style not found." } });
            }

            // Soft delete - just deactivate
            style.IsActive = false;
            style.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, data = new { message = "Style deactivated successfully." }, error = (object?)null });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting style {StyleId}", id);
            return StatusCode(500, new { success = false, error = new { code = "DatabaseError", message = "Failed to delete style." } });
        }
    }
}

/// <summary>
/// DTO for creating a new style
/// </summary>
public record StyleCreateDto(
    string Name,
    string Description,
    string PromptTemplate,
    string NegativePromptTemplate,
    bool IsActive = true
);

/// <summary>
/// DTO for updating a style
/// </summary>
public record StyleUpdateDto(
    string? Name = null,
    string? Description = null,
    string? PromptTemplate = null,
    string? NegativePromptTemplate = null,
    bool? IsActive = null
);