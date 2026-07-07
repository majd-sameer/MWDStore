import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AdminWarehousesService,
  type AdminWarehouseDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Warehouse browser: a full-width list. Creating and editing happen on their own
 * page (`/warehouses/new`, `/warehouses/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-warehouses',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader],
  templateUrl: './warehouses.html',
})
export class AdminWarehouses {
  private readonly service = inject(AdminWarehousesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource();
  protected readonly deletingId = signal<number | null>(null);

  protected remove(w: AdminWarehouseDto): void {
    if (!confirm(this.translate.instant('warehouses.confirm_delete', { name: w.name ?? '#' + w.id }))) {
      return;
    }
    this.deletingId.set(w.id);
    this.service.delete(w.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('warehouses.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('warehouses.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
