import { Injectable, NgZone } from '@angular/core';
import * as faceapi from 'face-api.js';
import { IModelLoaderService } from '../interfaces/service.interfaces';

@Injectable({
  providedIn: 'root'
})
export class ModelLoaderService implements IModelLoaderService {
  private modelsLoaded = false;
  private modelLoadingPromise: Promise<void> | null = null;

  constructor(private ngZone: NgZone) {}

  /**
   * Load face-api.js models (lazy loading)
   */
  async loadModels(): Promise<void> {
    if (this.modelsLoaded) {
      return;
    }

    if (this.modelLoadingPromise) {
      return this.modelLoadingPromise;
    }

    this.modelLoadingPromise = this.doLoadModels();
    return this.modelLoadingPromise;
  }

  /**
   * Check if models are loaded
   */
  areModelsLoaded(): boolean {
    return this.modelsLoaded;
  }

  private async doLoadModels(): Promise<void> {
    return this.ngZone.runOutsideAngular(async () => {
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
    });
  }
}