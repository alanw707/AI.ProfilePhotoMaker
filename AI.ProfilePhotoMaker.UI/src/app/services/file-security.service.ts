import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';

export interface FileSecurityValidation {
  isValid: boolean;
  securityIssues: string[];
  sanitizedFile?: File;
  riskLevel: 'low' | 'medium' | 'high' | 'critical';
}

export interface SecurityConfig {
  maxFileSize: number; // bytes
  allowedMimeTypes: string[];
  allowedExtensions: string[];
  maxFilesPerUpload: number;
  maxTotalSize: number;
  scanForMaliciousContent: boolean;
  validateFileHeaders: boolean;
}

/**
 * Service responsible for file upload security validation and sanitization
 * Implements OWASP recommendations for secure file uploads
 */
@Injectable({
  providedIn: 'root',
})
export class FileSecurityService {
  private readonly DEFAULT_CONFIG: SecurityConfig = {
    maxFileSize: 10 * 1024 * 1024, // 10MB
    allowedMimeTypes: [
      'image/jpeg',
      'image/jpg',
      'image/png',
      'image/webp',
      'image/heic',
      'image/heif',
    ],
    allowedExtensions: ['.jpg', '.jpeg', '.png', '.webp', '.heic', '.heif'],
    maxFilesPerUpload: 20,
    maxTotalSize: 100 * 1024 * 1024, // 100MB total
    scanForMaliciousContent: true,
    validateFileHeaders: true,
  };

  private config: SecurityConfig = { ...this.DEFAULT_CONFIG };

  /**
   * Validate a single file against security policies
   */
  validateFile(file: File): Observable<FileSecurityValidation> {
    const validation: FileSecurityValidation = {
      isValid: true,
      securityIssues: [],
      riskLevel: 'low',
    };

    try {
      // 1. File size validation
      if (file.size > this.config.maxFileSize) {
        validation.isValid = false;
        validation.securityIssues.push(
          `File size ${this.formatFileSize(file.size)} exceeds maximum allowed size ${this.formatFileSize(this.config.maxFileSize)}`
        );
        validation.riskLevel = 'medium';
      }

      // 2. Empty file check
      if (file.size === 0) {
        validation.isValid = false;
        validation.securityIssues.push('Empty files are not allowed');
        validation.riskLevel = 'high';
      }

      // 3. MIME type validation
      if (!this.config.allowedMimeTypes.includes(file.type)) {
        validation.isValid = false;
        validation.securityIssues.push(`File type '${file.type}' is not allowed`);
        validation.riskLevel = 'high';
      }

      // 4. File extension validation
      const extension = this.getFileExtension(file.name).toLowerCase();
      if (!this.config.allowedExtensions.includes(extension)) {
        validation.isValid = false;
        validation.securityIssues.push(`File extension '${extension}' is not allowed`);
        validation.riskLevel = 'high';
      }

      // 5. Filename validation (prevent path traversal)
      if (this.containsPathTraversal(file.name)) {
        validation.isValid = false;
        validation.securityIssues.push('Filename contains potentially dangerous path characters');
        validation.riskLevel = 'critical';
      }

      // 6. Dangerous filename patterns
      if (this.hasDangerousFilename(file.name)) {
        validation.isValid = false;
        validation.securityIssues.push('Filename matches dangerous pattern');
        validation.riskLevel = 'critical';
      }

      // 7. MIME type vs extension mismatch
      if (!this.validateMimeExtensionMatch(file.type, extension)) {
        validation.isValid = false;
        validation.securityIssues.push('File type and extension do not match');
        validation.riskLevel = 'high';
      }

      return of(validation);
    } catch (error) {
      validation.isValid = false;
      validation.securityIssues.push('File validation failed due to internal error');
      validation.riskLevel = 'critical';
      return of(validation);
    }
  }

  /**
   * Validate multiple files as a batch
   */
  validateFiles(files: File[]): Observable<{
    isValid: boolean;
    fileValidations: FileSecurityValidation[];
    batchIssues: string[];
    totalSize: number;
  }> {
    if (files.length === 0) {
      return of({
        isValid: false,
        fileValidations: [],
        batchIssues: ['No files provided for validation'],
        totalSize: 0,
      });
    }

    // Check batch limits
    const batchIssues: string[] = [];
    let batchValid = true;

    if (files.length > this.config.maxFilesPerUpload) {
      batchValid = false;
      batchIssues.push(
        `Too many files: ${files.length} exceeds maximum of ${this.config.maxFilesPerUpload}`
      );
    }

    const totalSize = files.reduce((sum, file) => sum + file.size, 0);
    if (totalSize > this.config.maxTotalSize) {
      batchValid = false;
      batchIssues.push(
        `Total size ${this.formatFileSize(totalSize)} exceeds maximum ${this.formatFileSize(this.config.maxTotalSize)}`
      );
    }

    // Validate individual files
    const fileValidations: FileSecurityValidation[] = [];
    for (const file of files) {
      const validation = this.validateFileSynchronously(file);
      fileValidations.push(validation);
      if (!validation.isValid) {
        batchValid = false;
      }
    }

    return of({
      isValid: batchValid,
      fileValidations,
      batchIssues,
      totalSize,
    });
  }

  /**
   * Sanitize filename to prevent security issues
   */
  sanitizeFilename(filename: string): string {
    // Remove path traversal attempts
    let sanitized = filename.replace(/[\/\\\.]{2,}/g, '');

    // Remove dangerous characters
    sanitized = sanitized.replace(/[<>:"|?*\x00-\x1f]/g, '_');

    // Limit length
    if (sanitized.length > 255) {
      const extension = this.getFileExtension(sanitized);
      const nameOnly = sanitized.substring(0, sanitized.lastIndexOf('.'));
      sanitized = nameOnly.substring(0, 255 - extension.length) + extension;
    }

    // Ensure it doesn't start with dangerous patterns
    if (/^(con|prn|aux|nul|com[1-9]|lpt[1-9])$/i.test(sanitized.split('.')[0])) {
      sanitized = 'file_' + sanitized;
    }

    return sanitized;
  }

  /**
   * Advanced file header validation to detect file type spoofing
   */
  async validateFileHeaders(
    file: File
  ): Promise<{ isValid: boolean; detectedType: string; issues: string[] }> {
    if (!this.config.validateFileHeaders) {
      return { isValid: true, detectedType: file.type, issues: [] };
    }

    try {
      const buffer = await this.readFileBuffer(file, 12); // Read first 12 bytes for magic numbers
      const signature = Array.from(new Uint8Array(buffer))
        .map(byte => byte.toString(16).padStart(2, '0'))
        .join('');

      const detectedType = this.detectFileTypeFromSignature(signature);
      const issues: string[] = [];

      if (detectedType && detectedType !== file.type) {
        issues.push(`File header indicates ${detectedType} but MIME type is ${file.type}`);
        return { isValid: false, detectedType, issues };
      }

      return { isValid: true, detectedType: detectedType || file.type, issues: [] };
    } catch (error) {
      return {
        isValid: false,
        detectedType: 'unknown',
        issues: ['Failed to read file headers'],
      };
    }
  }

  /**
   * Update security configuration
   */
  updateConfig(newConfig: Partial<SecurityConfig>): void {
    this.config = { ...this.config, ...newConfig };
    console.log('🔒 File security configuration updated:', this.config);
  }

  /**
   * Get current security configuration
   */
  getConfig(): Readonly<SecurityConfig> {
    return { ...this.config };
  }

  // Private helper methods

  private validateFileSynchronously(file: File): FileSecurityValidation {
    const validation: FileSecurityValidation = {
      isValid: true,
      securityIssues: [],
      riskLevel: 'low',
    };

    // File size validation
    if (file.size > this.config.maxFileSize) {
      validation.isValid = false;
      validation.securityIssues.push('File too large');
      validation.riskLevel = 'medium';
    }

    // MIME type validation
    if (!this.config.allowedMimeTypes.includes(file.type)) {
      validation.isValid = false;
      validation.securityIssues.push('Invalid file type');
      validation.riskLevel = 'high';
    }

    // Extension validation
    const extension = this.getFileExtension(file.name).toLowerCase();
    if (!this.config.allowedExtensions.includes(extension)) {
      validation.isValid = false;
      validation.securityIssues.push('Invalid file extension');
      validation.riskLevel = 'high';
    }

    return validation;
  }

  private getFileExtension(filename: string): string {
    const lastDot = filename.lastIndexOf('.');
    return lastDot >= 0 ? filename.substring(lastDot) : '';
  }

  private containsPathTraversal(filename: string): boolean {
    return /\.\.\/|\.\.\\|\/\.\.|\\\.\./.test(filename);
  }

  private hasDangerousFilename(filename: string): boolean {
    const dangerousPatterns = [
      /^\./, // Hidden files
      /\.(exe|bat|cmd|scr|pif|com|dll|vbs|js|jar|sh)$/i, // Executable extensions
      /^(thumbs\.db|desktop\.ini|\$recycle\.bin)$/i, // System files
    ];

    return dangerousPatterns.some(pattern => pattern.test(filename));
  }

  private validateMimeExtensionMatch(mimeType: string, extension: string): boolean {
    const mimeExtensionMap: { [key: string]: string[] } = {
      'image/jpeg': ['.jpg', '.jpeg'],
      'image/png': ['.png'],
      'image/webp': ['.webp'],
      'image/heic': ['.heic'],
      'image/heif': ['.heif'],
    };

    const validExtensions = mimeExtensionMap[mimeType];
    return validExtensions ? validExtensions.includes(extension) : false;
  }

  private async readFileBuffer(file: File, bytes: number): Promise<ArrayBuffer> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as ArrayBuffer);
      reader.onerror = () => reject(reader.error);
      reader.readAsArrayBuffer(file.slice(0, bytes));
    });
  }

  private detectFileTypeFromSignature(signature: string): string | null {
    const signatures: { [key: string]: string } = {
      ffd8ff: 'image/jpeg',
      '89504e47': 'image/png',
      '52494646': 'image/webp', // Partial - WebP starts with RIFF
      '00000018667479': 'image/heic', // HEIC signature is more complex
      '00000020667479': 'image/heif',
    };

    for (const [sig, type] of Object.entries(signatures)) {
      if (signature.startsWith(sig)) {
        return type;
      }
    }

    return null;
  }

  private formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
  }
}
