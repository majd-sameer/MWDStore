import {
  ChangeDetectionStrategy,
  Component,
  computed,
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
  AdminCmsService,
  AdminMediaService,
  AdminProductsService,
  type AdminNewsCategoryDto,
  type NewsItemUpsertRequest,
  type ProductQuickSearchItem,
} from 'data-access';
import { LanguageService } from 'core';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';
import { type MultiLangValue } from '../../shared/multi-lang-input';
import { RichTextEditor } from '../../shared/rich-text-editor';
import { NEWS_TEMPLATES, type NewsTemplate } from './news-templates';

/** Fixed, code-known category slugs (mirror the backend seeder). */
const SLUG_SUCCESS_STORY = 'success-story';
const SLUG_ALERT = 'alert';

interface NewsModel {
  name: MultiLangValue;
  slug: string;
  shortContent: MultiLangValue;
  fullContent: MultiLangValue;
  isPublished: boolean;
  /** Alerts only — a `datetime-local` string (empty when unset). */
  alertExpiresOn: string;
  /** Alerts only — the CTA link (empty when unset). */
  alertCtaUrl: string;
}

function emptyModel(): NewsModel {
  return {
    name: { ar: '', en: '' },
    slug: '',
    shortContent: { ar: '', en: '' },
    fullContent: { ar: '', en: '' },
    isPublished: true,
    alertExpiresOn: '',
    alertCtaUrl: '',
  };
}

/** ISO 8601 → `yyyy-MM-ddTHH:mm` in local time, for a `datetime-local` input. */
function toDateTimeLocal(iso: string | null | undefined): string {
  if (!iso) {
    return '';
  }
  const d = new Date(iso);
  if (isNaN(d.getTime())) {
    return '';
  }
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** `datetime-local` string → ISO 8601 (UTC), or null when empty/invalid. */
function fromDateTimeLocal(value: string): string | null {
  if (!value) {
    return null;
  }
  const d = new Date(value);
  return isNaN(d.getTime()) ? null : d.toISOString();
}

/**
 * Create / edit a news article on its own page (mirrors the product form). Edit
 * mode fetches the full detail (`GET /api/admin/news/items/{id}`) to seed body,
 * thumbnail, category and the category-specific fields. The category is one of the
 * three fixed, seeded categories; picking it reveals only the fields it needs
 * (success story → linked product; alert → home-band expiry + CTA link).
 */
@Component({
  selector: 'app-admin-news-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader, RichTextEditor],
  templateUrl: './news-form.html',
  styleUrl: './news-form.scss',
})
export class AdminNewsForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminCmsService);
  private readonly media = inject(AdminMediaService);
  private readonly products = inject(AdminProductsService);
  private readonly language = inject(LanguageService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly newsId = computed(() => Number(this.idParam().get('id')));

  protected readonly categories = this.service.newsCategoriesResource();

  protected readonly loading = signal(false);
  protected readonly loadError = signal(false);
  protected readonly serverError = signal<string | null>(null);
  /** The single selected category id (one category per article). */
  protected readonly categoryId = signal<number | null>(null);
  protected readonly thumbnailId = signal<number | null>(null);
  protected readonly thumbnailUrl = signal<string | null>(null);
  protected readonly uploading = signal(false);

  // Success-story product picker.
  protected readonly searchResults = signal<ProductQuickSearchItem[]>([]);
  protected readonly productId = signal<number | null>(null);
  protected readonly productName = signal<string | null>(null);
  private searchTimer?: ReturnType<typeof setTimeout>;

  /** The slug of the currently selected category, driving the conditional fields. */
  protected readonly selectedSlug = computed(() => {
    const id = this.categoryId();
    return (this.categories.value() ?? []).find((c) => c.id === id)?.slug ?? null;
  });
  protected readonly isSuccessStory = computed(() => this.selectedSlug() === SLUG_SUCCESS_STORY);
  protected readonly isAlert = computed(() => this.selectedSlug() === SLUG_ALERT);

  /** Ready-made article layouts (one per category) offered above the body editors. */
  protected readonly templates = NEWS_TEMPLATES;

  protected readonly model = signal<NewsModel>(emptyModel());
  protected readonly f = form(this.model, (path) => {
    required(path.name.ar, { message: 'Title is required' });
  });
  protected readonly err = firstError;

  constructor() {
    if (!this.isNew()) {
      this.loading.set(true);
      this.service.getNewsItem(this.newsId()).subscribe({
        next: (detail) => {
          this.model.set({
            name: { ar: detail.name ?? '', en: detail.nameEn ?? '' },
            slug: detail.slug ?? '',
            shortContent: { ar: detail.shortContent ?? '', en: detail.shortContentEn ?? '' },
            fullContent: { ar: detail.fullContent ?? '', en: detail.fullContentEn ?? '' },
            isPublished: detail.isPublished,
            alertExpiresOn: toDateTimeLocal(detail.alertExpiresOn),
            alertCtaUrl: detail.alertCtaUrl ?? '',
          });
          this.categoryId.set(detail.categoryIds[0] ?? null);
          this.productId.set(detail.productId);
          this.productName.set(detail.productName);
          this.thumbnailId.set(detail.thumbnailImageId);
          this.thumbnailUrl.set(detail.thumbnailUrl);
          this.loading.set(false);
        },
        error: () => {
          this.loadError.set(true);
          this.loading.set(false);
        },
      });
    }
  }

  /** Label for a category option in the current UI language. */
  protected categoryLabel(c: AdminNewsCategoryDto): string {
    const en = c.nameEn ?? '';
    const ar = c.name ?? '';
    return (this.language.lang() === 'en' ? en || ar : ar || en) || (c.slug ?? '');
  }

  protected onCategoryChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.categoryId.set(value ? Number(value) : null);
  }

  /**
   * Replace both language bodies with the template's ready-made layout (after a
   * confirm when either body already has content), and pre-select the matching
   * category when the admin hasn't picked one yet.
   */
  protected applyTemplate(t: NewsTemplate): void {
    const { ar, en } = this.model().fullContent;
    if (
      (ar.trim() || en.trim()) &&
      !confirm(this.translate.instant('news.template.confirm_replace'))
    ) {
      return;
    }
    this.model.update((m) => ({ ...m, fullContent: { ar: t.ar, en: t.en } }));
    if (this.categoryId() === null) {
      const match = (this.categories.value() ?? []).find((c) => c.slug === t.key);
      if (match) {
        this.categoryId.set(match.id);
      }
    }
  }

  // ----- Success-story product picker -----------------------------------------

  protected searchProducts(query: string): void {
    const trimmed = query.trim();
    if (trimmed.length < 2) {
      this.searchResults.set([]);
      return;
    }
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => {
      this.products.quickSearch(trimmed).subscribe((items) => this.searchResults.set(items));
    }, 250);
  }

  protected selectProduct(item: ProductQuickSearchItem): void {
    this.productId.set(item.id);
    this.productName.set(item.name);
    this.searchResults.set([]);
  }

  protected clearProduct(): void {
    this.productId.set(null);
    this.productName.set(null);
    this.searchResults.set([]);
  }

  protected onThumbnailSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    this.uploading.set(true);
    this.media.upload(file).subscribe({
      next: (m) => {
        this.thumbnailId.set(m.id);
        this.thumbnailUrl.set(m.url);
        this.uploading.set(false);
        input.value = '';
      },
      error: () => {
        this.toast.error(this.translate.instant('news.upload_failed'));
        this.uploading.set(false);
      },
    });
  }

  protected clearThumbnail(): void {
    this.thumbnailId.set(null);
    this.thumbnailUrl.set(null);
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      const categoryId = this.categoryId();
      const body: NewsItemUpsertRequest = {
        name: m.name.ar,
        nameEn: m.name.en || null,
        slug: m.slug || null,
        shortContent: m.shortContent.ar || null,
        shortContentEn: m.shortContent.en || null,
        fullContent: m.fullContent.ar || null,
        fullContentEn: m.fullContent.en || null,
        isPublished: m.isPublished,
        thumbnailImageId: this.thumbnailId(),
        categoryIds: categoryId ? [categoryId] : [],
        // Category-specific fields are only sent for the category they belong to.
        productId: this.isSuccessStory() ? this.productId() : null,
        alertExpiresOn: this.isAlert() ? fromDateTimeLocal(m.alertExpiresOn) : null,
        alertCtaUrl: this.isAlert() ? m.alertCtaUrl || null : null,
      };
      try {
        if (this.isNew()) {
          await firstValueFrom(this.service.createNewsItem(body));
          this.toast.success(this.translate.instant('news.created_ok'));
        } else {
          await firstValueFrom(this.service.updateNewsItem(this.newsId(), body));
          this.toast.success(this.translate.instant('news.updated_ok'));
        }
        await this.router.navigate(['/news']);
      } catch {
        this.serverError.set(this.translate.instant('news.save_failed'));
      }
      return undefined;
    });
  }
}
