import { Injectable, NgZone } from '@angular/core';
import * as faceapi from 'face-api.js';
import { ModelLoaderService } from './model-loader.service';
import { ImageQualityService } from './image-quality.service';
import { FaceValidationResult, IFaceDetectionService } from '../interfaces/service.interfaces';

@Injectable({
  providedIn: 'root',
})
export class FaceDetectionService implements IFaceDetectionService {
  constructor(
    private ngZone: NgZone,
    private modelLoader: ModelLoaderService,
    private imageQuality: ImageQualityService
  ) {}

  /**
   * Validate image for face count, body type, and quality
   */
  async validateImage(file: File): Promise<FaceValidationResult> {
    try {
      await this.modelLoader.loadModels();

      const img = await this.imageQuality.loadImageElement(file);
      const detections = await this.ngZone.runOutsideAngular(async () => {
        return faceapi
          .detectAllFaces(img, new faceapi.TinyFaceDetectorOptions())
          .withFaceLandmarks(true);
      });

      const faceCount = detections.length;
      const errors: string[] = [];
      const warnings: string[] = [];

      // Check for close-up/cropped photos first
      const isCloseUpPhoto = this.detectCloseUpPhoto(img, detections);

      // Face count validation with improved messaging
      if (faceCount === 0) {
        if (isCloseUpPhoto.isTooTightlyCropped) {
          errors.push(
            'Photo is cropped too tightly. Please use a photo that shows your head and shoulders with some background visible.'
          );
        } else {
          errors.push(
            'No face detected in image. Please upload a clear photo with your face visible.'
          );
        }
      } else if (faceCount > 1) {
        if (isCloseUpPhoto.isExtremeCloseUp) {
          errors.push(
            'Photo is too close. Please include your shoulders and back up from the camera for better results.'
          );
        } else {
          errors.push(
            `Multiple faces detected (${faceCount}). Please upload photos with only yourself.`
          );
        }
      }

      // Body type detection
      const bodyType = await this.detectBodyType(img, detections);

      // Body type validation with improved messaging
      if (bodyType === 'full-body') {
        errors.push('Full body photo detected. Please upload headshot or upper body photos only.');
      } else if (bodyType === 'invalid') {
        if (isCloseUpPhoto.isExtremeCloseUp || isCloseUpPhoto.isTooTightlyCropped) {
          errors.push(
            'Photo framing issue. Please use a shoulders-up photo with some space around your head.'
          );
        } else {
          errors.push(
            'Unable to determine photo composition. Please upload a clear headshot or upper body photo.'
          );
        }
      } else if (
        bodyType === 'headshot' &&
        (isCloseUpPhoto.isExtremeCloseUp || isCloseUpPhoto.isTooTightlyCropped)
      ) {
        // Even though it's correctly classified as headshot, reject if it's too close/cropped
        if (isCloseUpPhoto.isExtremeCloseUp) {
          errors.push(
            'Photo is too close. Please include your shoulders and back up from the camera for better results.'
          );
        } else {
          errors.push(
            'Photo is cropped too tightly. Please use a photo that shows your head and shoulders with some background visible.'
          );
        }
      }

      // Quality scoring
      const qualityScore = await this.imageQuality.calculateQualityScore(img, detections, file);

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
        qualityScore.breakdown.faceQuality = Math.max(
          0,
          qualityScore.breakdown.faceQuality - penalty * 0.6
        );
      }

      // Quality warnings
      if (qualityScore.overall < 50) {
        warnings.push(
          'Image quality is below recommended standards. Consider uploading a higher quality photo.'
        );
      } else if (qualityScore.overall < 70) {
        warnings.push('Image quality could be improved for better AI results.');
      }

      const isValid =
        errors.length === 0 && faceCount === 1 && ['headshot', 'upper-body'].includes(bodyType);

      return {
        isValid,
        faceCount,
        bodyType,
        qualityScore,
        errors,
        warnings,
      };
    } catch (error) {
      console.error('Face validation error:', error);
      return {
        isValid: false,
        faceCount: 0,
        bodyType: 'invalid',
        qualityScore: this.imageQuality.getDefaultQualityScore(),
        errors: ['Unable to analyze image. Please try a different photo.'],
        warnings: [],
      };
    }
  }

  /**
   * Detect if photo is too close-up or tightly cropped
   */
  private detectCloseUpPhoto(
    img: HTMLImageElement,
    detections: faceapi.WithFaceLandmarks<any>[]
  ): {
    isExtremeCloseUp: boolean;
    isTooTightlyCropped: boolean;
  } {
    if (detections.length === 0) {
      // No face detected - check if image might be too tightly cropped
      // This is a heuristic: very small images or extreme aspect ratios might indicate cropping issues
      const aspectRatio = img.width / img.height;
      const isTooTightlyCropped =
        aspectRatio > 2 || aspectRatio < 0.5 || (img.width < 100 && img.height < 100);

      return {
        isExtremeCloseUp: false,
        isTooTightlyCropped,
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

      const touchesEdges =
        faceLeft <= margin ||
        faceRight >= img.width - margin ||
        faceTop <= margin ||
        faceBottom >= img.height - margin;

      // Extreme close-up indicators
      const isExtremeCloseUp =
        faceHeightRatio > 0.6 || // Face takes up >60% of image height
        faceWidthRatio > 0.7 || // Face takes up >70% of image width
        faceArea > 0.45 || // Face area >45% of total image
        touchesEdges; // Face touches image edges

      // Tightly cropped indicators (face is large but not extreme)
      const isTooTightlyCropped =
        !isExtremeCloseUp && (faceHeightRatio > 0.4 || faceWidthRatio > 0.5) && touchesEdges;

      return {
        isExtremeCloseUp,
        isTooTightlyCropped,
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
        isTooTightlyCropped: false,
      };
    }

    return {
      isExtremeCloseUp: false,
      isTooTightlyCropped: false,
    };
  }

  /**
   * Detect body type based on face position and image composition
   */
  private async detectBodyType(
    img: HTMLImageElement,
    detections: faceapi.WithFaceLandmarks<any>[]
  ): Promise<'headshot' | 'upper-body' | 'full-body' | 'invalid'> {
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
    const faceCenter = box.y + box.height / 2;
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
      imgHeight,
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
  private async calculateBodyTypeScore(
    img: HTMLImageElement,
    detection: any,
    metrics: any
  ): Promise<{
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
      confidence,
    };
  }

  /**
   * Detect body boundaries using edge detection
   */
  private async detectBodyBoundaries(
    img: HTMLImageElement,
    detection: any
  ): Promise<{
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

    ctx.drawImage(
      img,
      0,
      analysisStartY,
      img.width,
      img.height - analysisStartY,
      0,
      0,
      sampleWidth,
      sampleHeight
    );

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
          const below =
            (data[(y + 1) * sampleWidth * 4 + x * 4] +
              data[(y + 1) * sampleWidth * 4 + x * 4 + 1] +
              data[(y + 1) * sampleWidth * 4 + x * 4 + 2]) /
            3;

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
        confidence: Math.min(edgeRatio * 2, 1),
      };
    } catch (error) {
      return {
        hasLowerBodyContent: false,
        hasShoulderContent: false,
        confidence: 0,
      };
    }
  }
}
