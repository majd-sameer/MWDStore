import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminBrandsService, type AdminBrandDto } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Brand browser: a full-width list. Creating and editing happen on their own
 * page (`/brands/new`, `/brands/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-brands',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader],
  templateUrl: './brands.html',
})
export class AdminBrands {
  private readonly service = inject(AdminBrandsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource(() => true);
  protected readonly deletingId = signal<number | null>(null);

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
