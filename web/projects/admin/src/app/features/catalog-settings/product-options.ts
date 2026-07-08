import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AdminProductOptionsService,
  type AdminProductOptionListItem,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';

/**
 * Product option browser (Color, Size, …): a full-width list. Creating and
 * editing happen on their own page (`/product-options/new`,
 * `/product-options/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-product-options',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader, TableSkeleton],
  templateUrl: './product-options.html',
})
export class AdminProductOptions {
  private readonly service = inject(AdminProductOptionsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource();
  protected readonly deletingId = signal<number | null>(null);

  protected remove(o: AdminProductOptionListItem): void {
    if (!confirm(this.translate.instant('options.confirm_delete', { name: o.name ?? '#' + o.id }))) {
      return;
    }
    this.deletingId.set(o.id);
    this.service.delete(o.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('options.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('options.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
