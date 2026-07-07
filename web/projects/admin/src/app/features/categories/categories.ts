import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AdminCategoriesService,
  type AdminCategoryDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Category browser: a full-width list with publish status and display order.
 * Creating and editing happen on their own page (`/categories/new`,
 * `/categories/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-categories',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader],
  templateUrl: './categories.html',
})
export class AdminCategories {
  private readonly service = inject(AdminCategoriesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource(() => true);
  protected readonly deletingId = signal<number | null>(null);

  protected remove(c: AdminCategoryDto): void {
    if (!confirm(this.translate.instant('categories.confirm_delete', { name: c.name ?? '#' + c.id }))) {
      return;
    }
    this.deletingId.set(c.id);
    this.service.delete(c.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('categories.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('categories.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
