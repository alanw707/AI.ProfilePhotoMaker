import { Injectable, NgZone } from '@angular/core';
import * as faceapi from 'face-api.js';
import { IImageQualityService, QualityScore } from '../interfaces/service.interfaces';

@Injectable({
  providedIn: 'root',
})
export class ImageQualityService implements IImageQualityService {
  constructor(private ngZone: NgZone) {}

  /**
   * Calculate comprehensive quality score
   */
  async calculateQualityScore(
    img: HTMLImageElement,
    detections: faceapi.WithFaceLandmarks<any>[],
    file: File
  ): Promise<QualityScore> {
    const scores = {
      faceQuality: 0,
      technical: 0,
      composition: 0,
      fluxCompatibility: 0,
      lighting: 0,
    };

    const suggestions: string[] = [];

    // 1. Face Quality (30 points)
    if (detections.length === 1) {
      const detectionData = detections[0]?.detection;
      const confidence = typeof detectionData?.score === 'number' ? detectionData.score : 0;

      if (confidence > 0) {
        scores.faceQuality = Math.min(30, Math.max(0, confidence * 30));
      }

      if (confidence === 0) {
        suggestions.push('Unable to evaluate face clarity. Try retaking the photo.');
      } else if (confidence < 0.7) {
        suggestions.push('Face is not clearly visible. Try better lighting or camera angle.');
      }

      if (!detectionData?.box) {
        suggestions.push('Face detection data was incomplete. Ensure your face is fully visible.');
      }
    } else {
      suggestions.push('Upload photo with exactly one clearly visible face.');
    }

    // 2. Technical Quality (25 points)
    const techScore = await this.assessTechnicalQuality(img, file);
    scores.technical = techScore.score;
    suggestions.push(...techScore.suggestions);

    // 3. Composition (20 points)
    const compScore = this.assessComposition(img, detections);
    scores.composition = compScore.score;
    suggestions.push(...compScore.suggestions);

    // 4. FLUX Compatibility (15 points)
    const fluxScore = await this.assessFluxCompatibility(img);
    scores.fluxCompatibility = fluxScore.score;
    suggestions.push(...fluxScore.suggestions);

    // 5. Lighting (10 points)
    const lightScore = this.assessLighting(img);
    scores.lighting = lightScore.score;
    suggestions.push(...lightScore.suggestions);

    const overall = Math.round(
      scores.faceQuality +
        scores.technical +
        scores.composition +
        scores.fluxCompatibility +
        scores.lighting
    );

    return {
      overall: Math.max(1, Math.min(100, overall)),
      breakdown: scores,
      suggestions: suggestions.filter(s => s.length > 0),
    };
  }

  /**
   * Assess technical image quality
   */
  private async assessTechnicalQuality(
    img: HTMLImageElement,
    file: File
  ): Promise<{ score: number; suggestions: string[] }> {
    const suggestions: string[] = [];
    let score = 0;

    // Resolution check (10 points)
    const minRes = 512;
    const idealRes = 1024;

    if (img.width >= idealRes && img.height >= idealRes) {
      score += 10;
    } else if (img.width >= minRes && img.height >= minRes) {
      score += 7;
      suggestions.push('Higher resolution image (1024x1024+) recommended for best results.');
    } else {
      score += 3;
      suggestions.push('Image resolution is too low. Upload higher quality image.');
    }

    // File size check (5 points)
    const fileSizeMB = file.size / (1024 * 1024);
    if (fileSizeMB > 0.5 && fileSizeMB <= 7) {
      score += 5;
    } else if (fileSizeMB <= 0.5) {
      score += 2;
      suggestions.push('File size is small, which may indicate low quality.');
    }

    // Blur detection (10 points)
    const blurScore = await this.detectBlur(img);
    score += blurScore;

    if (blurScore < 5) {
      suggestions.push('Image appears blurry. Use better focus or lighting.');
    }

    return { score, suggestions };
  }

  /**
   * Assess image composition
   */
  private assessComposition(
    img: HTMLImageElement,
    detections: faceapi.WithFaceLandmarks<any>[]
  ): { score: number; suggestions: string[] } {
    const suggestions: string[] = [];
    let score = 0;

    if (detections.length === 1) {
      const detection = detections[0]?.detection;
      const box = detection?.box;

      if (!box) {
        suggestions.push('Composition cannot be assessed without detailed face detection data.');
        return { score, suggestions };
      }

      // Face positioning (10 points)
      const faceCenterX = (box.x + box.width / 2) / img.width;
      const faceCenterY = (box.y + box.height / 2) / img.height;

      // Check if face is roughly centered
      if (faceCenterX >= 0.3 && faceCenterX <= 0.7) {
        score += 5;
      } else {
        suggestions.push('Center your face in the photo for better composition.');
      }

      if (faceCenterY >= 0.2 && faceCenterY <= 0.6) {
        score += 5;
      } else {
        suggestions.push('Position your face in the upper portion of the photo.');
      }

      // Face size (10 points)
      const faceArea = (box.width * box.height) / (img.width * img.height);
      if (faceArea >= 0.1 && faceArea <= 0.4) {
        score += 10;
      } else if (faceArea < 0.1) {
        score += 5;
        suggestions.push('Move closer to camera or crop tighter for better face visibility.');
      } else {
        score += 7;
        suggestions.push('Face takes up a lot of the frame. Consider backing up slightly.');
      }
    } else {
      suggestions.push('Composition cannot be assessed without a clear single face.');
    }

    return { score, suggestions };
  }

  /**
   * Assess FLUX AI compatibility
   */
  private async assessFluxCompatibility(
    img: HTMLImageElement
  ): Promise<{ score: number; suggestions: string[] }> {
    const suggestions: string[] = [];
    let score = 10; // Start optimistic

    // Check for common FLUX negative prompt issues
    // This is a simplified assessment - could be enhanced with ML models

    // Basic image quality indicators
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d');

    if (!ctx) {
      suggestions.push('Unable to assess FLUX compatibility due to canvas support limitations.');
      return { score: Math.max(0, score), suggestions };
    }

    canvas.width = img.width;
    canvas.height = img.height;

    try {
      ctx.drawImage(img, 0, 0);
      const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
      const data = imageData.data;

      // Check for overexposure/underexposure
      let brightPixels = 0;
      let darkPixels = 0;

      for (let i = 0; i < data.length; i += 4) {
        const avg = (data[i] + data[i + 1] + data[i + 2]) / 3;
        if (avg > 240) {
          brightPixels++;
        }
        if (avg < 15) {
          darkPixels++;
        }
      }

      const totalPixels = data.length / 4;
      const brightRatio = brightPixels / totalPixels;
      const darkRatio = darkPixels / totalPixels;

      if (brightRatio > 0.3) {
        score -= 3;
        suggestions.push(
          'Image appears overexposed. Reduce lighting or use better camera settings.'
        );
      }

      if (darkRatio > 0.3) {
        score -= 3;
        suggestions.push('Image appears underexposed. Increase lighting for better visibility.');
      }
    } catch (error) {
      suggestions.push('Unable to assess FLUX compatibility due to canvas analysis error.');
    }

    return { score: Math.max(0, score), suggestions };
  }

  /**
   * Assess lighting quality
   */
  private assessLighting(img: HTMLImageElement): { score: number; suggestions: string[] } {
    const suggestions: string[] = [];
    let score = 8; // Start with good score

    // Basic lighting assessment using canvas analysis
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d');

    if (!ctx) {
      suggestions.push('Unable to assess lighting quality.');
      return { score: Math.max(0, Math.min(10, score)), suggestions };
    }

    canvas.width = Math.min(img.width, 200); // Sample for performance
    canvas.height = Math.min(img.height, 200);

    try {
      ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
      const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
      const data = imageData.data;

      let totalBrightness = 0;
      for (let i = 0; i < data.length; i += 4) {
        totalBrightness += (data[i] + data[i + 1] + data[i + 2]) / 3;
      }

      const avgBrightness = totalBrightness / (data.length / 4);

      if (avgBrightness < 50) {
        score -= 4;
        suggestions.push('Image is too dark. Use better lighting for clearer results.');
      } else if (avgBrightness > 200) {
        score -= 3;
        suggestions.push('Image is too bright. Reduce harsh lighting or exposure.');
      } else if (avgBrightness >= 80 && avgBrightness <= 180) {
        score += 2; // Bonus for good lighting
      }
    } catch (error) {
      // Lighting analysis failed
      suggestions.push('Unable to assess lighting quality.');
    }

    return { score: Math.max(0, Math.min(10, score)), suggestions };
  }

  /**
   * Detect image blur using Laplacian variance
   */
  private async detectBlur(img: HTMLImageElement): Promise<number> {
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d');

    if (!ctx) {
      return 5; // Default medium score if analysis fails
    }

    // Sample size for performance
    const sampleSize = 100;
    canvas.width = sampleSize;
    canvas.height = sampleSize;

    try {
      ctx.drawImage(img, 0, 0, sampleSize, sampleSize);
      const imageData = ctx.getImageData(0, 0, sampleSize, sampleSize);
      const data = imageData.data;

      // Convert to grayscale and apply Laplacian operator
      let variance = 0;
      const laplacian = [];

      // Simplified blur detection
      for (let y = 1; y < sampleSize - 1; y++) {
        for (let x = 1; x < sampleSize - 1; x++) {
          const idx = (y * sampleSize + x) * 4;
          const gray = (data[idx] + data[idx + 1] + data[idx + 2]) / 3;

          // Simple edge detection
          const neighbors = [
            (data[(y - 1) * sampleSize * 4 + x * 4] +
              data[(y - 1) * sampleSize * 4 + x * 4 + 1] +
              data[(y - 1) * sampleSize * 4 + x * 4 + 2]) /
              3,
            (data[(y + 1) * sampleSize * 4 + x * 4] +
              data[(y + 1) * sampleSize * 4 + x * 4 + 1] +
              data[(y + 1) * sampleSize * 4 + x * 4 + 2]) /
              3,
            (data[y * sampleSize * 4 + (x - 1) * 4] +
              data[y * sampleSize * 4 + (x - 1) * 4 + 1] +
              data[y * sampleSize * 4 + (x - 1) * 4 + 2]) /
              3,
            (data[y * sampleSize * 4 + (x + 1) * 4] +
              data[y * sampleSize * 4 + (x + 1) * 4 + 1] +
              data[y * sampleSize * 4 + (x + 1) * 4 + 2]) /
              3,
          ];

          const laplacianValue = Math.abs(4 * gray - neighbors.reduce((a, b) => a + b, 0));
          laplacian.push(laplacianValue);
        }
      }

      // Calculate variance
      const mean = laplacian.reduce((a, b) => a + b, 0) / laplacian.length;
      variance =
        laplacian.reduce((acc, val) => acc + Math.pow(val - mean, 2), 0) / laplacian.length;

      // Convert to 0-10 score (higher variance = less blur = higher score)
      return Math.min(10, Math.max(0, (variance / 100) * 10));
    } catch (error) {
      return 5; // Default medium score if analysis fails
    }
  }

  /**
   * Load image file as HTMLImageElement
   */
  loadImageElement(file: File): Promise<HTMLImageElement> {
    return new Promise((resolve, reject) => {
      this.ngZone.runOutsideAngular(() => {
        const img = new Image();
        img.onload = () => {
          this.ngZone.run(() => {
            resolve(img);
          });
        };
        img.onerror = () => {
          this.ngZone.run(() => {
            reject(new Error('Failed to load image'));
          });
        };
        img.src = URL.createObjectURL(file);
      });
    });
  }

  /**
   * Get default quality score for error cases
   */
  getDefaultQualityScore(): QualityScore {
    return {
      overall: 1,
      breakdown: {
        faceQuality: 0,
        technical: 0,
        composition: 0,
        fluxCompatibility: 0,
        lighting: 0,
      },
      suggestions: ['Unable to analyze image quality. Please try a different photo.'],
    };
  }
}
