import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';

export interface StyleOption {
  id: string;
  name: string;
  description: string;
  previewUrl: string;
  selected: boolean;
}

@Component({
  selector: 'app-style-selector',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './style-selector.component.html',
  styleUrls: ['./style-selector.component.sass'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StyleSelectorComponent {
  constructor(private _router: Router) {}
  @Input() availableStyles: StyleOption[] = [];
  @Input() imagesPerStyle = 2;
  @Input() selectedStyles = 0;
  @Input() trainingCredits = 15;
  @Input() generationCredits = 0;
  @Input() totalCredits = 0;
  @Input() hasEnoughCredits = true;
  @Input() remainingCredits = 0;
  @Input() isTrainingStarted = false;
  @Input() modelStatus = '';
  @Input() uploadedImageCount = 0;
  @Input() isTraining = false;
  @Input() isGenerating = false;
  @Input() progressPercentage = 0;
  @Input() progressMessage = '';
  @Input() estimatedCompletion = '';
  @Input() lastGenerationCount = 0;
  @Input() showLastGenerationMessage = false;

  @Output() styleToggled = new EventEmitter<StyleOption>();
  @Output() imagesPerStyleChanged = new EventEmitter<number>();
  @Output() selectAllStyles = new EventEmitter<void>();
  @Output() deselectAllStyles = new EventEmitter<void>();
  @Output() startTraining = new EventEmitter<void>();
  @Output() continueInBackground = new EventEmitter<void>();
  @Output() dismissSuccessMessage = new EventEmitter<void>();

  onToggleStyle(style: StyleOption): void {
    this.styleToggled.emit(style);
  }

  onImagesPerStyleChange(count: number): void {
    this.imagesPerStyle = count;
    this.imagesPerStyleChanged.emit(count);
  }

  onSelectAll(): void {
    this.selectAllStyles.emit();
  }

  onDeselectAll(): void {
    this.deselectAllStyles.emit();
  }

  onStartTraining(): void {
    this.startTraining.emit();
  }

  onContinueInBackground(): void {
    this.continueInBackground.emit();
  }

  onDismissSuccessMessage(): void {
    this.dismissSuccessMessage.emit();
  }

  formatStyleName(styleName: string): string {
    if (!styleName) {
      return '';
    }
    return styleName
      .split('-')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' ');
  }

  onImageError(event: any) {
    // Placeholder image fallback - could be passed as input
    event.target.src = '/api/placeholder/style-preview';
    event.target.onerror = null;
  }

  navigateToGallery() {
    // Navigate to gallery with refresh parameter to force reload
    this.router.navigate(['/gallery'], {
      queryParams: { refresh: Date.now() },
    });
  }
}
