import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class WorkflowStepService {

  constructor() { }

  /**
   * Determines the status of a workflow step based on current progress
   * @param step - The step number to check
   * @param uploadedImages - Number of uploaded images
   * @param uploadedImageThumbnails - Array of uploaded image thumbnails
   * @param generatedPhotosCount - Number of generated photos
   * @param currentStep - Current active step
   * @returns Step status: 'completed', 'active', or 'pending'
   */
  getStepStatus(step: number, uploadedImages: number, uploadedImageThumbnails: any[], generatedPhotosCount: number, currentStep: number): string {
    const hasUploadedImages = uploadedImages > 0 || uploadedImageThumbnails.length > 0;
    
    switch (step) {
      case 1:
        if (hasUploadedImages) {return 'completed';}
        if (currentStep === 1) {return 'active';}
        return 'pending';
      case 2:
        if (hasUploadedImages && generatedPhotosCount === 0) {return 'active';}
        if (generatedPhotosCount > 0) {return 'completed';}
        return 'pending';
      case 3:
        if (generatedPhotosCount > 0) {return 'completed';}
        return 'pending';
      default:
        if (step < currentStep) {return 'completed';}
        if (step === currentStep) {return 'active';}
        return 'pending';
    }
  }

  /**
   * Gets the human-readable status text for a workflow step
   * @param step - The step number to check
   * @param uploadedImages - Number of uploaded images
   * @param uploadedImageThumbnails - Array of uploaded image thumbnails
   * @param generatedPhotosCount - Number of generated photos
   * @param currentStep - Current active step
   * @returns Human-readable status: 'Completed', 'In Progress', or 'Pending'
   */
  getStepStatusText(step: number, uploadedImages: number, uploadedImageThumbnails: any[], generatedPhotosCount: number, currentStep: number): string {
    const status = this.getStepStatus(step, uploadedImages, uploadedImageThumbnails, generatedPhotosCount, currentStep);
    switch (status) {
      case 'completed': return 'Completed';
      case 'active': return 'In Progress';
      default: return 'Pending';
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
  updateCurrentStep(uploadedImages: number, uploadedImageThumbnails: any[], generatedPhotosCount: number, currentStep: number): number {
    if ((uploadedImages > 0 || uploadedImageThumbnails.length > 0) && currentStep === 1) {
      return 2;
    }
    if (generatedPhotosCount > 0 && currentStep === 2) {
      return 3;
    }
    return currentStep;
  }
}