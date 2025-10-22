import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { ConfigService } from './config/config.service';

/**
 * Abstract base service for HTTP operations with automatic API URL building.
 * Provides protected methods for common HTTP operations (GET, POST, PUT, PATCH, DELETE).
 */
export abstract class BaseHttpService {
  protected readonly http = inject(HttpClient);
  protected readonly configService = inject(ConfigService);

  protected get apiUrl(): string {
    return this.configService.apiUrl;
  }

  protected buildUrl(path: string): string {
    const cleanPath = path.startsWith('/') ? path : `/${path}`;

    return `${this.apiUrl}${cleanPath}`;
  }

  protected get<T>(path: string, options?: object): Observable<T> {
    return this.http.get<T>(this.buildUrl(path), options);
  }

  protected post<T>(path: string, body: unknown, options?: object): Observable<T> {
    return this.http.post<T>(this.buildUrl(path), body, options);
  }

  protected put<T>(path: string, body: unknown, options?: object): Observable<T> {
    return this.http.put<T>(this.buildUrl(path), body, options);
  }

  protected patch<T>(path: string, body: unknown, options?: object): Observable<T> {
    return this.http.patch<T>(this.buildUrl(path), body, options);
  }

  protected delete<T>(path: string, options?: object): Observable<T> {
    return this.http.delete<T>(this.buildUrl(path), options);
  }
}
