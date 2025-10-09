import { HttpEvent } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseHttpService } from './base-http.service';

export interface BlobUploadResult {
  blobUrl: string;
  fileName: string;
  contentType?: string;
  fileSizeBytes: number;
}

@Injectable({ providedIn: 'root' })
export class FileUploadService extends BaseHttpService {
  /**
   * Uploads a file to the server, which then uploads it to Azure Blob Storage.
   * The server uses Managed Identity for secure access to storage.
   */
  public uploadFile(file: File): Observable<HttpEvent<BlobUploadResult>> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    return this.post<HttpEvent<BlobUploadResult>>(`/api/upload`, formData, {
      reportProgress: true,
      observe: 'events',
    });
  }
}
