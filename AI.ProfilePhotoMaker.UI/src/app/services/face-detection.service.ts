import { Injectable } from '@angular/core';
import * as faceapi from 'face-api.js';

export interface QualityScore {
  overall: number; // 1-100
  breakdown: {
    faceQuality: number;
    technical: number;
    composition: number;
    fluxCompatibility: number;
    lighting: number;
  };
  suggestions: string[];
}

export interface FaceValidationResult {
  isValid: boolean;
  faceCount: number;
  bodyType: 'headshot' | 'upper-body' | 'full-body' | 'invalid';
  qualityScore: QualityScore;
  errors: string[];
  warnings: string[];
}

@Injectable({
  providedIn: 'root'
})
export class FaceDetectionService {
  private modelsLoaded = false;
  private modelLoadingPromise: Promise<void> | null = null;

  constructor() {}

  /**
   * Load face-api.js models (lazy loading)
   */
  private async loadModels(): Promise<void> {
    if (this.modelsLoaded) {
      return;
    }

    if (this.modelLoadingPromise) {
      return this.modelLoadingPromise;
    }

    this.modelLoadingPromise = this.doLoadModels();
    return this.modelLoadingPromise;
  }

  private async doLoadModels(): Promise<void> {
    try {
      const MODEL_URL = '/assets/models';
      
      // Load lightweight models for better performance
      // Try to load from CDN if local models are not available
      const modelUrls = [
        MODEL_URL,
        'https://cdn.jsdelivr.net/npm/@vladmandic/face-api/model'
      ];

      let loaded = false;
      for (const url of modelUrls) {
        try {
          await Promise.all([
            faceapi.nets.tinyFaceDetector.loadFromUri(url),
            faceapi.nets.faceLandmark68TinyNet.loadFromUri(url),
            faceapi.nets.faceRecognitionNet.loadFromUri(url),
            faceapi.nets.ssdMobilenetv1.loadFromUri(url)
          ]);
          loaded = true;
          console.log(`Face detection models loaded successfully from: ${url}`);
          break;
        } catch (error) {
          console.warn(`Failed to load models from ${url}:`, error);
        }
      }

      if (!loaded) {
        throw new Error('Failed to load models from any source');
      }

      this.modelsLoaded = true;
    } catch (error) {
      console.error('Failed to load face detection models:', error);
      throw new Error('Failed to initialize face detection');
    }
  }

  /**
   * Validate image for face count, body type, and quality
   */
  async validateImage(file: File): Promise<FaceValidationResult> {
    try {
      await this.loadModels();

      const img = await this.loadImageElement(file);
      const detections = await faceapi
        .detectAllFaces(img, new faceapi.TinyFaceDetectorOptions())
        .withFaceLandmarks(true);

      const faceCount = detections.length;
      const errors: string[] = [];
      const warnings: string[] = [];

      // Check for close-up/cropped photos first
      const isCloseUpPhoto = this.detectCloseUpPhoto(img, detections);

      // Face count validation with improved messaging
      if (faceCount === 0) {
        if (isCloseUpPhoto.isTooTightlyCropped) {
          errors.push('Photo is cropped too tightly. Please use a photo that shows your head and shoulders with some background visible.');
        } else {
          errors.push('No face detected in image. Please upload a clear photo with your face visible.');
        }
      } else if (faceCount > 1) {
        if (isCloseUpPhoto.isExtremeCloseUp) {
          errors.push('Photo is too close. Please include your shoulders and back up from the camera for better results.');
        } else {
          errors.push(`Multiple faces detected (${faceCount}). Please upload photos with only yourself.`);
        }
      }

      // Body type detection
      const bodyType = await this.detectBodyType(img, detections);
      
      // Body type validation with improved messaging
      if (bodyType === 'full-body') {
        errors.push('Full body photo detected. Please upload headshot or upper body photos only.');
      } else if (bodyType === 'invalid') {
        if (isCloseUpPhoto.isExtremeCloseUp || isCloseUpPhoto.isTooTightlyCropped) {
          errors.push('Photo framing issue. Please use a shoulders-up photo with some space around your head.');
        } else {
          errors.push('Unable to determine photo composition. Please upload a clear headshot or upper body photo.');
        }
      } else if (bodyType === 'headshot' && (isCloseUpPhoto.isExtremeCloseUp || isCloseUpPhoto.isTooTightlyCropped)) {
        // Even though it's correctly classified as headshot, reject if it's too close/cropped
        if (isCloseUpPhoto.isExtremeCloseUp) {
          errors.push('Photo is too close. Please include your shoulders and back up from the camera for better results.');
        } else {
          errors.push('Photo is cropped too tightly. Please use a photo that shows your head and shoulders with some background visible.');
        }
      }

      // Quality scoring
      const qualityScore = await this.calculateQualityScore(img, detections, file);

      // Apply heavy penalties for validation failures to ensure rejected photos get low scores
      if (errors.length > 0) {
        // Photo has validation errors - apply significant penalty
        let penalty = 0;
        
        // Multiple faces or no faces
        if (faceCount === 0) {
          penalty = 50; // Cap at ~50
        } else if (faceCount > 1) {
          penalty = 60; // Cap at ~40
        }
        
        // Close-up photos
        if (isCloseUpPhoto.isExtremeCloseUp) {
          penalty = Math.max(penalty, 45); // Cap at ~55
        } else if (isCloseUpPhoto.isTooTightlyCropped) {
          penalty = Math.max(penalty, 35); // Cap at ~65
        }
        
        // Full-body photos  
        if (bodyType === 'full-body') {
          penalty = Math.max(penalty, 50); // Cap at ~50
        }
        
        // Apply the penalty
        qualityScore.overall = Math.max(20, qualityScore.overall - penalty);
        
        // Also reduce face quality score in breakdown
        qualityScore.breakdown.faceQuality = Math.max(0, qualityScore.breakdown.faceQuality - (penalty * 0.6));
      }

      // Quality warnings
      if (qualityScore.overall < 50) {
        warnings.push('Image quality is below recommended standards. Consider uploading a higher quality photo.');
      } else if (qualityScore.overall < 70) {
        warnings.push('Image quality could be improved for better AI results.');
      }

      const isValid = errors.length === 0 && faceCount === 1 && ['headshot', 'upper-body'].includes(bodyType);

      return {
        isValid,
        faceCount,
        bodyType,
        qualityScore,
        errors,
        warnings
      };

    } catch (error) {
      console.error('Face validation error:', error);
      return {
        isValid: false,
        faceCount: 0,
        bodyType: 'invalid',
        qualityScore: this.getDefaultQualityScore(),
        errors: ['Unable to analyze image. Please try a different photo.'],
        warnings: []
      };
    }
  }

  /**
   * Detect if photo is too close-up or tightly cropped
   */
  private detectCloseUpPhoto(img: HTMLImageElement, detections: faceapi.WithFaceLandmarks<any>[]): {
    isExtremeCloseUp: boolean;
    isTooTightlyCropped: boolean;
  } {
    if (detections.length === 0) {
      // No face detected - check if image might be too tightly cropped
      // This is a heuristic: very small images or extreme aspect ratios might indicate cropping issues
      const aspectRatio = img.width / img.height;
      const isTooTightlyCropped = aspectRatio > 2 || aspectRatio < 0.5 || 
                                 (img.width < 100 && img.height < 100);
      
      return {
        isExtremeCloseUp: false,
        isTooTightlyCropped
      };
    }

    if (detections.length === 1) {
      const detection = detections[0];
      const box = detection.detection.box;
      
      // Calculate face metrics
      const faceWidth = box.width;
      const faceHeight = box.height;
      const faceWidthRatio = faceWidth / img.width;
      const faceHeightRatio = faceHeight / img.height;
      const faceArea = faceWidthRatio * faceHeightRatio;
      
      // Check if face touches or exceeds image boundaries (with small margin)
      const margin = 10; // pixels
      const faceLeft = box.x;
      const faceRight = box.x + box.width;
      const faceTop = box.y;
      const faceBottom = box.y + box.height;
      
      const touchesEdges = faceLeft <= margin || 
                          faceRight >= (img.width - margin) || 
                          faceTop <= margin || 
                          faceBottom >= (img.height - margin);
      
      // Extreme close-up indicators
      const isExtremeCloseUp = faceHeightRatio > 0.6 || // Face takes up >60% of image height
                              faceWidthRatio > 0.7 ||   // Face takes up >70% of image width
                              faceArea > 0.45 ||        // Face area >45% of total image
                              touchesEdges;             // Face touches image edges
      
      // Tightly cropped indicators (face is large but not extreme)
      const isTooTightlyCropped = !isExtremeCloseUp && 
                                 (faceHeightRatio > 0.4 || faceWidthRatio > 0.5) &&
                                 touchesEdges;
      
      return {
        isExtremeCloseUp,
        isTooTightlyCropped
      };
    }

    // Multiple detections - might be a close-up causing false multiple face detection
    if (detections.length > 1) {
      // Check if any detected face is very large (indicating close-up)
      const hasLargeFace = detections.some(detection => {
        const box = detection.detection.box;
        const faceHeightRatio = box.height / img.height;
        const faceWidthRatio = box.width / img.width;
        return faceHeightRatio > 0.4 || faceWidthRatio > 0.5;
      });
      
      return {
        isExtremeCloseUp: hasLargeFace,
        isTooTightlyCropped: false
      };
    }

    return {
      isExtremeCloseUp: false,
      isTooTightlyCropped: false
    };
  }

  /**
   * Detect body type based on face position and image composition
   */
  private async detectBodyType(img: HTMLImageElement, detections: faceapi.WithFaceLandmarks<any>[]): Promise<'headshot' | 'upper-body' | 'full-body' | 'invalid'> {
    if (detections.length === 0) {
      return 'invalid';
    }

    const detection = detections[0];
    const imgHeight = img.height;
    const imgWidth = img.width;

    // Get face bounding box
    const box = detection.detection.box;
    const faceTop = box.y;
    const faceBottom = box.y + box.height;
    const faceCenter = box.y + (box.height / 2);
    const faceWidth = box.width;
    const faceHeight = box.height;

    // Calculate relative positions
    const faceTopRatio = faceTop / imgHeight;
    const faceCenterRatio = faceCenter / imgHeight;
    const faceBottomRatio = faceBottom / imgHeight;
    const faceWidthRatio = faceWidth / imgWidth;
    const faceHeightRatio = faceHeight / imgHeight;

    // First check if this is a close-up photo that should not be classified as full-body
    const closeUpCheck = this.detectCloseUpPhoto(img, detections);
    
    // If it's an extreme close-up or tightly cropped, it cannot be full-body
    if (closeUpCheck.isExtremeCloseUp) {
      return 'headshot'; // Very close photos are headshots by definition
    }
    
    if (closeUpCheck.isTooTightlyCropped) {
      return 'headshot'; // Tightly cropped photos are also headshots
    }

    // For photos with very large faces (>50% of image height), they're likely headshots
    if (faceHeightRatio > 0.5) {
      return 'headshot';
    }

    // Enhanced body type detection with multiple criteria
    const bodyTypeScore = await this.calculateBodyTypeScore(img, detection, {
      faceTopRatio,
      faceCenterRatio,
      faceBottomRatio,
      faceWidthRatio,
      faceHeightRatio,
      imgWidth,
      imgHeight
    });

    // Determine body type based on comprehensive scoring
    if (bodyTypeScore.isFullBody) {
      return 'full-body';
    } else if (bodyTypeScore.isHeadshot) {
      return 'headshot';
    } else if (bodyTypeScore.isUpperBody) {
      return 'upper-body';
    } else {
      return 'headshot'; // Default to headshot for ambiguous cases
    }
  }

  /**
   * Calculate comprehensive body type score using multiple detection methods
   */
  private async calculateBodyTypeScore(img: HTMLImageElement, detection: any, metrics: any): Promise<{
    isHeadshot: boolean;
    isUpperBody: boolean;
    isFullBody: boolean;
    confidence: number;
  }> {
    let headshotScore = 0;
    let upperBodyScore = 0;
    let fullBodyScore = 0;

    // 1. Face Position Analysis (40% weight)
    if (metrics.faceCenterRatio <= 0.25) {
      headshotScore += 40;
    } else if (metrics.faceCenterRatio <= 0.4) {
      upperBodyScore += 30;
      headshotScore += 10;
    } else if (metrics.faceCenterRatio <= 0.6) {
      upperBodyScore += 40;
    } else {
      fullBodyScore += 40;
    }

    // 2. Face Size Analysis (25% weight)
    // Larger faces typically indicate closer crops (headshots)
    if (metrics.faceHeightRatio >= 0.3) {
      headshotScore += 25;
    } else if (metrics.faceHeightRatio >= 0.15) {
      upperBodyScore += 25;
    } else if (metrics.faceHeightRatio >= 0.08) {
      upperBodyScore += 15;
      fullBodyScore += 10;
    } else {
      fullBodyScore += 25;
    }

    // 3. Image Aspect Ratio Analysis (15% weight)
    const aspectRatio = metrics.imgWidth / metrics.imgHeight;
    if (aspectRatio >= 0.7 && aspectRatio <= 1.3) {
      // Square-ish images often headshots or upper body
      headshotScore += 10;
      upperBodyScore += 5;
    } else if (aspectRatio < 0.7) {
      // Tall images might be full body
      fullBodyScore += 15;
    }

    // 4. Face-to-Image Ratio Analysis (10% weight)
    const faceArea = metrics.faceWidthRatio * metrics.faceHeightRatio;
    if (faceArea >= 0.15) {
      headshotScore += 10;
    } else if (faceArea >= 0.05) {
      upperBodyScore += 10;
    } else {
      fullBodyScore += 10;
    }

    // 5. Edge Detection for Body Boundaries (10% weight)
    try {
      const bodyBoundaryScore = await this.detectBodyBoundaries(img, detection);
      if (bodyBoundaryScore.hasLowerBodyContent) {
        fullBodyScore += 10;
      } else if (bodyBoundaryScore.hasShoulderContent) {
        upperBodyScore += 10;
      } else {
        headshotScore += 10;
      }
    } catch (error) {
      // Edge detection failed, use position fallback
      if (metrics.faceCenterRatio > 0.5) {
        fullBodyScore += 5;
      } else {
        upperBodyScore += 5;
      }
    }

    // Determine final classification
    const maxScore = Math.max(headshotScore, upperBodyScore, fullBodyScore);
    const confidence = maxScore / 100;

    return {
      isHeadshot: headshotScore === maxScore && headshotScore >= 50,
      isUpperBody: upperBodyScore === maxScore && upperBodyScore >= 40,
      isFullBody: fullBodyScore === maxScore && fullBodyScore >= 30,
      confidence
    };
  }

  /**
   * Detect body boundaries using edge detection
   */
  private async detectBodyBoundaries(img: HTMLImageElement, detection: any): Promise<{
    hasLowerBodyContent: boolean;
    hasShoulderContent: boolean;
    confidence: number;
  }> {
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d')!;
    
    // Sample analysis region below the face
    const sampleHeight = Math.min(200, img.height);
    const sampleWidth = Math.min(200, img.width);
    canvas.width = sampleWidth;
    canvas.height = sampleHeight;
    
    // Focus on area below the face
    const faceBottom = detection.detection.box.y + detection.detection.box.height;
    const analysisStartY = Math.min(faceBottom, img.height * 0.3);
    
    ctx.drawImage(img, 0, analysisStartY, img.width, img.height - analysisStartY, 
                  0, 0, sampleWidth, sampleHeight);

    try {
      const imageData = ctx.getImageData(0, 0, sampleWidth, sampleHeight);
      const data = imageData.data;

      // Analyze content below face for body indicators
      let edgeCount = 0;
      let verticalEdges = 0;
      let horizontalEdges = 0;

      // Simple edge detection
      for (let y = 1; y < sampleHeight - 1; y++) {
        for (let x = 1; x < sampleWidth - 1; x++) {
          const idx = (y * sampleWidth + x) * 4;
          const current = (data[idx] + data[idx + 1] + data[idx + 2]) / 3;
          
          // Check horizontal edges (shoulders, torso)
          const right = (data[idx + 4] + data[idx + 5] + data[idx + 6]) / 3;
          const left = (data[idx - 4] + data[idx - 3] + data[idx - 2]) / 3;
          
          // Check vertical edges (body outline)
          const below = (data[(y + 1) * sampleWidth * 4 + x * 4] + 
                        data[(y + 1) * sampleWidth * 4 + x * 4 + 1] + 
                        data[(y + 1) * sampleWidth * 4 + x * 4 + 2]) / 3;
          
          if (Math.abs(current - right) > 30 || Math.abs(current - left) > 30) {
            verticalEdges++;
          }
          
          if (Math.abs(current - below) > 30) {
            horizontalEdges++;
          }
          
          if (Math.abs(current - right) > 20 || Math.abs(current - below) > 20) {
            edgeCount++;
          }
        }
      }

      const totalPixels = sampleWidth * sampleHeight;
      const edgeRatio = edgeCount / totalPixels;
      const verticalRatio = verticalEdges / totalPixels;
      const horizontalRatio = horizontalEdges / totalPixels;

      // Determine body content based on edge patterns
      const hasLowerBodyContent = edgeRatio > 0.15 && verticalRatio > 0.08;
      const hasShoulderContent = horizontalRatio > 0.05 && edgeRatio > 0.08;

      return {
        hasLowerBodyContent,
        hasShoulderContent,
        confidence: Math.min(edgeRatio * 2, 1)
      };

    } catch (error) {
      return {
        hasLowerBodyContent: false,
        hasShoulderContent: false,
        confidence: 0
      };
    }
  }

  /**
   * Calculate comprehensive quality score
   */
  private async calculateQualityScore(img: HTMLImageElement, detections: faceapi.WithFaceLandmarks<any>[], file: File): Promise<QualityScore> {
    const scores = {
      faceQuality: 0,
      technical: 0,
      composition: 0,
      fluxCompatibility: 0,
      lighting: 0
    };

    const suggestions: string[] = [];

    // 1. Face Quality (30 points)
    if (detections.length === 1) {
      const detection = detections[0];
      const confidence = detection.detection.score;
      
      scores.faceQuality = Math.min(30, confidence * 30);
      
      if (confidence < 0.7) {
        suggestions.push('Face is not clearly visible. Try better lighting or camera angle.');
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

    // 6. Check for close-up photo issues and add suggestions
    const closeUpCheck = this.detectCloseUpPhoto(img, detections);
    if (closeUpCheck.isExtremeCloseUp) {
      suggestions.push('Photo is too close. Try backing up from the camera to include your shoulders.');
      // Reduce composition score for extreme close-ups
      scores.composition = Math.max(0, scores.composition - 5);
    } else if (closeUpCheck.isTooTightlyCropped) {
      suggestions.push('Ensure your entire head is visible with space on all sides of the frame.');
      // Reduce composition score for tight cropping
      scores.composition = Math.max(0, scores.composition - 3);
    }

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
      suggestions: suggestions.filter(s => s.length > 0)
    };
  }

  /**
   * Assess technical image quality
   */
  private async assessTechnicalQuality(img: HTMLImageElement, file: File): Promise<{score: number, suggestions: string[]}> {
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
  private assessComposition(img: HTMLImageElement, detections: faceapi.WithFaceLandmarks<any>[]): {score: number, suggestions: string[]} {
    const suggestions: string[] = [];
    let score = 0;

    if (detections.length === 1) {
      const detection = detections[0];
      const box = detection.detection.box;
      
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
  private async assessFluxCompatibility(img: HTMLImageElement): Promise<{score: number, suggestions: string[]}> {
    const suggestions: string[] = [];
    let score = 10; // Start optimistic

    // Check for common FLUX negative prompt issues
    // This is a simplified assessment - could be enhanced with ML models
    
    // Basic image quality indicators
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d')!;
    canvas.width = img.width;
    canvas.height = img.height;
    ctx.drawImage(img, 0, 0);
    
    try {
      const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
      const data = imageData.data;
      
      // Check for overexposure/underexposure
      let brightPixels = 0;
      let darkPixels = 0;
      
      for (let i = 0; i < data.length; i += 4) {
        const avg = (data[i] + data[i + 1] + data[i + 2]) / 3;
        if (avg > 240) brightPixels++;
        if (avg < 15) darkPixels++;
      }
      
      const totalPixels = data.length / 4;
      const brightRatio = brightPixels / totalPixels;
      const darkRatio = darkPixels / totalPixels;
      
      if (brightRatio > 0.3) {
        score -= 3;
        suggestions.push('Image appears overexposed. Reduce lighting or use better camera settings.');
      }
      
      if (darkRatio > 0.3) {
        score -= 3;
        suggestions.push('Image appears underexposed. Increase lighting for better visibility.');
      }
      
    } catch (error) {
      // Canvas analysis failed, use default score
    }

    return { score: Math.max(0, score), suggestions };
  }

  /**
   * Assess lighting quality
   */
  private assessLighting(img: HTMLImageElement): {score: number, suggestions: string[]} {
    const suggestions: string[] = [];
    let score = 8; // Start with good score

    // Basic lighting assessment using canvas analysis
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d')!;
    canvas.width = Math.min(img.width, 200); // Sample for performance
    canvas.height = Math.min(img.height, 200);
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
    
    try {
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
    const ctx = canvas.getContext('2d')!;
    
    // Sample size for performance
    const sampleSize = 100;
    canvas.width = sampleSize;
    canvas.height = sampleSize;
    
    ctx.drawImage(img, 0, 0, sampleSize, sampleSize);
    
    try {
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
            (data[(y-1) * sampleSize * 4 + x * 4] + data[(y-1) * sampleSize * 4 + x * 4 + 1] + data[(y-1) * sampleSize * 4 + x * 4 + 2]) / 3,
            (data[(y+1) * sampleSize * 4 + x * 4] + data[(y+1) * sampleSize * 4 + x * 4 + 1] + data[(y+1) * sampleSize * 4 + x * 4 + 2]) / 3,
            (data[y * sampleSize * 4 + (x-1) * 4] + data[y * sampleSize * 4 + (x-1) * 4 + 1] + data[y * sampleSize * 4 + (x-1) * 4 + 2]) / 3,
            (data[y * sampleSize * 4 + (x+1) * 4] + data[y * sampleSize * 4 + (x+1) * 4 + 1] + data[y * sampleSize * 4 + (x+1) * 4 + 2]) / 3
          ];
          
          const laplacianValue = Math.abs(4 * gray - neighbors.reduce((a, b) => a + b, 0));
          laplacian.push(laplacianValue);
        }
      }
      
      // Calculate variance
      const mean = laplacian.reduce((a, b) => a + b, 0) / laplacian.length;
      variance = laplacian.reduce((acc, val) => acc + Math.pow(val - mean, 2), 0) / laplacian.length;
      
      // Convert to 0-10 score (higher variance = less blur = higher score)
      return Math.min(10, Math.max(0, (variance / 100) * 10));
      
    } catch (error) {
      return 5; // Default medium score if analysis fails
    }
  }

  /**
   * Load image file as HTMLImageElement
   */
  private loadImageElement(file: File): Promise<HTMLImageElement> {
    return new Promise((resolve, reject) => {
      const img = new Image();
      img.onload = () => resolve(img);
      img.onerror = reject;
      img.src = URL.createObjectURL(file);
    });
  }

  /**
   * Get default quality score for error cases
   */
  private getDefaultQualityScore(): QualityScore {
    return {
      overall: 1,
      breakdown: {
        faceQuality: 0,
        technical: 0,
        composition: 0,
        fluxCompatibility: 0,
        lighting: 0
      },
      suggestions: ['Unable to analyze image quality. Please try a different photo.']
    };
  }
}