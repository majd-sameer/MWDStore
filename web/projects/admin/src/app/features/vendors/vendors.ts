import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminOperationsService, type AdminVendorDto } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Vendor browser: a full-width list. Creating and editing happen on their own
 * page (`/vendors/new`, `/vendors/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-vendors',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader],
  templateUrl: './vendors.html',
})
export class AdminVendors {
  private readonly service = inject(AdminOperationsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.vendorsResource();
  protected readonly deletingId = signal<number | null>(null);

  protected remove(v: AdminVendorDto): void {
    if (!confirm(this.translate.instant('vendors.confirm_delete', { name: v.name ?? '#' + v.id }))) {
      return;
    }
    this.deletingId.set(v.id);
    this.service.deleteVendor(v.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('vendors.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('vendors.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
