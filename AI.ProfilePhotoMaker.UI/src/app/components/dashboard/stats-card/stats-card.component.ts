import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

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
  @Input() showSettingsHint: boolean = false;
}