import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminTaxService } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, ConfirmService, Icon, TableCards, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Tax browser: the tax-classes manager alongside the per-destination rates list.
 * Creating and editing a rate happen on their own page (`/taxes/new`,
 * `/taxes/:id`); tax classes stay here as a lightweight secondary entity.
 */
@Component({
  selector: 'app-admin-taxes',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Button, Icon, TranslatePipe, PageHeader, TableCards],
  template: `
    <app-page-header
      [title]="'taxes.title' | translate"
      [subtitle]="'taxes.subtitle' | translate"
    >
      <a routerLink="/taxes/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'taxes.new' | translate }}
      </a>
    </app-page-header>

    <div class="row g-4">
      <div class="col-lg-4">
        <div class="card border-0 shadow-sm">
          <div class="card-header bg-body fw-semibold">{{ 'taxes.classes_title' | translate }}</div>
          <div class="card-body">
            @for (c of classes.value() ?? []; track c.id) {
              <div class="d-flex align-items-center gap-2 mb-2">
                <input type="text" class="form-control form-control-sm" [value]="c.name"
                  (change)="renameClass(c.id, $any($event.target).value)" />
                <button type="button" class="btn btn-sm btn-outline-danger"
                  (click)="removeClass(c.id, c.name)">✕</button>
              </div>
            } @empty {
              <p class="text-body-secondary small">{{ 'taxes.no_classes' | translate }}</p>
            }
            <div class="d-flex gap-2 mt-3">
              <input type="text" class="form-control form-control-sm"
                [placeholder]="'taxes.new_class_ph' | translate" #className />
              <button type="button" libButton variant="secondary" [outline]="true"
                (click)="addClass(className)">
                {{ 'common.add' | translate }}
              </button>
            </div>
          </div>
        </div>
      </div>

      <div class="col-lg-8">
        <div class="card border-0 shadow-sm">
          <div class="card-header bg-body fw-semibold">{{ 'taxes.rates_title' | translate }}</div>
          <div class="card-body">
            @if (rates.isLoading()) {
              <div class="text-center py-4">
                <div class="spinner-border text-primary" role="status">
                  <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
                </div>
              </div>
            } @else if (rates.error()) {
              <div class="alert alert-danger mb-0">{{ 'common.error_api' | translate }}</div>
            } @else if (rates.value(); as rows) {
              <div class="table-responsive">
                <table class="table table-hover align-middle mb-0" libTableCards>
                  <thead>
                    <tr>
                      <th>{{ 'taxes.tax_class' | translate }}</th>
                      <th>{{ 'common.country' | translate }}</th>
                      <th>{{ 'common.state' | translate }}</th>
                      <th>{{ 'common.zip' | translate }}</th>
                      <th class="text-end">{{ 'taxes.col_rate' | translate }}</th>
                      <th class="text-end">{{ 'common.actions' | translate }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (r of rows; track r.id) {
                      <tr>
                        <td>
                          <a [routerLink]="['/taxes', r.id]" class="text-decoration-none fw-medium">{{ r.taxClassName }}</a>
                        </td>
                        <td>{{ r.countryName ?? ('common.any' | translate) }}</td>
                        <td>{{ r.stateOrProvinceName ?? ('common.any' | translate) }}</td>
                        <td>{{ r.zipCode ?? ('common.any' | translate) }}</td>
                        <td class="text-end">{{ r.rate }}</td>
                        <td class="text-end">
                          <span class="d-inline-flex gap-1">
                            <a [routerLink]="['/taxes', r.id]" class="action-btn" [title]="'common.edit' | translate">
                              <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                            </a>
                            <button
                              type="button"
                              class="action-btn action-btn-danger"
                              [title]="'common.delete' | translate"
                              [disabled]="deletingId() === r.id"
                              (click)="removeRate(r.id)"
                            >
                              <lib-icon name="trash" [size]="15" [label]="'common.delete' | translate" />
                            </button>
                          </span>
                        </td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="6">
                          <div class="empty-state">
                            <span class="empty-icon"><lib-icon name="box" [size]="26" /></span>
                            <div class="empty-title">{{ 'taxes.empty' | translate }}</div>
                            <a routerLink="/taxes/new" class="btn btn-primary btn-sm mt-2">
                              {{ 'taxes.create_first' | translate }}
                            </a>
                          </div>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `,
})
export class AdminTaxes {
  private readonly service = inject(AdminTaxService);
  private readonly confirmService = inject(ConfirmService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly classes = this.service.classesResource();
  protected readonly rates = this.service.ratesResource();
  protected readonly deletingId = signal<number | null>(null);

  // ----- Classes -------------------------------------------------------------

  protected addClass(input: HTMLInputElement): void {
    const name = input.value.trim();
    if (!name) {
      return;
    }
    this.service.createClass({ name }).subscribe({
      next: () => {
        input.value = '';
        this.classes.reload();
      },
      error: () => this.toast.error(this.translate.instant('taxes.class_create_failed')),
    });
  }

  protected renameClass(id: number, name: string): void {
    const trimmed = name.trim();
    if (!trimmed) {
      return;
    }
    this.service.updateClass(id, { name: trimmed }).subscribe({
      next: () => this.toast.success(this.translate.instant('taxes.class_updated')),
      error: () => this.toast.error(this.translate.instant('taxes.class_update_failed')),
    });
  }

  protected async removeClass(id: number, name: string | null): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: this.translate.instant('common.confirm_title'),
      message: this.translate.instant('taxes.confirm_delete_class', { name: name ?? '' }),
      okText: this.translate.instant('common.delete'),
      cancelText: this.translate.instant('common.cancel'),
      destructive: true,
    });
    if (!ok) {
      return;
    }
    this.service.deleteClass(id).subscribe({
      next: () => {
        this.classes.reload();
        this.rates.reload();
      },
      error: () =>
        this.toast.error(this.translate.instant('taxes.class_delete_failed')),
    });
  }

  // ----- Rates ----------------------------------------------------------------

  protected async removeRate(id: number): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: this.translate.instant('common.confirm_title'),
      message: this.translate.instant('taxes.confirm_delete_rate'),
      okText: this.translate.instant('common.delete'),
      cancelText: this.translate.instant('common.cancel'),
      destructive: true,
    });
    if (!ok) {
      return;
    }
    this.deletingId.set(id);
    this.service.deleteRate(id).subscribe({
      next: () => {
        this.deletingId.set(null);
        this.rates.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('taxes.rate_delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
