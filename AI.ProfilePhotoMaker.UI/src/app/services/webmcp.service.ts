import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { StylePreviewService } from './style-preview.service';
import { ReplicateService, GenerateImagesRequest } from './replicate.service';
import { CreditService, CreditPackage } from './credit.service';
import { AuthService } from './auth.service';

// WebMCP interfaces to prevent TS errors if navigator.modelContext is not yet available
declare global {
  interface Navigator {
    modelContext?: ModelContext;
  }
}

interface ModelContext {
  registerTool(tool: ModelContextTool): void;
  clearContext(): void;
}

interface ModelContextTool {
  name: string;
  description: string;
  inputSchema?: object;
  execute: (input: any, client: ModelContextClient) => Promise<any>;
  annotations?: { readOnlyHint?: boolean };
}

interface ModelContextClient {
  requestUserInteraction(callback: () => Promise<any>): Promise<any>;
}

@Injectable({
  providedIn: 'root',
})
export class WebMCPService {
  private readonly stylePreviewService = inject(StylePreviewService);
  private readonly replicateService = inject(ReplicateService);
  private readonly creditService = inject(CreditService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  private isRegistered = false;

  constructor() {}

  registerTools() {
    if (this.isRegistered || typeof navigator.modelContext === 'undefined') {
      return;
    }

    console.log('Registering WebMCP tools...');

    try {
      navigator.modelContext.clearContext(); // Start fresh
      
      // Tool: list_styles
      navigator.modelContext.registerTool({
        name: 'list_styles',
        description: 'Get a list of all available professional headshot styles.',
        execute: () => this.listStyles(),
        annotations: { readOnlyHint: true },
      });

      // Tool: get_style_preview
      navigator.modelContext.registerTool({
        name: 'get_style_preview',
        description: 'Get a preview image URL for a specific style.',
        inputSchema: {
          type: 'object',
          properties: {
            styleName: { type: 'string', description: 'The name of the style to preview.' },
          },
          required: ['styleName'],
        },
        execute: (input) => this.getStylePreview(input.styleName),
        annotations: { readOnlyHint: true },
      });

      // Tool: generate_headshot
      navigator.modelContext.registerTool({
        name: 'generate_headshot',
        description: 'Generate a new professional headshot for the logged-in user.',
        inputSchema: {
          type: 'object',
          properties: {
            style: { type: 'string', description: 'The desired style for the headshot.' },
            numOutputs: { type: 'number', description: 'Number of images to generate (1-4).', default: 1 },
          },
          required: ['style'],
        },
        execute: (input, client) => this.generateHeadshot(input, client),
      });
      
      this.isRegistered = true;
      console.log('WebMCP tools registered successfully.');

    } catch (e) {
      console.error('Failed to register WebMCP tools:', e);
    }
  }

  private async listStyles(): Promise<{ style: string; url: string }[]> {
    const response = await firstValueFrom(this.stylePreviewService.getStylePreviews());
    return response.previews.map(p => ({ style: p.style, url: p.url }));
  }

  private async getStylePreview(styleName: string): Promise<string> {
    return await firstValueFrom(this.stylePreviewService.getStylePreviewUrl(styleName));
  }

  private async generateHeadshot(input: { style: string; numOutputs?: number }, client: ModelContextClient): Promise<any> {
    const user = await firstValueFrom(this.authService.user$);
    if (!user) {
      return { success: false, error: 'User is not logged in.' };
    }
    
    // In a real scenario, we'd need to get the trained model version
    // For now, this is a placeholder.
    const trainedModelVersion = 'mock-model-version'; // This needs a real source

    const request: GenerateImagesRequest = {
      trainedModelVersion,
      userId: user.id,
      style: input.style,
      numOutputs: input.numOutputs || 1,
    };

    return client.requestUserInteraction(async () => {
        // This is where you would show a confirmation dialog to the user
        console.log(`WebMCP: Requesting user confirmation to generate ${request.numOutputs} image(s) in style "${request.style}".`);
        this.router.navigate(['/gallery']); // Navigate to a relevant page
        
        const result = await firstValueFrom(this.replicateService.generateImages(request));
        return { success: true, predictionId: result.id, status: result.status };
    });
  }
}
