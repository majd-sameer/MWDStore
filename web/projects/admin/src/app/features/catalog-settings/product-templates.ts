import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AdminOperationsService,
  type AdminProductTemplateDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';

/**
 * Product template browser (named attribute sets): a full-width list. Creating
 * and editing happen on their own page (`/product-templates/new`,
 * `/product-templates/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-product-templates',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader, TableSkeleton],
  templateUrl: './product-templates.html',
})
export class AdminProductTemplates {
  private readonly service = inject(AdminOperationsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.templatesResource();
  protected readonly deletingId = signal<number | null>(null);

  protected remove(t: AdminProductTemplateDto): void {
    if (!confirm(this.translate.instant('templates.confirm_delete', { name: t.name ?? '#' + t.id }))) {
      return;
    }
    this.deletingId.set(t.id);
    this.service.deleteTemplate(t.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('templates.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('templates.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
