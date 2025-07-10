import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ConfigStatusComponent } from './components/config-status/config-status.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ConfigStatusComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.sass'
})
export class AppComponent {
  title = 'AI.ProfilePhotoMaker.UI';
}
