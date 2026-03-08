import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { ConfigService } from './config.service';

/**
 * Standard API response format used throughout the application
 */
export interface ApiResponse<T = any> {
  success: boolean;
  data?: T;
  message?: string;
  error?: {
    code: string;
    message: string;
  };
}

/**
 * Configuration options for HTTP requests
 */
export interface RequestOptions {
  headers?: HttpHeaders | Record<string, string | string[]>;
  withCredentials?: boolean;
  observe?: 'body';
  responseType?: 'json';
  reportProgress?: boolean;
}

/**
 * Base HTTP service that provides common HTTP patterns and error handling
 */
@Injectable({
  providedIn: 'root',
})
export class BaseHttpService {
  constructor(
    protected http: HttpClient,
    protected config: ConfigService
  ) {}

  /**
   * Builds a full API URL from a relative path
   */
  protected buildApiUrl(endpoint: string): string {
    const baseUrl = this.config.getApiUrl();
    const cleanEndpoint = endpoint.startsWith('/') ? endpoint.slice(1) : endpoint;
    return `${baseUrl}/${cleanEndpoint}`;
  }

  /**
   * Builds a full URL (for external resources or specific base URLs)
   */
  protected buildFullUrl(baseUrl: string, endpoint: string): string {
    const cleanBase = baseUrl.endsWith('/') ? baseUrl.slice(0, -1) : baseUrl;
    const cleanEndpoint = endpoint.startsWith('/') ? endpoint.slice(1) : endpoint;
    return `${cleanBase}/${cleanEndpoint}`;
  }

  /**
   * GET request with standardized error handling and response mapping
   */
  protected get<T>(endpoint: string, options?: RequestOptions): Observable<T> {
    const url = this.buildApiUrl(endpoint);
    return this.http.get<ApiResponse<T>>(url, options).pipe(
      map(response => this.extractData<T>(response)),
      catchError(error => this.handleError(error, 'GET', endpoint))
    );
  }

  /**
   * POST request with standardized error handling and response mapping
   */
  protected post<T>(endpoint: string, body: any, options?: RequestOptions): Observable<T> {
    const url = this.buildApiUrl(endpoint);
    return this.http.post<ApiResponse<T>>(url, body, options).pipe(
      map(response => this.extractData<T>(response)),
      catchError(error => this.handleError(error, 'POST', endpoint))
    );
  }

  /**
   * PUT request with standardized error handling and response mapping
   */
  protected put<T>(endpoint: string, body: any, options?: RequestOptions): Observable<T> {
    const url = this.buildApiUrl(endpoint);
    return this.http.put<ApiResponse<T>>(url, body, options).pipe(
      map(response => this.extractData<T>(response)),
      catchError(error => this.handleError(error, 'PUT', endpoint))
    );
  }

  /**
   * DELETE request with standardized error handling and response mapping
   */
  protected delete<T>(endpoint: string, options?: RequestOptions): Observable<T> {
    const url = this.buildApiUrl(endpoint);
    return this.http.delete<ApiResponse<T>>(url, options).pipe(
      map(response => this.extractData<T>(response)),
      catchError(error => this.handleError(error, 'DELETE', endpoint))
    );
  }

  /**
   * PATCH request with standardized error handling and response mapping
   */
  protected patch<T>(endpoint: string, body: any, options?: RequestOptions): Observable<T> {
    const url = this.buildApiUrl(endpoint);
    return this.http.patch<ApiResponse<T>>(url, body, options).pipe(
      map(response => this.extractData<T>(response)),
      catchError(error => this.handleError(error, 'PATCH', endpoint))
    );
  }

  /**
   * Raw HTTP request without response mapping (for non-standard APIs)
   */
  protected rawGet<T>(url: string, options?: RequestOptions): Observable<T> {
    return this.http
      .get<T>(url, options)
      .pipe(catchError(error => this.handleError(error, 'GET', url)));
  }

  /**
   * Raw POST request without response mapping (for non-standard APIs)
   */
  protected rawPost<T>(url: string, body: any, options?: RequestOptions): Observable<T> {
    return this.http
      .post<T>(url, body, options)
      .pipe(catchError(error => this.handleError(error, 'POST', url)));
  }

  /**
   * Extracts data from standard API response format
   */
  private extractData<T>(response: ApiResponse<T>): T {
    if (response.success && response.data !== undefined) {
      return response.data;
    }

    if (!response.success && response.error) {
      throw new Error(`API Error: ${response.error.code} - ${response.error.message}`);
    }

    // Handle cases where success is true but no data field exists
    // (e.g., delete operations that return { success: true, message: "..." })
    if (response.success) {
      return response as any; // Return the whole response for backward compatibility
    }

    throw new Error('Unexpected API response format');
  }

  /**
   * Standardized error handling for HTTP requests
   */
  private handleError(
    error: HttpErrorResponse,
    method: string,
    endpoint: string
  ): Observable<never> {
    let errorMessage = 'An unknown error occurred';
    let errorCode = 'UnknownError';

    if (error.error instanceof ErrorEvent) {
      // Client-side error
      errorMessage = `Client Error: ${error.error.message}`;
      errorCode = 'ClientError';
    } else {
      // Server-side error
      if (error.error && typeof error.error === 'object') {
        // API error response
        if (error.error.error) {
          errorMessage = error.error.error.message || errorMessage;
          errorCode = error.error.error.code || 'ServerError';
        } else if (error.error.message) {
          errorMessage = error.error.message;
          errorCode = 'ServerError';
        }
      } else {
        // HTTP error
        errorMessage = `HTTP ${error.status}: ${error.statusText || 'Unknown Error'}`;
        errorCode = `HTTP${error.status}`;
      }
    }

    // Log error for debugging
    console.error('HTTP %s %s failed:', method, endpoint, {
      code: errorCode,
      message: errorMessage,
      status: error.status,
      url: error.url,
      originalError: error,
    });

    return throwError(() => ({
      code: errorCode,
      message: errorMessage,
      status: error.status,
      originalError: error,
    }));
  }

  /**
   * Creates standard HTTP headers with authentication if available
   */
  protected createAuthHeaders(): HttpHeaders {
    let headers = new HttpHeaders({
      'Content-Type': 'application/json',
    });

    // Authorization header is not set from client storage; rely on HttpOnly cookie
    headers = headers.set('X-Requested-With', 'XMLHttpRequest');

    return headers;
  }

  /**
   * Creates HTTP headers for file uploads
   */
  protected createUploadHeaders(): HttpHeaders {
    let headers = new HttpHeaders();
    // Don't set Content-Type for file uploads - let browser set it with boundary

    headers = headers.set('X-Requested-With', 'XMLHttpRequest');

    return headers;
  }

  /**
   * Utility method to check if user is authenticated
   */
  protected isAuthenticated(): boolean {
    // Delegate to cookie-based session; client cannot inspect HttpOnly cookie
    return true;
  }
}
