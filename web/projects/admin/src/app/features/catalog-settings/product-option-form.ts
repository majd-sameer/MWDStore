import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  form,
  FormField as Control,
  required,
  submit,
} from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminProductOptionsService } from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';

/**
 * Create / edit a product option (Color, Size, …) on its own page. The options
 * API has no single-fetch endpoint, so edit mode seeds from the list resource.
 */
@Component({
  selector: 'app-admin-product-option-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/product-options" class="text-decoration-none">← {{ 'options.title' | translate }}</a>
    </nav>
    <app-page-header [title]="(isNew() ? 'options.new_title' : 'options.edit_title') | translate" />

    @if (!isNew() && list.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (!isNew() && list.error()) {
      <div class="alert alert-danger">{{ 'options.load_one_failed' | translate }}</div>
    } @else {
      <div class="row g-4">
        <div class="col-lg-7">
          <div class="card border-0 shadow-sm">
            <div class="card-body">
              @if (serverError(); as message) {
                <div class="alert alert-danger" role="alert">{{ message }}</div>
              }
              <form (submit)="onSubmit($event)" novalidate>
                <lib-form-field [label]="'common.name' | translate" controlId="opt-name" [required]="true" [error]="err(f.name())">
                  <input id="opt-name" type="text" class="form-control"
                    [class.is-invalid]="!!err(f.name())" [formField]="f.name" />
                </lib-form-field>

                <div class="form-actions">
                  <button libButton variant="primary" [disabled]="f().submitting()">
                    {{ (f().submitting() ? 'common.saving' : isNew() ? 'options.create' : 'common.save_changes') | translate }}
                  </button>
                  <a routerLink="/product-options" class="btn btn-outline-secondary">{{ 'common.cancel' | translate }}</a>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class AdminProductOptionForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminProductOptionsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly optionId = computed(() => Number(this.idParam().get('id')));

  protected readonly list = this.service.listResource();
  private readonly existing = computed(
    () => this.list.value()?.find((o) => o.id === this.optionId()) ?? null,
  );

  protected readonly model = signal<{ name: string }>({ name: '' });
  protected readonly f = form(this.model, (path) => {
    required(path.name, { message: 'Name is required' });
  });
  protected readonly err = firstError;
  protected readonly serverError = signal<string | null>(null);

  private seeded = false;

  constructor() {
    effect(() => {
      if (this.isNew() || this.seeded) {
        return;
      }
      const o = this.existing();
      if (!o) {
        return;
      }
      this.seeded = true;
      this.model.set({ name: o.name ?? '' });
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      try {
        if (this.isNew()) {
          await firstValueFrom(this.service.create({ name: this.model().name }));
          this.toast.success(this.translate.instant('options.created_ok'));
        } else {
          await firstValueFrom(
            this.service.update(this.optionId(), { name: this.model().name }),
          );
          this.toast.success(this.translate.instant('options.updated_ok'));
        }
        await this.router.navigate(['/product-options']);
      } catch {
        this.serverError.set(this.translate.instant('options.save_failed'));
      }
      return undefined;
    });
  }
}
