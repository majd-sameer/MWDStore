import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from '../http-utils';
import type { MediaDto } from '../models';

/** Admin media uploads (`/api/admin/media`). */
@Injectable({ providedIn: 'root' })
export class AdminMediaService {
  private readonly http = inject(HttpClient);

  /** POST /api/admin/media (multipart). Returns the stored media row + URL. */
  upload(file: File): Observable<MediaDto> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<MediaDto>(`${API_ROOT}/admin/media`, body);
  }
}
