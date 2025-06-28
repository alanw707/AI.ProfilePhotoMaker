using Microsoft.AspNetCore.Mvc;

namespace AI.ProfilePhotoMaker.API.Controllers;

[Route("api/placeholder")]
[ApiController]
public class PlaceholderImageController : ControllerBase
{
    private readonly ILogger<PlaceholderImageController> _logger;

    public PlaceholderImageController(ILogger<PlaceholderImageController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate a placeholder image on demand
    /// </summary>
    [HttpGet("style-preview")]
    public IActionResult GetStylePreviewPlaceholder()
    {
        try
        {
            // Return a simple SVG as an image
            var svg = @"<svg width=""400"" height=""400"" viewBox=""0 0 400 400"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
  <defs>
    <linearGradient id=""bgGradient"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">
      <stop offset=""0%"" style=""stop-color:#6366f1;stop-opacity:1"" />
      <stop offset=""100%"" style=""stop-color:#8b5cf6;stop-opacity:1"" />
    </linearGradient>
  </defs>
  
  <!-- Background -->
  <rect width=""400"" height=""400"" fill=""url(#bgGradient)"" rx=""8""/>
  
  <!-- Person silhouette -->
  <g opacity=""0.3"">
    <!-- Head -->
    <circle cx=""200"" cy=""120"" r=""45"" fill=""white""/>
    <!-- Body -->
    <path d=""M 140 170 Q 200 150 260 170 L 240 350 L 160 350 Z"" fill=""white""/>
  </g>
  
  <!-- Text -->
  <text x=""200"" y=""380"" font-family=""Arial, sans-serif"" font-size=""24"" font-weight=""bold"" fill=""white"" text-anchor=""middle"">
    Style Preview
  </text>
</svg>";

            // Convert SVG to bytes
            var svgBytes = System.Text.Encoding.UTF8.GetBytes(svg);
            
            // Return as SVG image
            return File(svgBytes, "image/svg+xml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating placeholder image");
            // Return a 1x1 transparent PNG as fallback
            var transparentPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
            return File(transparentPng, "image/png");
        }
    }
}