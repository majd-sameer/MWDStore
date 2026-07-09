import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminOperationsService, type AdminVendorDto } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';

/** Active-state segments for the status filter chips. */
type StatusFilter = 'all' | 'active' | 'inactive';

/**
 * Vendor browser: a full-width list. Creating and editing happen on their own
 * page (`/vendors/new`, `/vendors/:id`), mirroring the product list/form split.
 *
 * The endpoint returns the full list, so the name search and status filter
 * below run client-side over the loaded rows.
 */
@Component({
  selector: 'app-admin-vendors',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader, TableSkeleton],
  templateUrl: './vendors.html',
})
export class AdminVendors {
  private readonly service = inject(AdminOperationsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.vendorsResource();
  protected readonly deletingId = signal<number | null>(null);

  // ----- Filters (client-side) -------------------------------------------------
  protected readonly search = signal('');
  protected readonly status = signal<StatusFilter>('all');

  protected readonly filtered = computed<AdminVendorDto[]>(() => {
    const rows = this.list.value() ?? [];
    const term = this.search().trim().toLowerCase();
    const status = this.status();
    return rows.filter((v) => {
      if (status === 'active' && !v.isActive) {
        return false;
      }
      if (status === 'inactive' && v.isActive) {
        return false;
      }
      if (!term) {
        return true;
      }
      return (
        (v.name ?? '').toLowerCase().includes(term) ||
        (v.slug ?? '').toLowerCase().includes(term) ||
        (v.email ?? '').toLowerCase().includes(term)
      );
    });
  });

  protected readonly hasFilters = computed(
    () => Boolean(this.search()) || this.status() !== 'all',
  );

  protected setSearch(value: string): void {
    this.search.set(value);
  }

  protected setStatus(status: StatusFilter): void {
    this.status.set(status);
  }

  protected clearFilters(): void {
    this.search.set('');
    this.status.set('all');
  }

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
