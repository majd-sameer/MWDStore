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
import { AdminCmsService, type PageUpsertRequest } from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';
import { MultiLangInput, type MultiLangValue } from '../../shared/multi-lang-input';

interface PageModel {
  name: MultiLangValue;
  slug: string;
  body: MultiLangValue;
  metaTitle: MultiLangValue;
  metaKeywords: MultiLangValue;
  metaDescription: MultiLangValue;
  isPublished: boolean;
}

function emptyModel(): PageModel {
  return {
    name: { ar: '', en: '' },
    slug: '',
    body: { ar: '', en: '' },
    metaTitle: { ar: '', en: '' },
    metaKeywords: { ar: '', en: '' },
    metaDescription: { ar: '', en: '' },
    isPublished: true,
  };
}

/**
 * Create / edit a CMS page on its own page (mirrors the product form). The pages
 * API has no single-fetch endpoint, but the list DTO carries the full body +
 * meta, so edit mode seeds from the list resource. Saving returns to the list.
 */
@Component({
  selector: 'app-admin-page-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader, MultiLangInput],
  templateUrl: './page-form.html',
})
export class AdminPageForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminCmsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly pageId = computed(() => Number(this.idParam().get('id')));

  protected readonly list = this.service.pagesResource();
  private readonly existing = computed(
    () => this.list.value()?.find((p) => p.id === this.pageId()) ?? null,
  );

  protected readonly model = signal<PageModel>(emptyModel());
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
      const p = this.existing();
      if (!p) {
        return;
      }
      this.seeded = true;
      this.model.set({
        name: { ar: p.name ?? '', en: p.nameEn ?? '' },
        slug: p.slug ?? '',
        body: { ar: p.body ?? '', en: p.bodyEn ?? '' },
        metaTitle: { ar: p.metaTitle ?? '', en: p.metaTitleEn ?? '' },
        metaKeywords: { ar: p.metaKeywords ?? '', en: p.metaKeywordsEn ?? '' },
        metaDescription: { ar: p.metaDescription ?? '', en: p.metaDescriptionEn ?? '' },
        isPublished: p.isPublished,
      });
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      const body: PageUpsertRequest = {
        name: m.name.ar,
        nameEn: m.name.en || null,
        slug: m.slug || null,
        body: m.body.ar || null,
        bodyEn: m.body.en || null,
        metaTitle: m.metaTitle.ar || null,
        metaTitleEn: m.metaTitle.en || null,
        metaKeywords: m.metaKeywords.ar || null,
        metaKeywordsEn: m.metaKeywords.en || null,
        metaDescription: m.metaDescription.ar || null,
        metaDescriptionEn: m.metaDescription.en || null,
        isPublished: m.isPublished,
      };
      try {
        if (this.isNew()) {
          await firstValueFrom(this.service.createPage(body));
          this.toast.success(this.translate.instant('pages.created_ok'));
        } else {
          await firstValueFrom(this.service.updatePage(this.pageId(), body));
          this.toast.success(this.translate.instant('pages.updated_ok'));
        }
        await this.router.navigate(['/pages']);
      } catch {
        this.serverError.set(this.translate.instant('pages.save_failed'));
      }
      return undefined;
    });
  }
}
