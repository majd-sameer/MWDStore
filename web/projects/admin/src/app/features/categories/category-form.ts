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
  AdminCategoriesService,
  type CategoryUpsertRequest,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';

interface CategoryModel {
  name: string;
  slug: string;
  description: string;
  displayOrder: number;
  isPublished: boolean;
  includeInMenu: boolean;
}

function emptyModel(): CategoryModel {
  return {
    name: '',
    slug: '',
    description: '',
    displayOrder: 0,
    isPublished: true,
    includeInMenu: true,
  };
}

/**
 * Create / edit a category on its own page (mirrors the product form). The `:id`
 * route param is either `new` (create) or a numeric id (edit, seeded from
 * `GET /api/admin/categories/{id}`). Saving returns to the category list.
 */
@Component({
  selector: 'app-admin-category-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/categories" class="text-decoration-none">← {{ 'categories.title' | translate }}</a>
    </nav>
    <app-page-header
      [title]="(isNew() ? 'categories.new_title' : 'categories.edit_title') | translate"
    />

    @if (!isNew() && existing.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (!isNew() && existing.error()) {
      <div class="alert alert-danger">{{ 'common.error_api' | translate }}</div>
    } @else {
      <div class="row g-4">
        <div class="col-lg-7">
          <div class="card border-0 shadow-sm">
            <div class="card-body">
              @if (serverError(); as message) {
                <div class="alert alert-danger" role="alert">{{ message }}</div>
              }
              <form (submit)="onSubmit($event)" novalidate>
                <lib-form-field
                  [label]="'categories.name' | translate"
                  controlId="cat-name"
                  [required]="true"
                  [error]="err(f.name())"
                >
                  <input id="cat-name" type="text" class="form-control"
                    [class.is-invalid]="!!err(f.name())" [formField]="f.name" />
                </lib-form-field>
                <lib-form-field
                  [label]="'categories.slug' | translate"
                  controlId="cat-slug"
                  [hint]="'categories.slug_hint' | translate"
                >
                  <input id="cat-slug" type="text" class="form-control" [formField]="f.slug" />
                </lib-form-field>
                <lib-form-field [label]="'categories.description' | translate" controlId="cat-desc">
                  <textarea id="cat-desc" rows="3" class="form-control" [formField]="f.description"></textarea>
                </lib-form-field>
                <lib-form-field [label]="'categories.display_order' | translate" controlId="cat-order">
                  <input id="cat-order" type="number" class="form-control" [formField]="f.displayOrder" />
                </lib-form-field>
                <div class="form-check form-switch">
                  <input id="cat-pub" type="checkbox" class="form-check-input" [formField]="f.isPublished" />
                  <label for="cat-pub" class="form-check-label">{{ 'categories.published' | translate }}</label>
                </div>
                <div class="form-check form-switch mb-3">
                  <input id="cat-menu" type="checkbox" class="form-check-input" [formField]="f.includeInMenu" />
                  <label for="cat-menu" class="form-check-label">{{ 'categories.show_in_menu' | translate }}</label>
                </div>

                <div class="form-actions">
                  <button libButton variant="primary" [disabled]="f().submitting()">
                    {{ (f().submitting() ? 'categories.saving' : isNew() ? 'categories.create' : 'categories.save') | translate }}
                  </button>
                  <a routerLink="/categories" class="btn btn-outline-secondary">
                    {{ 'common.cancel' | translate }}
                  </a>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class AdminCategoryForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminCategoriesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly categoryId = computed(() => Number(this.idParam().get('id')));

  protected readonly existing = this.service.getResource(this.categoryId);

  protected readonly model = signal<CategoryModel>(emptyModel());
  protected readonly f = form(this.model, (path) => {
    required(path.name, { message: 'Name is required' });
  });
  protected readonly err = firstError;
  protected readonly serverError = signal<string | null>(null);

  private seeded = false;

  constructor() {
    // Seed the form once the category arrives (edit mode only).
    effect(() => {
      if (this.isNew() || this.seeded) {
        return;
      }
      const c = this.existing.value();
      if (!c) {
        return;
      }
      this.seeded = true;
      this.model.set({
        name: c.name ?? '',
        slug: c.slug ?? '',
        description: c.description ?? '',
        displayOrder: c.displayOrder,
        isPublished: c.isPublished,
        includeInMenu: c.includeInMenu,
      });
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      const body: CategoryUpsertRequest = {
        name: m.name,
        slug: m.slug || null,
        description: m.description || null,
        displayOrder: Number(m.displayOrder),
        isPublished: m.isPublished,
        includeInMenu: m.includeInMenu,
      };
      try {
        if (this.isNew()) {
          await firstValueFrom(this.service.create(body));
          this.toast.success(this.translate.instant('categories.created_ok'));
        } else {
          await firstValueFrom(this.service.update(this.categoryId(), body));
          this.toast.success(this.translate.instant('categories.updated_ok'));
        }
        await this.router.navigate(['/categories']);
      } catch {
        this.serverError.set(this.translate.instant('categories.save_failed'));
      }
      return undefined;
    });
  }
}
