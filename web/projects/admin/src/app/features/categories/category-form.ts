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
import { MultiLangInput, type MultiLangValue } from '../../shared/multi-lang-input';

interface CategoryModel {
  name: MultiLangValue;
  slug: string;
  description: MultiLangValue;
  displayOrder: number;
  isPublished: boolean;
  includeInMenu: boolean;
}

function emptyModel(): CategoryModel {
  return {
    name: { ar: '', en: '' },
    slug: '',
    description: { ar: '', en: '' },
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
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader, MultiLangInput],
  templateUrl: './category-form.html',
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
    required(path.name.ar, { message: 'Name is required' });
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
        name: { ar: c.name ?? '', en: c.nameEn ?? '' },
        slug: c.slug ?? '',
        description: { ar: c.description ?? '', en: c.descriptionEn ?? '' },
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
        name: m.name.ar,
        nameEn: m.name.en || null,
        slug: m.slug || null,
        description: m.description.ar || null,
        descriptionEn: m.description.en || null,
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
