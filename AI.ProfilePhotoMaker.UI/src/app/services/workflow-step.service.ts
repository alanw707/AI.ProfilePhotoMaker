import { Injectable } from '@angular/core';

export interface ImageThumbnail {
  id?: string;
  name?: string;
  url?: string;
  file?: File;
}

@Injectable({
  providedIn: 'root',
})
export class WorkflowStepService {
  // Service doesn't need constructor - Angular will provide singleton instance

  /**
   * Determines the status of a workflow step based on current progress
   * @param step - The step number to check
   * @param uploadedImages - Number of uploaded images
   * @param uploadedImageThumbnails - Array of uploaded image thumbnails
   * @param generatedPhotosCount - Number of generated photos
   * @param currentStep - Current active step
   * @returns Step status: 'completed', 'active', or 'pending'
   */
  getStepStatus(
    step: number,
    uploadedImages: number,
    uploadedImageThumbnails: ImageThumbnail[],
    generatedPhotosCount: number,
    currentStep: number
  ): string {
    const hasUploadedImages = uploadedImages > 0 || uploadedImageThumbnails.length > 0;

    switch (step) {
      case 1:
        return this._getStep1Status(hasUploadedImages, currentStep);
      case 2:
        return this._getStep2Status(hasUploadedImages, generatedPhotosCount);
      case 3:
        return this._getStep3Status(generatedPhotosCount);
      default:
        return this._getDefaultStepStatus(step, currentStep);
    }
  }

  private _getStep1Status(hasUploadedImages: boolean, currentStep: number): string {
    if (hasUploadedImages) {
      return 'completed';
    }
    return currentStep === 1 ? 'active' : 'pending';
  }

  private _getStep2Status(hasUploadedImages: boolean, generatedPhotosCount: number): string {
    if (hasUploadedImages && generatedPhotosCount === 0) {
      return 'active';
    }
    return generatedPhotosCount > 0 ? 'completed' : 'pending';
  }

  private _getStep3Status(generatedPhotosCount: number): string {
    return generatedPhotosCount > 0 ? 'completed' : 'pending';
  }

  private _getDefaultStepStatus(step: number, currentStep: number): string {
    if (step < currentStep) {
      return 'completed';
    }
    return step === currentStep ? 'active' : 'pending';
  }

  /**
   * Gets the human-readable status text for a workflow step
   * @param step - The step number to check
   * @param uploadedImages - Number of uploaded images
   * @param uploadedImageThumbnails - Array of uploaded image thumbnails
   * @param generatedPhotosCount - Number of generated photos
   * @param currentStep - Current active step
   * @returns Human-readable status: 'Done', 'Active', or 'Pending'
   */
  getStepStatusText(
    step: number,
    uploadedImages: number,
    uploadedImageThumbnails: ImageThumbnail[],
    generatedPhotosCount: number,
    currentStep: number
  ): string {
    const status = this.getStepStatus(
      step,
      uploadedImages,
      uploadedImageThumbnails,
      generatedPhotosCount,
      currentStep
    );
    switch (status) {
      case 'completed':
        return 'Done';
      case 'active':
        return 'Active';
      default:
        return 'Pending';
    }
  }

  /**
   * Updates the current step based on workflow progress
   * @param uploadedImages - Number of uploaded images
   * @param uploadedImageThumbnails - Array of uploaded image thumbnails
   * @param generatedPhotosCount - Number of generated photos
   * @param currentStep - Current active step
   * @returns Updated current step number
   */
  updateCurrentStep(
    uploadedImages: number,
    uploadedImageThumbnails: ImageThumbnail[],
    generatedPhotosCount: number,
    currentStep: number
  ): number {
    if ((uploadedImages > 0 || uploadedImageThumbnails.length > 0) && currentStep === 1) {
      return 2;
    }
    if (generatedPhotosCount > 0 && currentStep === 2) {
      return 3;
    }
    return currentStep;
  }
}
