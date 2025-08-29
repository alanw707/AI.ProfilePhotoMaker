/**
 * Database entity types matching backend models
 * These types ensure frontend-backend type compatibility
 */

/**
 * Database model creation status enum
 * Matches AI.ProfilePhotoMaker.API.Models.ModelCreationStatus
 */
export enum ModelCreationStatus {
  Pending = 'Pending',
  Creating = 'Creating',
  Ready = 'Ready',
  Failed = 'Failed',
}
