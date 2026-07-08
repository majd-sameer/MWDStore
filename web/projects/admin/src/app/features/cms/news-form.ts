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
  type NewsItemUpsertRequest,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';
import { MultiLangInput, type MultiLangValue } from '../../shared/multi-lang-input';

interface NewsModel {
  name: MultiLangValue;
  slug: string;
  shortContent: MultiLangValue;
  fullContent: MultiLangValue;
  isPublished: boolean;
}

function emptyModel(): NewsModel {
  return {
    name: { ar: '', en: '' },
    slug: '',
    shortContent: { ar: '', en: '' },
    fullContent: { ar: '', en: '' },
    isPublished: true,
  };
}

/**
 * Create / edit a news article on its own page (mirrors the product form). Edit
 * mode fetches the full detail (`GET /api/admin/news/items/{id}`) to seed body,
 * thumbnail and category assignment. News categories are managed on the list page.
 */
@Component({
  selector: 'app-admin-news-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader, MultiLangInput],
  templateUrl: './news-form.html',
})
export class AdminNewsForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminCmsService);
  private readonly media = inject(AdminMediaService);
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
  protected readonly categoryIds = signal<number[]>([]);
  protected readonly thumbnailId = signal<number | null>(null);
  protected readonly thumbnailUrl = signal<string | null>(null);
  protected readonly uploading = signal(false);

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
          });
          this.categoryIds.set(detail.categoryIds);
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

  protected toggleCategory(id: number): void {
    this.categoryIds.update((ids) =>
      ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id],
    );
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
        categoryIds: this.categoryIds(),
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
