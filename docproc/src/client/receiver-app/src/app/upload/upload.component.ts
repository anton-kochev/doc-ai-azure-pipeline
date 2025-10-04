import { HttpEventType } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { switchMap } from 'rxjs';

import { FileUploadService } from '../file-upload.service';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatProgressBarModule],
  selector: 'app-upload',
  styleUrl: './upload.component.scss',
  templateUrl: './upload.component.html',
})
export class UploadComponent {
  selectedFile = signal<File | null>(null);
  isDragging = signal(false);
  isUploading = signal(false);
  uploadProgress = signal(0);
  uploadError = signal<string | null>(null);
  uploadSuccess = signal(false);

  private readonly fileUploadService = inject(FileUploadService);

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.selectedFile.set(input.files[0]);
      this.resetUploadState();
    }
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging.set(false);

    if (event.dataTransfer?.files && event.dataTransfer.files.length > 0) {
      this.selectedFile.set(event.dataTransfer.files[0]);
      this.resetUploadState();
    }
  }

  removeFile(): void {
    this.selectedFile.set(null);
    this.resetUploadState();
  }

  uploadFile(): void {
    const file = this.selectedFile();
    if (!file) return;

    this.isUploading.set(true);
    this.uploadError.set(null);
    this.uploadSuccess.set(false);
    this.uploadProgress.set(0);

    this.fileUploadService
      .getSasUrl(file.name)
      .pipe(switchMap(response => this.fileUploadService.uploadFile(file, response.uploadUrl)))
      .subscribe({
        next: event => {
          if (event.type === HttpEventType.UploadProgress) {
            const progress = event.total ? Math.round((100 * event.loaded) / event.total) : 0;
            this.uploadProgress.set(progress);
          } else if (event.type === HttpEventType.Response) {
            this.uploadSuccess.set(true);
            this.isUploading.set(false);
          }
        },
        error: error => {
          this.uploadError.set('Upload failed. Please try again.');
          this.isUploading.set(false);
          console.error('Upload error:', error);
        },
      });
  }

  private resetUploadState(): void {
    this.isUploading.set(false);
    this.uploadProgress.set(0);
    this.uploadError.set(null);
    this.uploadSuccess.set(false);
  }
}
