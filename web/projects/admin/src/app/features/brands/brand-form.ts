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
  AdminBrandsService,
  type AdminBrandDto,
  type BrandUpsertRequest,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { MultiLangInput, type MultiLangValue } from '../../shared/multi-lang-input';

interface BrandModel {
  name: MultiLangValue;
  slug: string;
  description: MultiLangValue;
  isPublished: boolean;
}

function emptyModel(): BrandModel {
  return { name: { ar: '', en: '' }, slug: '', description: { ar: '', en: '' }, isPublished: true };
}

/**
 * Create / edit a brand inside an offcanvas panel. Opened by the brand list via
 * `NgbOffcanvas.open(AdminBrandForm)`, which calls {@link init} with the row
 * being edited (or `null` to create). The panel seeds itself from that row — no
 * extra fetch — and reports back through {@link saved} / {@link cancelled} so the
 * list can close the panel and refresh.
 */
@Component({
  selector: 'app-admin-brand-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, TranslatePipe, MultiLangInput],
  templateUrl: './brand-form.html',
})
export class AdminBrandForm {
  private readonly service = inject(AdminBrandsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  /** Emitted after a successful create/update — the host closes + reloads. */
  readonly saved = output<void>();
  /** Emitted when the user cancels or closes the panel without saving. */
  readonly cancelled = output<void>();

  /** The brand being edited, or `null` while creating. */
  private readonly editing = signal<AdminBrandDto | null>(null);
  protected readonly isNew = computed(() => this.editing() === null);

  protected readonly model = signal<BrandModel>(emptyModel());
  protected readonly f = form(this.model, (path) => {
    required(path.name.ar, { message: 'Name is required' });
  });
  protected readonly err = firstError;
  protected readonly serverError = signal<string | null>(null);

  /** Seed the panel for creating (`null`) or editing the given brand. */
  init(brand: AdminBrandDto | null): void {
    this.editing.set(brand);
    this.serverError.set(null);
    this.model.set(
      brand
        ? {
            name: { ar: brand.name ?? '', en: brand.nameEn ?? '' },
            slug: brand.slug ?? '',
            description: { ar: brand.description ?? '', en: brand.descriptionEn ?? '' },
            isPublished: brand.isPublished,
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
      const body: BrandUpsertRequest = {
        name: m.name.ar,
        nameEn: m.name.en || null,
        slug: m.slug || null,
        description: m.description.ar || null,
        descriptionEn: m.description.en || null,
        isPublished: m.isPublished,
      };
      try {
        const current = this.editing();
        if (current) {
          await firstValueFrom(this.service.update(current.id, body));
          this.toast.success(this.translate.instant('brands.updated_ok'));
        } else {
          await firstValueFrom(this.service.create(body));
          this.toast.success(this.translate.instant('brands.created_ok'));
        }
        this.saved.emit();
      } catch {
        this.serverError.set(this.translate.instant('brands.save_failed'));
      }
      return undefined;
    });
  }
}
