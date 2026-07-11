import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { NgbOffcanvas } from '@ng-bootstrap/ng-bootstrap';
import {
  AdminProductAttributesService,
  type AdminProductAttributeDto,
  type AdminProductAttributeGroupDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { MultiLangInput, type MultiLangValue } from '../../shared/multi-lang-input';
import { TableSkeleton } from '../../shared/table-skeleton';
import { AdminProductAttributeForm } from './product-attribute-form';

/**
 * Product attribute browser: the attribute list with a small group manager
 * alongside. Creating and editing an attribute happen in an offcanvas panel
 * ({@link AdminProductAttributeForm}), seeded straight from the selected row;
 * groups stay here since they are a lightweight secondary entity.
 */
@Component({
  selector: 'app-admin-product-attributes',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, Icon, TranslatePipe, PageHeader, MultiLangInput, TableSkeleton],
  templateUrl: './product-attributes.html',
})
export class AdminProductAttributes {
  private readonly service = inject(AdminProductAttributesService);
  private readonly offcanvas = inject(NgbOffcanvas);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource();
  protected readonly groups = this.service.groupsResource();
  protected readonly deletingId = signal<number | null>(null);
  /** Bilingual name for the "add group" box. */
  protected readonly newGroupName = signal<MultiLangValue>({ ar: '', en: '' });

  /** Open the create panel. */
  protected openNew(): void {
    this.openForm(null);
  }

  /** Open the edit panel seeded from an existing row. */
  protected openEdit(a: AdminProductAttributeDto): void {
    this.openForm(a);
  }

  /**
   * Slide the attribute form in from the end. The panel only closes through its
   * own Save/Cancel actions (static backdrop + no Esc), and a successful save
   * reloads the list.
   */
  private openForm(attribute: AdminProductAttributeDto | null): void {
    const ref = this.offcanvas.open(AdminProductAttributeForm, {
      position: 'end',
      panelClass: 'attribute-panel',
      ariaLabelledBy: 'attribute-panel-title',
      backdrop: 'static',
      keyboard: false,
    });
    const panel = ref.componentInstance as AdminProductAttributeForm;
    panel.init(attribute);
    panel.saved.subscribe(() => {
      ref.close();
      this.list.reload();
    });
    panel.cancelled.subscribe(() => ref.dismiss());
  }

  protected remove(a: AdminProductAttributeDto): void {
    if (!confirm(this.translate.instant('attributes.confirm_delete', { name: a.name ?? '#' + a.id }))) {
      return;
    }
    this.deletingId.set(a.id);
    this.service.delete(a.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('attributes.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('attributes.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }

  protected addGroup(): void {
    const value = this.newGroupName();
    const name = value.ar.trim();
    if (!name) {
      return;
    }
    this.service.createGroup({ name, nameEn: value.en || null }).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('attributes.group_created'));
        this.newGroupName.set({ ar: '', en: '' });
        this.groups.reload();
      },
      error: () => this.toast.error(this.translate.instant('attributes.group_create_failed')),
    });
  }

  protected removeGroup(g: AdminProductAttributeGroupDto): void {
    if (!confirm(this.translate.instant('attributes.confirm_delete_group', { name: g.name ?? '' }))) {
      return;
    }
    this.service.deleteGroup(g.id).subscribe({
      next: () => {
        this.groups.reload();
      },
      error: () =>
        this.toast.error(this.translate.instant('attributes.group_delete_failed')),
    });
  }
}
