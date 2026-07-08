import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminCmsService, type AdminPageDto } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';

/**
 * CMS page browser: a full-width list. Creating and editing happen on their own
 * page (`/pages/new`, `/pages/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-pages',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader, TableSkeleton],
  templateUrl: './pages.html',
})
export class AdminPages {
  private readonly service = inject(AdminCmsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.pagesResource();
  protected readonly deletingId = signal<number | null>(null);

  protected remove(p: AdminPageDto): void {
    if (!confirm(this.translate.instant('pages.confirm_delete', { name: p.name ?? '#' + p.id }))) {
      return;
    }
    this.deletingId.set(p.id);
    this.service.deletePage(p.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('pages.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('pages.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
