import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

export enum LogLevel {
  DEBUG = 0,
  INFO = 1,
  WARN = 2,
  ERROR = 3,
}

@Injectable({
  providedIn: 'root',
})
export class LoggingService {
  private readonly isDebugMode = environment.features.debugMode;
  private readonly isProduction = environment.production;

  /**
   * Log debug messages (only in debug mode)
   */
  debug(message: string, data?: any): void {
    if (!this.isDebugMode) {
      return;
    }

    if (data) {
      console.log(`🐛 ${message}`, data);
    } else {
      console.log(`🐛 ${message}`);
    }
  }

  /**
   * Log informational messages
   */
  info(message: string, data?: any): void {
    if (data) {
      console.log(`ℹ️ ${message}`, data);
    } else {
      console.log(`ℹ️ ${message}`);
    }
  }

  /**
   * Log warning messages (always shown)
   */
  warn(message: string, data?: any): void {
    if (data) {
      console.warn(`⚠️ ${message}`, data);
    } else {
      console.warn(`⚠️ ${message}`);
    }
  }

  /**
   * Log error messages (always shown)
   */
  error(message: string, error?: any): void {
    if (error) {
      console.error(`❌ ${message}`, error);
    } else {
      console.error(`❌ ${message}`);
    }
  }

  /**
   * Log API-related debug information
   */
  apiDebug(operation: string, url: string, data?: any): void {
    if (!this.isDebugMode) {
      return;
    }

    this.debug(`API ${operation}: ${url}`, data);
  }

  /**
   * Log state changes (debug only)
   */
  stateDebug(component: string, state: any): void {
    if (!this.isDebugMode) {
      return;
    }

    this.debug(`State Update [${component}]`, state);
  }

  /**
   * Log workflow steps (debug only)
   */
  workflowDebug(step: string, data?: any): void {
    if (!this.isDebugMode) {
      return;
    }

    this.debug(`🔄 Workflow: ${step}`, data);
  }

  /**
   * Log authentication events (debug only)
   */
  authDebug(event: string, data?: any): void {
    if (!this.isDebugMode) {
      return;
    }

    this.debug(`🔐 Auth: ${event}`, data);
  }

  /**
   * Log file operations (debug only)
   */
  fileDebug(operation: string, filename: string, data?: any): void {
    if (!this.isDebugMode) {
      return;
    }

    this.debug(`📁 File ${operation}: ${filename}`, data);
  }

  /**
   * Conditional logging based on feature flags
   */
  conditionalLog(condition: boolean, level: LogLevel, message: string, data?: any): void {
    if (!condition) {
      return;
    }

    switch (level) {
      case LogLevel.DEBUG:
        this.debug(message, data);
        break;
      case LogLevel.INFO:
        this.info(message, data);
        break;
      case LogLevel.WARN:
        this.warn(message, data);
        break;
      case LogLevel.ERROR:
        this.error(message, data);
        break;
    }
  }
}
