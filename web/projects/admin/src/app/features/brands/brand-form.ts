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
import {
  AdminBrandsService,
  type BrandUpsertRequest,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';

interface BrandModel {
  name: string;
  slug: string;
  description: string;
  isPublished: boolean;
}

function emptyModel(): BrandModel {
  return { name: '', slug: '', description: '', isPublished: true };
}

/**
 * Create / edit a brand on its own page (mirrors the product form). The `:id`
 * route param is either `new` (create) or a numeric id (edit, seeded from
 * `GET /api/admin/brands/{id}`). Saving returns to the brand list.
 */
@Component({
  selector: 'app-admin-brand-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/brands" class="text-decoration-none">← {{ 'brands.title' | translate }}</a>
    </nav>
    <app-page-header [title]="(isNew() ? 'brands.new_title' : 'brands.edit_title') | translate" />

    @if (!isNew() && existing.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (!isNew() && existing.error()) {
      <div class="alert alert-danger">{{ 'brands.load_one_failed' | translate }}</div>
    } @else {
      <div class="row g-4">
        <div class="col-lg-7">
          <div class="card border-0 shadow-sm">
            <div class="card-body">
              @if (serverError(); as message) {
                <div class="alert alert-danger" role="alert">{{ message }}</div>
              }
              <form (submit)="onSubmit($event)" novalidate>
                <lib-form-field [label]="'common.name' | translate" controlId="brand-name" [required]="true" [error]="err(f.name())">
                  <input id="brand-name" type="text" class="form-control"
                    [class.is-invalid]="!!err(f.name())" [formField]="f.name" />
                </lib-form-field>
                <lib-form-field [label]="'common.slug' | translate" controlId="brand-slug" [hint]="'common.slug_hint' | translate">
                  <input id="brand-slug" type="text" class="form-control" [formField]="f.slug" />
                </lib-form-field>
                <lib-form-field [label]="'common.description' | translate" controlId="brand-desc">
                  <textarea id="brand-desc" rows="3" class="form-control" [formField]="f.description"></textarea>
                </lib-form-field>
                <div class="form-check form-switch mb-3">
                  <input id="brand-pub" type="checkbox" class="form-check-input" [formField]="f.isPublished" />
                  <label for="brand-pub" class="form-check-label">{{ 'common.published' | translate }}</label>
                </div>

                <div class="form-actions">
                  <button libButton variant="primary" [disabled]="f().submitting()">
                    {{ (f().submitting() ? 'common.saving' : isNew() ? 'brands.create' : 'common.save_changes') | translate }}
                  </button>
                  <a routerLink="/brands" class="btn btn-outline-secondary">{{ 'common.cancel' | translate }}</a>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class AdminBrandForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminBrandsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly brandId = computed(() => Number(this.idParam().get('id')));

  protected readonly existing = this.service.getResource(this.brandId);

  protected readonly model = signal<BrandModel>(emptyModel());
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
      const b = this.existing.value();
      if (!b) {
        return;
      }
      this.seeded = true;
      this.model.set({
        name: b.name ?? '',
        slug: b.slug ?? '',
        description: b.description ?? '',
        isPublished: b.isPublished,
      });
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      const body: BrandUpsertRequest = {
        name: m.name,
        slug: m.slug || null,
        description: m.description || null,
        isPublished: m.isPublished,
      };
      try {
        if (this.isNew()) {
          await firstValueFrom(this.service.create(body));
          this.toast.success(this.translate.instant('brands.created_ok'));
        } else {
          await firstValueFrom(this.service.update(this.brandId(), body));
          this.toast.success(this.translate.instant('brands.updated_ok'));
        }
        await this.router.navigate(['/brands']);
      } catch {
        this.serverError.set(this.translate.instant('brands.save_failed'));
      }
      return undefined;
    });
  }
}
