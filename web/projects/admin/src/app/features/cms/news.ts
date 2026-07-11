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
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';

/**
 * News browser: the article list. Creating and editing an article happen on their
 * own page (`/news/new`, `/news/:id`), where the article's category (success story,
 * activity or alert) is picked. The three categories are code-owned/seeded, so there
 * is no category management here.
 */
@Component({
  selector: 'app-admin-news',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, Icon, TranslatePipe, PageHeader, TableSkeleton],
  templateUrl: './news.html',
})
export class AdminNews {
  private readonly service = inject(AdminCmsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly items = this.service.newsItemsResource();
  protected readonly deletingId = signal<number | null>(null);

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
}
