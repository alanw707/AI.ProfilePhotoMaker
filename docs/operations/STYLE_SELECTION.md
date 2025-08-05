# Style Selection & Customization

## Overview

The Style Selection system allows users to choose from 23+ professional photo styles for their AI-generated profile photos. Each style includes carefully crafted prompts, negative prompts, and preview images to help users visualize the final result.

## Available Styles

### Professional Styles
- **LinkedIn**: Professional networking headshot
- **Corporate**: Traditional business portrait
- **Executive**: C-suite level professional photo
- **Consultant**: Approachable business professional
- **Business**: General business appropriate

### Creative Styles
- **Artistic**: Creative and unique aesthetic
- **Author**: Literary and intellectual vibe
- **Creative**: Bold artistic expression
- **Fashion**: High-fashion editorial style
- **Glamour**: Polished magazine-style portrait

### Industry-Specific
- **Medical**: Healthcare professional
- **Legal**: Law professional portrait
- **Academic**: Scholar/educator style
- **Tech Professional**: Modern tech industry
- **Entrepreneur**: Startup founder aesthetic

### Lifestyle Styles
- **Casual**: Relaxed everyday look
- **Fitness**: Active lifestyle portrait
- **Digital Nomad**: Remote worker vibe
- **Influencer**: Social media ready
- **Spiritual**: Mindful and serene

## Style Configuration

### Database Schema

```sql
CREATE TABLE Styles (
    Id INTEGER PRIMARY KEY,
    Name TEXT UNIQUE NOT NULL,
    DisplayName TEXT NOT NULL,
    Description TEXT,
    PromptTemplate TEXT NOT NULL,
    NegativePrompt TEXT,
    IsActive BOOLEAN DEFAULT 1,
    SortOrder INTEGER DEFAULT 0,
    CreatedAt DATETIME NOT NULL
);

CREATE TABLE UserStyleSelections (
    Id INTEGER PRIMARY KEY,
    UserId TEXT NOT NULL,
    StyleId INTEGER NOT NULL,
    SelectedAt DATETIME NOT NULL,
    FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id),
    FOREIGN KEY (StyleId) REFERENCES Styles(Id)
);
```

### Style Prompt System

Each style includes:

1. **Prompt Template**
   ```
   "Professional headshot of {trigger_word}, wearing business attire, 
   confident expression, studio lighting, clean background..."
   ```

2. **Negative Prompt**
   ```
   "casual clothing, messy hair, poor lighting, cluttered background,
   low quality, blurry, distorted features..."
   ```

3. **Style Parameters**
   ```json
   {
     "guidance_scale": 3.5,
     "num_inference_steps": 28,
     "aspect_ratio": "1:1",
     "style_strength": 0.8
   }
   ```

## Frontend Implementation

### Style Selection Component

```typescript
@Component({
  selector: 'app-style-selector',
  template: `
    <div class="style-selector">
      <h2>Choose Your Styles ({{selectedCount}}/10)</h2>
      
      <div class="style-grid">
        <mat-card *ngFor="let style of availableStyles"
                  [class.selected]="isSelected(style)"
                  [class.disabled]="!canSelect(style)"
                  (click)="toggleStyle(style)">
          
          <img [src]="style.previewUrl" 
               [alt]="style.displayName">
          
          <mat-card-content>
            <h3>{{style.displayName}}</h3>
            <p>{{style.description}}</p>
            <div class="credit-cost">
              <mat-icon>star</mat-icon>
              <span>10 credits</span>
            </div>
          </mat-card-content>
          
          <mat-checkbox [checked]="isSelected(style)"
                        [disabled]="!canSelect(style)">
          </mat-checkbox>
        </mat-card>
      </div>
      
      <div class="selection-summary">
        <p>Total cost: {{totalCost}} credits</p>
        <button mat-raised-button 
                color="primary"
                [disabled]="selectedStyles.length === 0"
                (click)="confirmSelection()">
          Confirm Selection
        </button>
      </div>
    </div>
  `
})
export class StyleSelectorComponent {
  maxStyles = 10;
  creditCostPerStyle = 10;
  
  canSelect(style: Style): boolean {
    return this.isSelected(style) || 
           this.selectedStyles.length < this.maxStyles;
  }
  
  get totalCost(): number {
    return this.selectedStyles.length * this.creditCostPerStyle;
  }
}
```

### Style Preview Gallery

```typescript
interface StylePreview {
  id: number;
  name: string;
  displayName: string;
  description: string;
  previewUrl: string;
  examples: string[];  // Additional example images
}

@Component({
  selector: 'app-style-preview',
  template: `
    <mat-dialog-content>
      <h2>{{style.displayName}}</h2>
      
      <div class="preview-carousel">
        <img [src]="currentImage" class="main-preview">
        
        <div class="thumbnail-strip">
          <img *ngFor="let img of allImages" 
               [src]="img"
               [class.active]="img === currentImage"
               (click)="currentImage = img">
        </div>
      </div>
      
      <div class="style-details">
        <h3>About This Style</h3>
        <p>{{style.description}}</p>
        
        <h3>Best For:</h3>
        <ul>
          <li *ngFor="let useCase of style.useCases">
            {{useCase}}
          </li>
        </ul>
        
        <div class="cost-info">
          <mat-icon>info</mat-icon>
          <span>Generates 2 photos for 10 credits</span>
        </div>
      </div>
    </mat-dialog-content>
  `
})
```

## Style Management

### Backend API

```csharp
[ApiController]
[Route("api/[controller]")]
public class StyleController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAvailableStyles()
    {
        var styles = await _context.Styles
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.DisplayName)
            .Select(s => new StyleDto
            {
                Id = s.Id,
                Name = s.Name,
                DisplayName = s.DisplayName,
                Description = s.Description,
                PreviewUrl = $"/style-previews/{s.Name}.jpg"
            })
            .ToListAsync();
            
        return Ok(styles);
    }
    
    [HttpPost("select")]
    public async Task<IActionResult> SelectStyles(
        [FromBody] SelectStylesRequest request)
    {
        if (request.StyleIds.Count > 10)
        {
            return BadRequest("Maximum 10 styles allowed");
        }
        
        // Clear existing selections
        var existing = await _context.UserStyleSelections
            .Where(s => s.UserId == UserId)
            .ToListAsync();
        _context.UserStyleSelections.RemoveRange(existing);
        
        // Add new selections
        foreach (var styleId in request.StyleIds)
        {
            _context.UserStyleSelections.Add(new UserStyleSelection
            {
                UserId = UserId,
                StyleId = styleId,
                SelectedAt = DateTime.UtcNow
            });
        }
        
        await _context.SaveChangesAsync();
        return Ok();
    }
}
```

### Style Preview Generation

```csharp
public class StylePreviewService
{
    public async Task GenerateStylePreviews()
    {
        var styles = await _context.Styles.ToListAsync();
        
        foreach (var style in styles)
        {
            var prompt = style.PromptTemplate.Replace("{trigger_word}", "person");
            
            var result = await _replicateClient.GenerateImage(new
            {
                prompt = prompt,
                negative_prompt = style.NegativePrompt,
                model = "black-forest-labs/flux-dev",
                guidance_scale = 3.5,
                num_outputs = 1
            });
            
            // Save preview image
            var imagePath = $"/style-previews/{style.Name}.jpg";
            await SaveImage(result.Output[0], imagePath);
        }
    }
}
```

## Style Customization

### User Preferences

```typescript
interface UserStylePreferences {
  favoriteStyles: number[];
  excludedStyles: number[];
  customPromptAdditions?: string;
  preferredAspectRatio?: '1:1' | '4:5' | '16:9';
}
```

### Advanced Options

```typescript
@Component({
  selector: 'app-style-advanced',
  template: `
    <mat-expansion-panel>
      <mat-expansion-panel-header>
        <mat-panel-title>Advanced Options</mat-panel-title>
      </mat-expansion-panel-header>
      
      <div class="advanced-options">
        <mat-form-field>
          <mat-label>Additional Instructions</mat-label>
          <textarea matInput 
                    [(ngModel)]="customPrompt"
                    placeholder="e.g., 'wearing glasses', 'smiling'">
          </textarea>
        </mat-form-field>
        
        <mat-radio-group [(ngModel)]="aspectRatio">
          <mat-label>Aspect Ratio</mat-label>
          <mat-radio-button value="1:1">Square (1:1)</mat-radio-button>
          <mat-radio-button value="4:5">Portrait (4:5)</mat-radio-button>
          <mat-radio-button value="16:9">Wide (16:9)</mat-radio-button>
        </mat-radio-group>
      </div>
    </mat-expansion-panel>
  `
})
```

## Style Categories

### Organization

```typescript
interface StyleCategory {
  name: string;
  displayName: string;
  styles: Style[];
  icon: string;
  description: string;
}

const categories: StyleCategory[] = [
  {
    name: 'professional',
    displayName: 'Professional',
    icon: 'business',
    description: 'Perfect for LinkedIn, resumes, and corporate use',
    styles: ['linkedin', 'corporate', 'executive', 'consultant']
  },
  {
    name: 'creative',
    displayName: 'Creative',
    icon: 'palette',
    description: 'Express your artistic side',
    styles: ['artistic', 'author', 'creative', 'fashion']
  }
  // ... more categories
];
```

### Filtering UI

```typescript
<mat-chip-list class="category-filter">
  <mat-chip *ngFor="let category of categories"
            [selected]="selectedCategory === category"
            (click)="filterByCategory(category)">
    <mat-icon>{{category.icon}}</mat-icon>
    {{category.displayName}}
  </mat-chip>
</mat-chip-list>
```

## Responsive Design

```scss
.style-grid {
  display: grid;
  gap: 1.5rem;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  
  @media (max-width: 768px) {
    grid-template-columns: repeat(2, 1fr);
    gap: 1rem;
  }
  
  @media (max-width: 480px) {
    grid-template-columns: 1fr;
  }
}

.style-card {
  cursor: pointer;
  transition: all 0.3s ease;
  
  &:hover {
    transform: translateY(-4px);
    box-shadow: 0 8px 24px rgba(0,0,0,0.1);
  }
  
  &.selected {
    border: 2px solid var(--primary-color);
    background: var(--primary-light);
  }
  
  &.disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }
}
```

## Best Practices

1. **Style Quality**
   - Test each style thoroughly
   - Provide clear preview images
   - Update prompts based on results

2. **User Experience**
   - Show credit cost clearly
   - Provide style recommendations
   - Allow preview before selection

3. **Performance**
   - Lazy load preview images
   - Cache style data
   - Optimize image delivery

## API Reference

See [API Reference](./API_REFERENCE.md#styles) for detailed endpoint documentation.