import { HttpEvent } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BaseHttpService } from './base-http.service';

@Injectable({ providedIn: 'root' })
export class FileUploadService extends BaseHttpService {
  public getSasUrl(fileName: string, fileSizeBytes: number, contentType?: string): Observable<{ sasUrl: string }> {
    return this.post<{ sasUrl: string }>(
      `/api/upload/sas`,
      { fileName, fileSizeBytes, contentType },
    );
  }

  public uploadFile(file: File, sasUrl: string): Observable<HttpEvent<string>> {
    return this.http.put(sasUrl, file, {
      headers: {
        'x-ms-blob-type': 'BlockBlob', // Required for Azure Blob
        'Content-Type': file.type ?? 'application/octet-stream',
      },
      reportProgress: true,
      observe: 'events',
      responseType: 'text',
    });
  }
}
