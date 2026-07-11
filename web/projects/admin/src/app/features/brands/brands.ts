import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { NgbOffcanvas } from '@ng-bootstrap/ng-bootstrap';
import { AdminBrandsService, type AdminBrandDto } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';
import { AdminBrandForm } from './brand-form';

/**
 * Brand browser: a full-width list. Creating and editing happen in an offcanvas
 * panel that slides in from the end ({@link AdminBrandForm}), seeded straight
 * from the selected row.
 */
@Component({
  selector: 'app-admin-brands',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, TranslatePipe, PageHeader, TableSkeleton],
  templateUrl: './brands.html',
})
export class AdminBrands {
  private readonly service = inject(AdminBrandsService);
  private readonly offcanvas = inject(NgbOffcanvas);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource(() => true);
  protected readonly deletingId = signal<number | null>(null);

  /** Open the create panel. */
  protected openNew(): void {
    this.openForm(null);
  }

  /** Open the edit panel seeded from an existing row. */
  protected openEdit(b: AdminBrandDto): void {
    this.openForm(b);
  }

  /**
   * Slide the brand form in from the end. The panel only closes through its own
   * Save/Cancel actions (static backdrop + no Esc), and a successful save
   * reloads the list.
   */
  private openForm(brand: AdminBrandDto | null): void {
    const ref = this.offcanvas.open(AdminBrandForm, {
      position: 'end',
      panelClass: 'brand-panel',
      ariaLabelledBy: 'brand-panel-title',
      backdrop: 'static',
      keyboard: false,
    });
    const panel = ref.componentInstance as AdminBrandForm;
    panel.init(brand);
    panel.saved.subscribe(() => {
      ref.close();
      this.list.reload();
    });
    panel.cancelled.subscribe(() => ref.dismiss());
  }

  protected remove(b: AdminBrandDto): void {
    if (!confirm(this.translate.instant('brands.confirm_delete', { name: b.name ?? '#' + b.id }))) {
      return;
    }
    this.deletingId.set(b.id);
    this.service.delete(b.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('brands.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('brands.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
