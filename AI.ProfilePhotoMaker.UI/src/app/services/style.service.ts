import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';

export interface Style {
  id: number;
  name: string;
  description: string;
  promptTemplate: string;
  negativePromptTemplate: string;
  isActive: boolean;
  createdAt: Date;
  updatedAt: Date;
}

export interface StyleSelection {
  styleIds: number[];
}

@Injectable({
  providedIn: 'root'
})
export class StyleService {
  constructor(private http: HttpClient, private config: ConfigService) {}

  getAllStyles(): Observable<{ success: boolean; data: Style[]; error: any }> {
    return this.http.get<{ success: boolean; data: Style[]; error: any }>(this.config.getFullUrl('/styles'));
  }

  getActiveStyles(): Observable<{ success: boolean; data: Style[]; error: any }> {
    return this.http.get<{ success: boolean; data: Style[]; error: any }>(this.config.activeStylesUrl);
  }

  getStyleById(id: number): Observable<{ success: boolean; data: Style; error: any }> {
    return this.http.get<{ success: boolean; data: Style; error: any }>(this.config.getFullUrl(`/styles/${id}`));
  }

  getUserSelectedStyles(): Observable<{ success: boolean; data: Style[]; error: any }> {
    return this.http.get<{ success: boolean; data: Style[]; error: any }>(this.config.getFullUrl('/styles/user-selected'));
  }

  selectStyles(selection: StyleSelection): Observable<{ success: boolean; message: string; error: any }> {
    return this.http.post<{ success: boolean; message: string; error: any }>(this.config.getFullUrl('/styles/select'), selection);
  }
}