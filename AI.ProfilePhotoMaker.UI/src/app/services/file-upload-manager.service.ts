import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { FaceDetectionService, FaceValidationResult, QualityScore } from './face-detection.service';
import { SelectedFileWithQuality, QualityCheckError, QualityCheckResult, UploadProgress } from '../models/dashboard.types';

@Injectable({
  providedIn: 'root'
})
export class FileUploadManagerService {
  private uploadProgressSubject = new BehaviorSubject<UploadProgress>({ percentage: 0 });
  public uploadProgress$ = this.uploadProgressSubject.asObservable();

  private selectedFilesSubject = new BehaviorSubject<SelectedFileWithQuality[]>([]);
  public selectedFiles$ = this.selectedFilesSubject.asObservable();

  constructor(private faceDetectionService: FaceDetectionService) {}

  async processFiles(files: FileList): Promise<QualityCheckResult> {
    const validFiles: File[] = [];
    const errorFiles: QualityCheckError[] = [];
    const selectedFiles: SelectedFileWithQuality[] = [];

    for (let i = 0; i < files.length; i++) {
      const file = files[i];
      
      // Update progress
      this.uploadProgressSubject.next({
        percentage: Math.round((i / files.length) * 50), // 50% for processing
        currentFile: file.name,
        totalFiles: files.length
      });

      try {
        const qualityResult = await this.validateFile(file);
        
        if (qualityResult.errors.length === 0) {
          validFiles.push(file);
        } else {
          errorFiles.push({
            fileName: file.name,
            file: file,
            errors: qualityResult.errors,
            warnings: qualityResult.warnings,
            faceValidation: qualityResult.faceValidation,
            qualityScore: qualityResult.qualityScore
          });
        }

        selectedFiles.push(qualityResult);
      } catch (error) {
        errorFiles.push({
          fileName: file.name,
          file: file,
          errors: [`Failed to process file: ${error}`]
        });
      }
    }

    this.selectedFilesSubject.next(selectedFiles);
    
    // Complete processing
    this.uploadProgressSubject.next({
      percentage: 100,
      completed: true
    });

    return { validFiles, invalidFiles: errorFiles.map(e => e.file), errors: errorFiles, totalProcessed: files.length };
  }

  private async validateFile(file: File): Promise<SelectedFileWithQuality> {
    const errors: string[] = [];
    const warnings: string[] = [];

    // Basic file validation
    if (!file.type.startsWith('image/')) {
      errors.push('File must be an image');
    }

    if (file.size > 10 * 1024 * 1024) { // 10MB limit
      errors.push('File size must be less than 10MB');
    }

    // Face detection and quality analysis
    let faceValidation: FaceValidationResult | undefined;

    try {
      faceValidation = await this.faceDetectionService.validateImage(file);

      // Validate face requirements
      if (faceValidation && faceValidation.faceCount === 0) {
        errors.push('No face detected in the image');
      } else if (faceValidation && faceValidation.faceCount > 1) {
        errors.push('Multiple faces detected. Please use images with only one person');
      }

      // Quality warnings from faceValidation.qualityScore
      const qualityScore = faceValidation?.qualityScore;
      if (qualityScore && qualityScore.overall < 70) {
        warnings.push('Image quality could be improved for better results');
      }

      if (qualityScore && qualityScore.breakdown.technical < 60) {
        warnings.push('Image appears blurry or low quality. Sharper images produce better results');
      }

      if (qualityScore && qualityScore.breakdown.lighting < 60) {
        warnings.push('Lighting could be improved for better results');
      }

    } catch (error) {
      warnings.push('Could not analyze image quality');
    }

    return {
      file,
      qualityScore: faceValidation?.qualityScore,
      faceValidation,
      errors,
      warnings,
      isValid: errors.length === 0,
      showDetails: false
    };
  }

  clearFiles() {
    this.selectedFilesSubject.next([]);
    this.uploadProgressSubject.next({ percentage: 0 });
  }

  removeFile(fileToRemove: File) {
    const currentFiles = this.selectedFilesSubject.value;
    const updatedFiles = currentFiles.filter(f => f.file !== fileToRemove);
    this.selectedFilesSubject.next(updatedFiles);
  }

  getValidFiles(): File[] {
    return this.selectedFilesSubject.value
      .filter(f => f.isValid)
      .map(f => f.file);
  }

  hasValidFiles(): boolean {
    return this.getValidFiles().length > 0;
  }

  getFileCount(): number {
    return this.selectedFilesSubject.value.length;
  }

  getValidFileCount(): number {
    return this.getValidFiles().length;
  }
}