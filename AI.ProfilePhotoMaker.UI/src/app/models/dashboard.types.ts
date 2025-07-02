import { FaceValidationResult, QualityScore } from '../services/face-detection.service';

export interface GeneratedPhoto {
  id: string;
  url: string;
  style: string;
  createdAt: Date;
}

export interface QualityCheckError {
  fileName: string;
  file: File;
  errors: string[];
  warnings?: string[];
  faceValidation?: FaceValidationResult;
  qualityScore?: QualityScore;
}

export interface SelectedFileWithQuality {
  file: File;
  qualityScore?: QualityScore;
  faceValidation?: FaceValidationResult;
  errors: string[];
  warnings: string[];
  isValid: boolean;
  showDetails?: boolean; // For expandable details UI state
}

export interface QualityCheckResult {
  validFiles: File[];
  errorFiles: QualityCheckError[];
}

export interface UploadProgress {
  percentage: number;
  currentFile?: string;
  totalFiles?: number;
  completed?: boolean;
  error?: string;
}

export interface TrainingStatus {
  isTraining: boolean;
  progress: number;
  status: string;
  modelId?: string;
  error?: string;
  estimatedTimeRemaining?: number;
}

export interface GenerationStatus {
  isGenerating: boolean;
  progress: number;
  status: string;
  estimatedTimeRemaining?: number;
  completedImages?: number;
  totalImages?: number;
  error?: string;
}