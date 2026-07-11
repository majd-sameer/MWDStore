import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import {
  form,
  FormField as Control,
  required,
  submit,
} from '@angular/forms/signals';
import {
  AdminCategoriesService,
  type AdminCategoryDto,
  type CategoryUpsertRequest,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
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
 * Create / edit a category inside an offcanvas panel. Opened by the category
 * list via `NgbOffcanvas.open(AdminCategoryForm)`, which calls {@link init} with
 * the row being edited (or `null` to create). The panel seeds itself from that
 * row — no extra fetch — and reports back through {@link saved} / {@link cancelled}
 * so the list can close the panel and refresh.
 */
@Component({
  selector: 'app-admin-category-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, TranslatePipe, MultiLangInput],
  templateUrl: './category-form.html',
})
export class AdminCategoryForm {
  private readonly service = inject(AdminCategoriesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  /** Emitted after a successful create/update — the host closes + reloads. */
  readonly saved = output<void>();
  /** Emitted when the user cancels or closes the panel without saving. */
  readonly cancelled = output<void>();

  /** The category being edited, or `null` while creating. */
  private readonly editing = signal<AdminCategoryDto | null>(null);
  protected readonly isNew = computed(() => this.editing() === null);

  protected readonly model = signal<CategoryModel>(emptyModel());
  protected readonly f = form(this.model, (path) => {
    required(path.name.ar, { message: 'Name is required' });
  });
  protected readonly err = firstError;
  protected readonly serverError = signal<string | null>(null);

  /** Seed the panel for creating (`null`) or editing the given category. */
  init(category: AdminCategoryDto | null): void {
    this.editing.set(category);
    this.serverError.set(null);
    this.model.set(
      category
        ? {
            name: { ar: category.name ?? '', en: category.nameEn ?? '' },
            slug: category.slug ?? '',
            description: {
              ar: category.description ?? '',
              en: category.descriptionEn ?? '',
            },
            displayOrder: category.displayOrder,
            isPublished: category.isPublished,
            includeInMenu: category.includeInMenu,
          }
        : emptyModel(),
    );
  }

  protected onCancel(): void {
    this.cancelled.emit();
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
        const current = this.editing();
        if (current) {
          await firstValueFrom(this.service.update(current.id, body));
          this.toast.success(this.translate.instant('categories.updated_ok'));
        } else {
          await firstValueFrom(this.service.create(body));
          this.toast.success(this.translate.instant('categories.created_ok'));
        }
        this.saved.emit();
      } catch {
        this.serverError.set(this.translate.instant('categories.save_failed'));
      }
      return undefined;
    });
  }
}
