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
 * Create / edit a brand on its own page (mirrors the product form). The `:id`
 * route param is either `new` (create) or a numeric id (edit, seeded from
 * `GET /api/admin/brands/{id}`). Saving returns to the brand list.
 */
@Component({
  selector: 'app-admin-brand-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader, MultiLangInput],
  templateUrl: './brand-form.html',
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
    required(path.name.ar, { message: 'Name is required' });
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
        name: { ar: b.name ?? '', en: b.nameEn ?? '' },
        slug: b.slug ?? '',
        description: { ar: b.description ?? '', en: b.descriptionEn ?? '' },
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
        name: m.name.ar,
        nameEn: m.name.en || null,
        slug: m.slug || null,
        description: m.description.ar || null,
        descriptionEn: m.description.en || null,
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
