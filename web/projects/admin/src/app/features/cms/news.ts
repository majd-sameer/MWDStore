import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  AdminCmsService,
  type AdminNewsItemListItem,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { MultiLangInput, type MultiLangValue } from '../../shared/multi-lang-input';

/**
 * News browser: the article list with a news-category manager alongside.
 * Creating and editing an article happen on their own page (`/news/new`,
 * `/news/:id`); categories stay here as a secondary entity.
 */
@Component({
  selector: 'app-admin-news',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, Button, Icon, TranslatePipe, PageHeader, MultiLangInput],
  templateUrl: './news.html',
})
export class AdminNews {
  private readonly service = inject(AdminCmsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly items = this.service.newsItemsResource();
  protected readonly categories = this.service.newsCategoriesResource();
  protected readonly deletingId = signal<number | null>(null);
  /** Bilingual name for the "add category" box. */
  protected readonly newCategoryName = signal<MultiLangValue>({ ar: '', en: '' });

  protected remove(n: AdminNewsItemListItem): void {
    if (!confirm(this.translate.instant('news.confirm_delete', { name: n.name ?? '#' + n.id }))) {
      return;
    }
    this.deletingId.set(n.id);
    this.service.deleteNewsItem(n.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('news.deleted_ok'));
        this.deletingId.set(null);
        this.items.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('news.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }

  // ----- Categories ------------------------------------------------------------

  protected addCategory(): void {
    const value = this.newCategoryName();
    const name = value.ar.trim();
    if (!name) {
      return;
    }
    this.service.createNewsCategory({ name, nameEn: value.en || null, isPublished: true }).subscribe({
      next: () => {
        this.newCategoryName.set({ ar: '', en: '' });
        this.categories.reload();
      },
      error: () => this.toast.error(this.translate.instant('news.category_create_failed')),
    });
  }

  protected renameCategory(id: number, value: MultiLangValue): void {
    const trimmed = value.ar.trim();
    if (!trimmed) {
      return;
    }
    this.service
      .updateNewsCategory(id, { name: trimmed, nameEn: value.en || null, isPublished: true })
      .subscribe({
        next: () => this.toast.success(this.translate.instant('news.category_updated')),
        error: () => this.toast.error(this.translate.instant('news.category_update_failed')),
      });
  }

  protected removeCategory(id: number, name: string | null): void {
    if (!confirm(this.translate.instant('news.confirm_delete_category', { name: name ?? '' }))) {
      return;
    }
    this.service.deleteNewsCategory(id).subscribe({
      next: () => this.categories.reload(),
      error: () => this.toast.error(this.translate.instant('news.category_delete_failed')),
    });
  }
}
