import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

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
  imports: [CommonModule, FormsModule],
  templateUrl: './style-selector.component.html',
  styleUrls: ['./style-selector.component.sass']
})
export class StyleSelectorComponent {
  @Input() availableStyles: StyleOption[] = [];
  @Input() imagesPerStyle: number = 2;
  @Input() selectedStyles: number = 0;
  @Input() trainingCredits: number = 15;
  @Input() generationCredits: number = 0;
  @Input() totalCredits: number = 0;
  @Input() hasEnoughCredits: boolean = true;
  @Input() remainingCredits: number = 0;
  @Input() isTrainingStarted: boolean = false;
  @Input() modelStatus: string = '';
  @Input() uploadedImageCount: number = 0;

  @Output() styleToggled = new EventEmitter<StyleOption>();
  @Output() imagesPerStyleChanged = new EventEmitter<number>();
  @Output() selectAllStyles = new EventEmitter<void>();
  @Output() deselectAllStyles = new EventEmitter<void>();
  @Output() startTraining = new EventEmitter<void>();

  onToggleStyle(style: StyleOption) {
    this.styleToggled.emit(style);
  }

  onImagesPerStyleChange(count: number) {
    this.imagesPerStyle = count;
    this.imagesPerStyleChanged.emit(count);
  }

  onSelectAll() {
    this.selectAllStyles.emit();
  }

  onDeselectAll() {
    this.deselectAllStyles.emit();
  }

  onStartTraining() {
    this.startTraining.emit();
  }

  formatStyleName(styleName: string): string {
    if (!styleName) return '';
    
    return styleName
      .split('-') // Split by dashes
      .map(word => word.charAt(0).toUpperCase() + word.slice(1)) // Capitalize first letter of each word
      .join(' '); // Join with spaces
  }

  onImageError(event: any) {
    // Placeholder image fallback - could be passed as input
    event.target.src = '/api/placeholder/style-preview';
    event.target.onerror = null;
  }
}