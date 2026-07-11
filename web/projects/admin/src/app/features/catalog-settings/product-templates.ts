import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { NgbOffcanvas } from '@ng-bootstrap/ng-bootstrap';
import {
  AdminOperationsService,
  type AdminProductTemplateDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';
import { AdminProductTemplateForm } from './product-template-form';

/**
 * Product template browser (named attribute sets): a full-width list. Creating
 * and editing happen in an offcanvas panel that slides in from the end
 * ({@link AdminProductTemplateForm}), seeded straight from the selected row.
 */
@Component({
  selector: 'app-admin-product-templates',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, TranslatePipe, PageHeader, TableSkeleton],
  templateUrl: './product-templates.html',
})
export class AdminProductTemplates {
  private readonly service = inject(AdminOperationsService);
  private readonly offcanvas = inject(NgbOffcanvas);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.templatesResource();
  protected readonly deletingId = signal<number | null>(null);

  /** Open the create panel. */
  protected openNew(): void {
    this.openForm(null);
  }

  /** Open the edit panel seeded from an existing row. */
  protected openEdit(t: AdminProductTemplateDto): void {
    this.openForm(t);
  }

  /**
   * Slide the template form in from the end. The panel only closes through its
   * own Save/Cancel actions (static backdrop + no Esc), and a successful save
   * reloads the list.
   */
  private openForm(template: AdminProductTemplateDto | null): void {
    const ref = this.offcanvas.open(AdminProductTemplateForm, {
      position: 'end',
      panelClass: 'template-panel',
      ariaLabelledBy: 'template-panel-title',
      backdrop: 'static',
      keyboard: false,
    });
    const panel = ref.componentInstance as AdminProductTemplateForm;
    panel.init(template);
    panel.saved.subscribe(() => {
      ref.close();
      this.list.reload();
    });
    panel.cancelled.subscribe(() => ref.dismiss());
  }

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
