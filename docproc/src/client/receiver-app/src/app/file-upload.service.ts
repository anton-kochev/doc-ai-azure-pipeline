import { HttpClient, HttpEvent } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class FileUploadService {
  private readonly http = inject(HttpClient);

  public getSasUrl(fileName: string): Observable<{ uploadUrl: string }> {
    return this.http.get<{ uploadUrl: string }>(
      `/api/upload/sas-url?fileName=${encodeURIComponent(fileName)}`,
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
