import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import {
  AdminSystemService,
  type AdminResourceDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Localization resources admin (old Localization module): pick a culture, then
 * search/edit/add the resource strings for it.
 */
@Component({
  selector: 'app-admin-localization',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, FormsModule, NgSelectModule, TranslatePipe, PageHeader],
  templateUrl: './localization.html',
})
export class AdminLocalization {
  private readonly service = inject(AdminSystemService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly cultures = this.service.culturesResource();
  protected readonly cultureId = signal<string>('');
  protected readonly query = signal<string>('');
  protected readonly resources = signal<AdminResourceDto[]>([]);

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    effect(() => {
      const culture = this.cultureId();
      const query = this.query();
      if (this.searchTimer) {
        clearTimeout(this.searchTimer);
      }
      if (!culture) {
        this.resources.set([]);
        return;
      }
      this.searchTimer = setTimeout(() => this.load(culture, query), 250);
    });
  }

  private load(culture: string, query: string): void {
    this.service.resources(culture, query || undefined).subscribe({
      next: (items) => this.resources.set(items),
      error: () => this.resources.set([]),
    });
  }

  protected addCulture(idInput: HTMLInputElement, nameInput: HTMLInputElement): void {
    const id = idInput.value.trim();
    const name = nameInput.value.trim() || id;
    if (!id) {
      return;
    }
    this.service.createCulture({ id, name }).subscribe({
      next: () => {
        idInput.value = '';
        nameInput.value = '';
        this.cultures.reload();
        this.toast.success(this.translate.instant('localization.culture_added'));
      },
      error: () => this.toast.error(this.translate.instant('localization.culture_add_failed')),
    });
  }

  protected saveResource(key: string, value: string): void {
    const cultureId = this.cultureId();
    if (!cultureId) {
      return;
    }
    this.service.upsertResource({ key, value, cultureId }).subscribe({
      next: () => this.toast.success(this.translate.instant('localization.resource_saved')),
      error: () => this.toast.error(this.translate.instant('localization.resource_save_failed')),
    });
  }

  protected addResource(keyInput: HTMLInputElement, valueInput: HTMLInputElement): void {
    const key = keyInput.value.trim();
    const cultureId = this.cultureId();
    if (!key || !cultureId) {
      return;
    }
    this.service.upsertResource({ key, value: valueInput.value, cultureId }).subscribe({
      next: () => {
        keyInput.value = '';
        valueInput.value = '';
        this.load(cultureId, this.query());
      },
      error: () => this.toast.error(this.translate.instant('localization.resource_add_failed')),
    });
  }

  protected removeResource(r: AdminResourceDto): void {
    this.service.deleteResource(r.id).subscribe({
      next: () => this.load(this.cultureId(), this.query()),
      error: () => this.toast.error(this.translate.instant('localization.resource_delete_failed')),
    });
  }
}
