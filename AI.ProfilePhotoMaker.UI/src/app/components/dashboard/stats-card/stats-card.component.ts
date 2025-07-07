import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';

@Component({
  selector: 'app-stats-card',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './stats-card.component.html',
  styleUrls: ['./stats-card.component.sass']
})
export class StatsCardComponent {
  @Input() icon: string = '';
  @Input() value: string | number = 0;
  @Input() label: string = '';
  @Input() showCard: boolean = true;
  @Input() isModelStatus: boolean = false;
  @Input() isLoading: boolean = false;
  @Input() isClickable: boolean = false;
  @Input() clickAction: string = ''; // 'gallery', 'settings', etc.
  
  @Output() cardClicked = new EventEmitter<string>();
  
  constructor(private router: Router) {}
  
  onCardClick() {
    if (!this.isClickable || this.isLoading) {
      return;
    }
    
    if (this.clickAction === 'gallery') {
      // Navigate to gallery with refresh parameter
      this.router.navigate(['/gallery'], { 
        queryParams: { refresh: Date.now() } 
      });
    } else if (this.clickAction === 'settings') {
      // Navigate to settings
      this.router.navigate(['/settings']);
    }
    
    // Emit event for custom handling
    this.cardClicked.emit(this.clickAction);
  }
}