import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminCmsService, type AdminMenuDto } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';

/**
 * Menu browser: a full-width list of navigation menus. Creating and editing a
 * menu (and its items) happen on their own page (`/menus/new`, `/menus/:id`),
 * mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-menus',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader, TableSkeleton],
  templateUrl: './menus.html',
})
export class AdminMenus {
  private readonly service = inject(AdminCmsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.menusResource();
  protected readonly deletingId = signal<number | null>(null);

  protected togglePublished(menu: AdminMenuDto, isPublished: boolean): void {
    this.service.updateMenu(menu.id, { name: menu.name ?? '', isPublished }).subscribe({
      next: () => this.list.reload(),
      error: () => this.toast.error(this.translate.instant('menus.update_failed')),
    });
  }

  protected removeMenu(menu: AdminMenuDto): void {
    if (!confirm(this.translate.instant('menus.confirm_delete', { name: menu.name ?? '' }))) {
      return;
    }
    this.deletingId.set(menu.id);
    this.service.deleteMenu(menu.id).subscribe({
      next: () => {
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('menus.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
