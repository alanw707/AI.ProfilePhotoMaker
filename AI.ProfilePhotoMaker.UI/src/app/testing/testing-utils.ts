/**
 * Testing utilities for Angular components and services
 * 
 * This module provides common testing patterns and utilities for the AI.ProfilePhotoMaker project.
 * Created as part of the refactoring process to establish comprehensive testing.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DebugElement } from '@angular/core';
import { By } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { Location } from '@angular/common';
import { Component } from '@angular/core';
import { of, BehaviorSubject, Observable } from 'rxjs';

// Mock Services
export class MockAuthService {
  private isAuthenticatedSubject = new BehaviorSubject<boolean>(false);
  isAuthenticated$ = this.isAuthenticatedSubject.asObservable();
  
  login(email: string, password: string) {
    this.isAuthenticatedSubject.next(true);
    return of({ token: 'mock-token', user: { email } });
  }
  
  logout() {
    this.isAuthenticatedSubject.next(false);
  }
  
  getCurrentUser() {
    return of({ id: '1', email: 'test@example.com', name: 'Test User' });
  }
  
  getToken() {
    return 'mock-jwt-token';
  }
}

export class MockDashboardStateService {
  private stateSubject = new BehaviorSubject({
    uploadedImages: [],
    selectedStyles: [],
    isTraining: false,
    isGenerating: false,
    credits: 3,
    trainedModelId: null
  });
  
  state$ = this.stateSubject.asObservable();
  
  updateUploadedImages(images: never[]) {
    const currentState = this.stateSubject.value;
    this.stateSubject.next({ ...currentState, uploadedImages: images });
  }
  
  updateSelectedStyles(styles: never[]) {
    const currentState = this.stateSubject.value;
    this.stateSubject.next({ ...currentState, selectedStyles: styles });
  }
  
  setTrainingStatus(isTraining: boolean) {
    const currentState = this.stateSubject.value;
    this.stateSubject.next({ ...currentState, isTraining });
  }
  
  setGeneratingStatus(isGenerating: boolean) {
    const currentState = this.stateSubject.value;
    this.stateSubject.next({ ...currentState, isGenerating });
  }
  
  updateCredits(credits: number) {
    const currentState = this.stateSubject.value;
    this.stateSubject.next({ ...currentState, credits });
  }
}

export class MockNotificationService {
  private notifications: any[] = [];
  
  showSuccess(message: string, title?: string) {
    this.notifications.push({ type: 'success', message, title });
  }
  
  showError(message: string, title?: string) {
    this.notifications.push({ type: 'error', message, title });
  }
  
  showInfo(message: string, title?: string) {
    this.notifications.push({ type: 'info', message, title });
  }
  
  getNotifications() {
    return [...this.notifications];
  }
  
  clearNotifications() {
    this.notifications = [];
  }
}

export class MockFileUploadService {
  uploadMultipleImages(files: File[]) {
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
  
  uploadSingleImage(file: File) {
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
  
  deleteImage(imageId: string) {
    return of({ success: true });
  }
}

export class MockReplicateService {
  trainModel(request: any) {
    return of({
      success: true,
      data: {
        id: 'mock-training-id',
        status: 'starting',
        estimatedTime: 900 // 15 minutes
      }
    });
  }
  
  generateImages(request: any) {
    return of({
      success: true,
      data: {
        id: 'mock-generation-id',
        status: 'starting',
        estimatedTime: 120 // 2 minutes
      }
    });
  }
  
  enhancePhoto(request: any) {
    return of({
      success: true,
      data: {
        id: 'mock-enhancement-id',
        status: 'starting',
        url: 'mock-enhanced-url'
      }
    });
  }
  
  checkTrainingStatus(id: string) {
    return of({
      success: true,
      data: {
        id,
        status: 'completed',
        modelId: 'mock-trained-model-id'
      }
    });
  }
  
  checkGenerationStatus(id: string) {
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
  getCredits() {
    return of({
      success: true,
      data: {
        weeklyCredits: 3,
        purchasedCredits: 25,
        totalCredits: 28
      }
    });
  }
  
  consumeCredits(amount: number, operation: string) {
    return of({
      success: true,
      data: {
        remainingCredits: 25 - amount,
        operation
      }
    });
  }
  
  purchaseCredits(packageId: string) {
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
  loadModels() {
    return Promise.resolve();
  }
  
  validateImageQuality(file: File) {
    return Promise.resolve({
      isValid: true,
      score: 0.85,
      reasons: [],
      faceCount: 1,
      dimensions: { width: 1024, height: 1024 }
    });
  }
  
  detectFaces(imageUrl: string) {
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
  static createMockFile(name: string = 'test.jpg', size: number = 1024, type: string = 'image/jpeg'): File {
    const blob = new Blob(['mock file content'], { type });
    return new File([blob], name, { type, lastModified: Date.now() });
  }
  
  /**
   * Creates multiple mock files for bulk upload testing
   */
  static createMockFiles(count: number = 3): File[] {
    return Array.from({ length: count }, (_, i) => 
      this.createMockFile(`test-${i + 1}.jpg`, 1024 + i * 100)
    );
  }
  
  /**
   * Triggers a file input change event for testing
   */
  static triggerFileInputChange(fixture: ComponentFixture<any>, files: File[], inputSelector: string = 'input[type="file"]') {
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
  static clickButton(fixture: ComponentFixture<any>, buttonSelector: string) {
    const button = fixture.debugElement.query(By.css(buttonSelector));
    if (button) {
      button.nativeElement.click();
      fixture.detectChanges();
    }
  }
  
  /**
   * Waits for async operations to complete
   */
  static async waitForAsync(fixture: ComponentFixture<any>) {
    await fixture.whenStable();
    fixture.detectChanges();
  }
  
  /**
   * Sets up a basic test module configuration
   */
  static async setupTestModule(component: any, providers: any[] = [], imports: any[] = []) {
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
@Component({ template: '' })
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
export const TestConstants = {
  MOCK_USER: {
    id: '123',
    email: 'test@example.com',
    name: 'Test User',
    credits: 3
  },
  MOCK_STYLES: [
    { id: '1', name: 'corporate', displayName: 'Corporate' },
    { id: '2', name: 'casual', displayName: 'Casual' },
    { id: '3', name: 'artistic', displayName: 'Artistic' }
  ],
  MOCK_IMAGES: [
    { id: '1', filename: 'test1.jpg', url: 'mock-url-1' },
    { id: '2', filename: 'test2.jpg', url: 'mock-url-2' }
  ],
  TIMEOUTS: {
    SHORT: 1000,
    MEDIUM: 5000,
    LONG: 10000
  }
};