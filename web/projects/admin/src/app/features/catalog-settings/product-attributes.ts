import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AdminProductAttributesService,
  type AdminProductAttributeDto,
  type AdminProductAttributeGroupDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Product attribute browser: the attribute list with a small group manager
 * alongside. Creating and editing an attribute happen on their own page
 * (`/product-attributes/new`, `/product-attributes/:id`); groups stay here
 * since they are a lightweight secondary entity.
 */
@Component({
  selector: 'app-admin-product-attributes',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Button, Icon, TranslatePipe, PageHeader],
  templateUrl: './product-attributes.html',
})
export class AdminProductAttributes {
  private readonly service = inject(AdminProductAttributesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource();
  protected readonly groups = this.service.groupsResource();
  protected readonly deletingId = signal<number | null>(null);

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

  protected addGroup(input: HTMLInputElement): void {
    const name = input.value.trim();
    if (!name) {
      return;
    }
    this.service.createGroup({ name }).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('attributes.group_created'));
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
