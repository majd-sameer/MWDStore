import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  signal,
} from '@angular/core';
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
  imports: [Button, TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'localization.title' | translate"
      [subtitle]="'localization.subtitle' | translate"
    />

    <div class="card border-0 shadow-sm">
      <div class="card-body">
        <div class="row g-2 align-items-end mb-3">
          <div class="col-md-3">
            <label class="form-label small" for="loc-culture">{{ 'localization.culture' | translate }}</label>
            <select id="loc-culture" class="form-select form-select-sm"
              (change)="cultureId.set($any($event.target).value)">
              <option value="">{{ 'common.choose' | translate }}</option>
              @for (c of cultures.value() ?? []; track c.id) {
                <option value="{{ c.id }}" [selected]="cultureId() === c.id">
                  {{ c.id }} — {{ c.name }}
                </option>
              }
            </select>
          </div>
          <div class="col-md-4">
            <label class="form-label small" for="loc-search">{{ 'localization.search' | translate }}</label>
            <input id="loc-search" type="text" class="form-control form-control-sm"
              (input)="query.set($any($event.target).value)" />
          </div>
          <div class="col-md-5 d-flex gap-2 justify-content-end">
            <input type="text" class="form-control form-control-sm w-auto"
              [placeholder]="'localization.new_culture_id_ph' | translate" #cultId />
            <input type="text" class="form-control form-control-sm w-auto"
              [placeholder]="'common.name' | translate" #cultName />
            <button type="button" libButton variant="secondary" size="sm" [outline]="true"
              (click)="addCulture(cultId, cultName)">
              {{ 'localization.add_culture' | translate }}
            </button>
          </div>
        </div>

        @if (cultureId()) {
          <table class="table table-sm align-middle mb-3">
            <thead>
              <tr>
                <th style="width: 40%">{{ 'common.key' | translate }}</th>
                <th>{{ 'common.value' | translate }}</th>
                <th style="width: 4rem"></th>
              </tr>
            </thead>
            <tbody>
              @for (r of resources(); track r.id) {
                <tr>
                  <td class="font-monospace small">{{ r.key }}</td>
                  <td>
                    <input type="text" class="form-control form-control-sm" [value]="r.value ?? ''"
                      (change)="saveResource(r.key, $any($event.target).value)" />
                  </td>
                  <td class="text-end">
                    <button type="button" class="btn btn-sm btn-outline-danger"
                      (click)="removeResource(r)">✕</button>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="3" class="text-center text-body-secondary py-4">
                    {{ 'localization.no_resources' | translate }}
                  </td>
                </tr>
              }
            </tbody>
          </table>

          <div class="row g-2 align-items-end">
            <div class="col-md-5">
              <label class="form-label small" for="res-key">{{ 'localization.new_key' | translate }}</label>
              <input id="res-key" type="text" class="form-control form-control-sm" #resKey />
            </div>
            <div class="col-md-5">
              <label class="form-label small" for="res-value">{{ 'common.value' | translate }}</label>
              <input id="res-value" type="text" class="form-control form-control-sm" #resValue />
            </div>
            <div class="col-md-2">
              <button type="button" libButton variant="primary" size="sm"
                (click)="addResource(resKey, resValue)">
                {{ 'common.add' | translate }}
              </button>
            </div>
          </div>
        } @else {
          <div class="alert alert-light border mb-0">{{ 'localization.choose_culture' | translate }}</div>
        }
      </div>
    </div>
  `,
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
