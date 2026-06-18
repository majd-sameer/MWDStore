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

interface NewsModel {
  name: string;
  slug: string;
  shortContent: string;
  fullContent: string;
  isPublished: boolean;
}

function emptyModel(): NewsModel {
  return { name: '', slug: '', shortContent: '', fullContent: '', isPublished: true };
}

/**
 * Create / edit a news article on its own page (mirrors the product form). Edit
 * mode fetches the full detail (`GET /api/admin/news/items/{id}`) to seed body,
 * thumbnail and category assignment. News categories are managed on the list page.
 */
@Component({
  selector: 'app-admin-news-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/news" class="text-decoration-none">← {{ 'news.title' | translate }}</a>
    </nav>
    <app-page-header [title]="(isNew() ? 'news.new_title' : 'news.edit_title') | translate" />

    @if (!isNew() && loading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (!isNew() && loadError()) {
      <div class="alert alert-danger">{{ 'news.load_one_failed' | translate }}</div>
    } @else {
      <div class="row g-4">
        <div class="col-lg-9">
          <div class="card border-0 shadow-sm">
            <div class="card-body">
              @if (serverError(); as message) {
                <div class="alert alert-danger" role="alert">{{ message }}</div>
              }
              <form (submit)="onSubmit($event)" novalidate>
                <div class="row">
                  <div class="col-md-6">
                    <lib-form-field [label]="'news.title_label' | translate" controlId="nw-name" [required]="true" [error]="err(f.name())">
                      <input id="nw-name" type="text" class="form-control"
                        [class.is-invalid]="!!err(f.name())" [formField]="f.name" />
                    </lib-form-field>
                  </div>
                  <div class="col-md-6">
                    <lib-form-field [label]="'common.slug' | translate" controlId="nw-slug" [hint]="'common.slug_hint' | translate">
                      <input id="nw-slug" type="text" class="form-control" [formField]="f.slug" />
                    </lib-form-field>
                  </div>
                </div>
                <lib-form-field [label]="'news.short_content' | translate" controlId="nw-short">
                  <textarea id="nw-short" rows="2" class="form-control" [formField]="f.shortContent"></textarea>
                </lib-form-field>
                <lib-form-field [label]="'news.full_content' | translate" controlId="nw-full">
                  <textarea id="nw-full" rows="10" class="form-control font-monospace"
                    [formField]="f.fullContent"></textarea>
                </lib-form-field>

                <label class="form-label" for="nw-thumb">{{ 'news.thumbnail' | translate }}</label>
                <div class="d-flex align-items-center gap-3 mb-2">
                  @if (thumbnailUrl(); as url) {
                    <img [src]="url" [alt]="'news.thumbnail' | translate" class="rounded border"
                      style="width: 56px; height: 56px; object-fit: cover" />
                    <button type="button" class="btn btn-sm btn-outline-danger" (click)="clearThumbnail()">
                      {{ 'common.remove' | translate }}
                    </button>
                  }
                </div>
                <input id="nw-thumb" type="file" class="form-control form-control-sm mb-3" accept="image/*"
                  [disabled]="uploading()" (change)="onThumbnailSelected($event)" />

                <div class="form-label">{{ 'nav.categories' | translate }}</div>
                <div class="border rounded p-2 mb-3">
                  @for (c of categories.value() ?? []; track c.id) {
                    <div class="form-check">
                      <input type="checkbox" class="form-check-input" id="nw-cat-{{ c.id }}"
                        [checked]="categoryIds().includes(c.id)"
                        (change)="toggleCategory(c.id)" />
                      <label class="form-check-label" for="nw-cat-{{ c.id }}">{{ c.name }}</label>
                    </div>
                  } @empty {
                    <span class="text-body-secondary small">{{ 'news.no_categories_defined' | translate }}</span>
                  }
                </div>

                <div class="form-check form-switch mb-3">
                  <input id="nw-pub" type="checkbox" class="form-check-input" [formField]="f.isPublished" />
                  <label for="nw-pub" class="form-check-label">{{ 'common.published' | translate }}</label>
                </div>

                <div class="form-actions">
                  <button libButton variant="primary" [disabled]="f().submitting() || uploading()">
                    {{ (f().submitting() ? 'common.saving' : isNew() ? 'news.create' : 'common.save_changes') | translate }}
                  </button>
                  <a routerLink="/news" class="btn btn-outline-secondary">{{ 'common.cancel' | translate }}</a>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    }
  `,
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
    required(path.name, { message: 'Title is required' });
  });
  protected readonly err = firstError;

  constructor() {
    if (!this.isNew()) {
      this.loading.set(true);
      this.service.getNewsItem(this.newsId()).subscribe({
        next: (detail) => {
          this.model.set({
            name: detail.name ?? '',
            slug: detail.slug ?? '',
            shortContent: detail.shortContent ?? '',
            fullContent: detail.fullContent ?? '',
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
        name: m.name,
        slug: m.slug || null,
        shortContent: m.shortContent || null,
        fullContent: m.fullContent || null,
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
