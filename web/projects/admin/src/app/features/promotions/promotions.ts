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
import { ConfirmService, Icon, TableCards, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Promotions browser: the cart-rule list plus a recent-usage log. Creating and
 * editing a promotion happen on their own page (`/promotions/new`,
 * `/promotions/:id`), mirroring the product list/form split.
 */
@Component({
  selector: 'app-admin-promotions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, Icon, TranslatePipe, PageHeader, TableCards],
  template: `
    <app-page-header
      [title]="'promotions.title' | translate"
      [subtitle]="'promotions.subtitle' | translate"
    >
      <a routerLink="/promotions/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'promotions.new' | translate }}
      </a>
    </app-page-header>

    <div class="card border-0 shadow-sm mb-4">
      <div class="card-body">
        @if (list.isLoading()) {
          <div class="text-center py-5">
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
            </div>
          </div>
        } @else if (list.error()) {
          <div class="alert alert-danger mb-0">{{ 'common.error_api' | translate }}</div>
        } @else if (list.value(); as rows) {
          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0" libTableCards>
              <thead>
                <tr>
                  <th scope="col">{{ 'common.name' | translate }}</th>
                  <th scope="col">{{ 'promotions.col_discount' | translate }}</th>
                  <th scope="col">{{ 'common.status' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'promotions.col_used' | translate }}</th>
                  <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                </tr>
              </thead>
              <tbody>
                @for (r of rows; track r.id) {
                  <tr>
                    <td>
                      <a [routerLink]="['/promotions', r.id]" class="text-decoration-none fw-medium">{{ r.name }}</a>
                      @if (r.isCouponRequired) {
                        <span class="badge text-bg-info ms-1">{{ 'promotions.coupon_badge' | translate }}</span>
                      }
                    </td>
                    <td>{{ r.discountAmount }}{{ r.ruleToApply === 'by_percent' ? '%' : '' }}</td>
                    <td>
                      @if (r.isActive) {
                        <span class="badge text-bg-success">{{ 'common.active' | translate }}</span>
                      } @else {
                        <span class="badge text-bg-secondary">{{ 'common.inactive' | translate }}</span>
                      }
                    </td>
                    <td class="text-end">{{ r.usageCount }}</td>
                    <td class="text-end">
                      <span class="d-inline-flex gap-1">
                        <a [routerLink]="['/promotions', r.id]" class="action-btn" [title]="'common.edit' | translate">
                          <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                        </a>
                        <button
                          type="button"
                          class="action-btn action-btn-danger"
                          [title]="'common.delete' | translate"
                          [disabled]="deletingId() === r.id"
                          (click)="remove(r)"
                        >
                          <lib-icon name="trash" [size]="15" [label]="'common.delete' | translate" />
                        </button>
                      </span>
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="5">
                      <div class="empty-state">
                        <span class="empty-icon"><lib-icon name="box" [size]="26" /></span>
                        <div class="empty-title">{{ 'promotions.empty' | translate }}</div>
                        <a routerLink="/promotions/new" class="btn btn-primary btn-sm mt-2">
                          {{ 'promotions.create_first' | translate }}
                        </a>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </div>
    </div>

    <div class="card border-0 shadow-sm">
      <div class="card-header bg-body fw-semibold">{{ 'promotions.usages_title' | translate }}</div>
      <div class="card-body">
        <div class="table-responsive">
        <table class="table table-sm align-middle mb-0" libTableCards>
          <thead>
            <tr>
              <th>{{ 'promotions.col_promotion' | translate }}</th>
              <th>{{ 'promotions.coupon_badge' | translate }}</th>
              <th>{{ 'customers.col_customer' | translate }}</th>
              <th>{{ 'dashboard.col_order' | translate }}</th>
              <th>{{ 'common.when' | translate }}</th>
            </tr>
          </thead>
          <tbody>
            @for (u of usages(); track u.id) {
              <tr>
                <td>{{ u.cartRuleName }}</td>
                <td>{{ u.couponCode ?? '—' }}</td>
                <td>{{ u.userEmail ?? u.userId }}</td>
                <td>#{{ u.orderId }}</td>
                <td>{{ u.createdOn | date: 'medium' }}</td>
              </tr>
            } @empty {
              <tr>
                <td colspan="5" class="text-center text-body-secondary py-3">
                  {{ 'promotions.no_usages' | translate }}
                </td>
              </tr>
            }
          </tbody>
        </table>
        </div>
      </div>
    </div>
  `,
})
export class AdminPromotions {
  private readonly service = inject(AdminPromotionsService);
  private readonly confirmService = inject(ConfirmService);
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

  protected async remove(r: AdminCartRuleListItem): Promise<void> {
    const ok = await this.confirmService.confirm({
      title: this.translate.instant('common.confirm_title'),
      message: this.translate.instant('promotions.confirm_delete', { name: r.name ?? '#' + r.id }),
      okText: this.translate.instant('common.delete'),
      cancelText: this.translate.instant('common.cancel'),
      destructive: true,
    });
    if (!ok) {
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
