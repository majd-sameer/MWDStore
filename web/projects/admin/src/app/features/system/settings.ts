import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { AdminSystemService } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * App settings (old Core configuration admin): edit values inline, add new keys.
 * Saving sends only the changed keys as a bulk upsert.
 */
@Component({
  selector: 'app-admin-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, TranslatePipe, PageHeader],
  templateUrl: './settings.html',
})
export class AdminSettings {
  private readonly service = inject(AdminSystemService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.settingsResource();
  protected readonly changes = signal<Record<string, string>>({});
  protected readonly saving = signal(false);

  protected hasChanges(): boolean {
    return Object.keys(this.changes()).length > 0;
  }

  protected stage(key: string, value: string): void {
    this.changes.update((c) => ({ ...c, [key]: value }));
  }

  protected stageNew(keyInput: HTMLInputElement, valueInput: HTMLInputElement): void {
    const key = keyInput.value.trim();
    if (!key) {
      return;
    }
    this.stage(key, valueInput.value);
    keyInput.value = '';
    valueInput.value = '';
    this.save();
  }

  protected save(): void {
    const settings = this.changes();
    if (!Object.keys(settings).length) {
      return;
    }
    this.saving.set(true);
    this.service.updateSettings({ settings }).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('settings.saved_ok'));
        this.changes.set({});
        this.saving.set(false);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('settings.save_failed'));
        this.saving.set(false);
      },
    });
  }
}
