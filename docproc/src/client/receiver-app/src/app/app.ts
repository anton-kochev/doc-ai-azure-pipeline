import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-root',
  imports: [RouterLink, RouterOutlet],
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {}
