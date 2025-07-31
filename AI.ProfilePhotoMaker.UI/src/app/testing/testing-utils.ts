/**
 * Testing utilities for Angular components and services
 * 
 * This module provides common testing patterns and utilities for the AI.ProfilePhotoMaker project.
 * Created as part of the refactoring process to establish comprehensive testing.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { BehaviorSubject, Observable, of } from 'rxjs';

// Mock Services
export class MockAuthService {
  private _isAuthenticatedSubject = new BehaviorSubject<boolean>(false);
  isAuthenticated$ = this._isAuthenticatedSubject.asObservable();
  
  login(email: string, _password: string): Observable<{ token: string; user: { email: string } }> {
    this._isAuthenticatedSubject.next(true);
    return of({ token: 'mock-token', user: { email } });
  }
  
  logout(): void {
    this._isAuthenticatedSubject.next(false);
  }
  
  getCurrentUser(): Observable<{ id: string; email: string; name: string }> {
    return of({ id: '1', email: 'test@example.com', name: 'Test User' });
  }
  
  getToken(): string {
    return 'mock-jwt-token';
  }
}

export class MockDashboardStateService {
  private _stateSubject = new BehaviorSubject({
    uploadedImages: [],
    selectedStyles: [],
    isTraining: false,
    isGenerating: false,
    credits: 3,
    trainedModelId: null
  });
  
  state$ = this._stateSubject.asObservable();
  
  updateUploadedImages(images: never[]): void {
    const currentState = this._stateSubject.value;
    this._stateSubject.next({ ...currentState, uploadedImages: images });
  }
  
  updateSelectedStyles(styles: never[]): void {
    const currentState = this._stateSubject.value;
    this._stateSubject.next({ ...currentState, selectedStyles: styles });
  }
  
  setTrainingStatus(isTraining: boolean): void {
    const currentState = this._stateSubject.value;
    this._stateSubject.next({ ...currentState, isTraining });
  }
  
  setGeneratingStatus(isGenerating: boolean): void {
    const currentState = this._stateSubject.value;
    this._stateSubject.next({ ...currentState, isGenerating });
  }
  
  updateCredits(credits: number): void {
    const currentState = this._stateSubject.value;
    this._stateSubject.next({ ...currentState, credits });
  }
}

export class MockNotificationService {
  private _notifications: unknown[] = [];
  
  showSuccess(message: string, title?: string): void {
    this._notifications.push({ type: 'success', message, title });
  }
  
  showError(message: string, title?: string): void {
    this._notifications.push({ type: 'error', message, title });
  }
  
  showInfo(message: string, title?: string): void {
    this._notifications.push({ type: 'info', message, title });
  }
  
  getNotifications(): unknown[] {
    return [...this._notifications];
  }
  
  clearNotifications(): void {
    this._notifications = [];
  }
}

export class MockFileUploadService {
  uploadMultipleImages(files: File[]): Observable<{
    success: boolean;
    data: { id: string; filename: string; url: string; size: number }[];
  }> {
    return of({
      success: true,
      data: files.map((file, index) => ({
        id: `mock-id-${index}`,
        filename: file.name,
        url: `mock-url-${index}`,
        size: file.size
      }))
    });
  }
  
  uploadSingleImage(file: File): Observable<{
    success: boolean;
    data: { id: string; filename: string; url: string; size: number };
  }> {
    return of({
      success: true,
      data: {
        id: 'mock-single-id',
        filename: file.name,
        url: 'mock-single-url',
        size: file.size
      }
    });
  }
  
  deleteImage(_imageId: string): Observable<{ success: boolean }> {
    return of({ success: true });
  }
}

export class MockReplicateService {
  trainModel(_request: unknown): Observable<{
    success: boolean;
    data: { id: string; status: string; estimatedTime: number };
  }> {
    return of({
      success: true,
      data: {
        id: 'mock-training-id',
        status: 'starting',
        estimatedTime: 900 // 15 minutes
      }
    });
  }
  
  generateImages(_request: unknown): Observable<{
    success: boolean;
    data: { id: string; status: string; estimatedTime: number };
  }> {
    return of({
      success: true,
      data: {
        id: 'mock-generation-id',
        status: 'starting',
        estimatedTime: 120 // 2 minutes
      }
    });
  }
  
  enhancePhoto(_request: unknown): Observable<{
    success: boolean;
    data: { id: string; status: string; url: string };
  }> {
    return of({
      success: true,
      data: {
        id: 'mock-enhancement-id',
        status: 'starting',
        url: 'mock-enhanced-url'
      }
    });
  }
  
  checkTrainingStatus(id: string): Observable<{
    success: boolean;
    data: { id: string; status: string; modelId: string };
  }> {
    return of({
      success: true,
      data: {
        id,
        status: 'completed',
        modelId: 'mock-trained-model-id'
      }
    });
  }
  
  checkGenerationStatus(id: string): Observable<{
    success: boolean;
    data: { id: string; status: string; images: { url: string; style: string }[] };
  }> {
    return of({
      success: true,
      data: {
        id,
        status: 'completed',
        images: [
          { url: 'mock-image-1.jpg', style: 'corporate' },
          { url: 'mock-image-2.jpg', style: 'casual' }
        ]
      }
    });
  }
}

export class MockCreditService {
  getCredits(): Observable<{
    success: boolean;
    data: { weeklyCredits: number; purchasedCredits: number; totalCredits: number };
  }> {
    return of({
      success: true,
      data: {
        weeklyCredits: 3,
        purchasedCredits: 25,
        totalCredits: 28
      }
    });
  }
  
  consumeCredits(amount: number, operation: string): Observable<{
    success: boolean;
    data: { remainingCredits: number; operation: string };
  }> {
    return of({
      success: true,
      data: {
        remainingCredits: 25 - amount,
        operation
      }
    });
  }
  
  purchaseCredits(_packageId: string): Observable<{
    success: boolean;
    data: { creditsAdded: number; totalCredits: number };
  }> {
    return of({
      success: true,
      data: {
        creditsAdded: 50,
        totalCredits: 75
      }
    });
  }
}

export class MockFaceDetectionService {
  loadModels(): Promise<void> {
    return Promise.resolve();
  }
  
  validateImageQuality(_file: File): Promise<{
    isValid: boolean;
    score: number;
    reasons: unknown[];
    faceCount: number;
    dimensions: { width: number; height: number };
  }> {
    return Promise.resolve({
      isValid: true,
      score: 0.85,
      reasons: [],
      faceCount: 1,
      dimensions: { width: 1024, height: 1024 }
    });
  }
  
  detectFaces(_imageUrl: string): Promise<{
    detection: { box: { x: number; y: number; width: number; height: number } };
    landmarks: unknown[];
    descriptor: Float32Array;
  }[]> {
    return Promise.resolve([
      {
        detection: { box: { x: 100, y: 100, width: 200, height: 200 } },
        landmarks: [],
        descriptor: new Float32Array(128)
      }
    ]);
  }
}

// Testing Helper Functions
export class TestingHelpers {
  /**
   * Creates a mock file for testing file upload functionality
   */
  static createMockFile(name = 'test.jpg', _size = 1024, type = 'image/jpeg'): File {
    const blob = new Blob(['mock file content'], { type });
    return new File([blob], name, { type, lastModified: Date.now() });
  }
  
  /**
   * Creates multiple mock files for bulk upload testing
   */
  static createMockFiles(count = 3): File[] {
    return Array.from({ length: count }, (_, i) => 
      this.createMockFile(`test-${i + 1}.jpg`, 1024 + i * 100)
    );
  }
  
  /**
   * Triggers a file input change event for testing
   */
  static triggerFileInputChange(
    fixture: ComponentFixture<unknown>,
    files: File[],
    inputSelector = 'input[type="file"]'
  ): void {
    const fileInput = fixture.debugElement.query(By.css(inputSelector));
    if (fileInput) {
      const event = new Event('change');
      Object.defineProperty(event, 'target', {
        value: { files },
        enumerable: true
      });
      fileInput.nativeElement.dispatchEvent(event);
      fixture.detectChanges();
    }
  }
  
  /**
   * Clicks a button and waits for change detection
   */
  static clickButton(fixture: ComponentFixture<unknown>, buttonSelector: string): void {
    const button = fixture.debugElement.query(By.css(buttonSelector));
    if (button) {
      button.nativeElement.click();
      fixture.detectChanges();
    }
  }
  
  /**
   * Waits for async operations to complete
   */
  static async waitForAsync(fixture: ComponentFixture<unknown>): Promise<void> {
    await fixture.whenStable();
    fixture.detectChanges();
  }
  
  /**
   * Sets up a basic test module configuration
   */
  static async setupTestModule(
    component: unknown,
    providers: unknown[] = [],
    imports: unknown[] = []
  ): Promise<void> {
    await TestBed.configureTestingModule({
      imports: [component, ...imports],
      providers: [
        { provide: MockAuthService, useClass: MockAuthService },
        { provide: MockDashboardStateService, useClass: MockDashboardStateService },
        { provide: MockNotificationService, useClass: MockNotificationService },
        { provide: MockFileUploadService, useClass: MockFileUploadService },
        { provide: MockReplicateService, useClass: MockReplicateService },
        { provide: MockCreditService, useClass: MockCreditService },
        { provide: MockFaceDetectionService, useClass: MockFaceDetectionService },
        ...providers
      ]
    }).compileComponents();
  }
}

// Mock Router for navigation testing
@Component({ 
  template: '',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MockComponent { }

export const mockRoutes = [
  { path: 'dashboard', component: MockComponent },
  { path: 'gallery', component: MockComponent },
  { path: 'login', component: MockComponent },
  { path: 'settings', component: MockComponent },
  { path: '', redirectTo: '/dashboard', pathMatch: 'full' }
];

/**
 * Test constants for consistent testing values
 */
export const testConstants = {
  mockUser: {
    id: '123',
    email: 'test@example.com',
    name: 'Test User',
    credits: 3
  },
  mockStyles: [
    { id: '1', name: 'corporate', displayName: 'Corporate' },
    { id: '2', name: 'casual', displayName: 'Casual' },
    { id: '3', name: 'artistic', displayName: 'Artistic' }
  ],
  mockImages: [
    { id: '1', filename: 'test1.jpg', url: 'mock-url-1' },
    { id: '2', filename: 'test2.jpg', url: 'mock-url-2' }
  ],
  timeouts: {
    short: 1000,
    medium: 5000,
    long: 10000
  }
};