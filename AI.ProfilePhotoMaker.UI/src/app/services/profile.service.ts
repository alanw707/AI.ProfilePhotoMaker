import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';

export interface UserProfile {
  id: number;
  userId: string;
  firstName?: string;
  lastName?: string;
  gender?: string;
  ethnicity?: string;
  trainedModelId?: string;
  modelTrainedAt?: Date;
  subscriptionTier: string;
  basicCredits: number;
  lastCreditReset: Date;
  createdAt: Date;
  updatedAt: Date;
}

export interface CreateProfileDto {
  firstName?: string;
  lastName?: string;
  gender?: string;
  ethnicity?: string;
}

export interface UpdateProfileDto {
  firstName?: string;
  lastName?: string;
  gender?: string;
  ethnicity?: string;
}

@Injectable({
  providedIn: 'root'
})
export class ProfileService {
  constructor(private http: HttpClient, private config: ConfigService) {}

  getCurrentUserProfile(): Observable<{ success: boolean; data: UserProfile; error: any }> {
    return this.http.get<{ success: boolean; data: UserProfile; error: any }>(this.config.profileUrl);
  }

  createProfile(profile: CreateProfileDto): Observable<{ success: boolean; data: UserProfile; error: any }> {
    return this.http.post<{ success: boolean; data: UserProfile; error: any }>(this.config.profileUrl, profile);
  }

  updateProfile(profile: UpdateProfileDto): Observable<{ success: boolean; data: UserProfile; error: any }> {
    return this.http.put<{ success: boolean; data: UserProfile; error: any }>(this.config.profileUrl, profile);
  }

  deleteProfile(): Observable<{ success: boolean; message: string }> {
    return this.http.delete<{ success: boolean; message: string }>(this.config.profileUrl);
  }
}