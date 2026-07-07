import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import {
  AdminPromotionsService,
  type AdminCartRuleListItem,
  type AdminCartRuleUsageDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Promotions browser: the cart-rule list plus a recent-usage log. Creating and
 * editing a promotion happen on their own page (`/promotions/new`,
 * `/promotions/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-promotions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, Icon, TranslatePipe, PageHeader],
  templateUrl: './promotions.html',
})
export class AdminPromotions {
  private readonly service = inject(AdminPromotionsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource();
  protected readonly usages = signal<AdminCartRuleUsageDto[]>([]);
  protected readonly deletingId = signal<number | null>(null);

  constructor() {
    this.service.usages().subscribe({
      next: (items) => this.usages.set(items),
      error: () => this.usages.set([]),
    });
  }

  protected remove(r: AdminCartRuleListItem): void {
    if (!confirm(this.translate.instant('promotions.confirm_delete', { name: r.name ?? '#' + r.id }))) {
      return;
    }
    this.deletingId.set(r.id);
    this.service.delete(r.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('promotions.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('promotions.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
