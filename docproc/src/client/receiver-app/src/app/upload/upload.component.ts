import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-upload',
  styleUrl: './upload.component.scss',
  templateUrl: './upload.component.html',
})
export class UploadComponent {}
