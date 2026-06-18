import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  form,
  FormField as Control,
  min,
  required,
  submit,
} from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  AdminCategoriesService,
  AdminPromotionsService,
  AdminProductsService,
  type CartRuleUpsertRequest,
  type ProductQuickSearchItem,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';

interface RuleModel {
  name: string;
  description: string;
  isActive: boolean;
  startOn: string;
  endOn: string;
  isCouponRequired: boolean;
  ruleToApply: string;
  discountAmount: number;
  maxDiscountAmount: string;
  usageLimitPerCoupon: string;
  usageLimitPerCustomer: string;
  couponCode: string;
}

function emptyModel(): RuleModel {
  return {
    name: '',
    description: '',
    isActive: true,
    startOn: '',
    endOn: '',
    isCouponRequired: true,
    ruleToApply: 'cart_fixed',
    discountAmount: 0,
    maxDiscountAmount: '',
    usageLimitPerCoupon: '',
    usageLimitPerCustomer: '',
    couponCode: '',
  };
}

interface LinkedProductRow {
  id: number;
  name: string;
}

/**
 * Create / edit a promotion (cart rule + coupon) on its own page (mirrors the
 * product form). Edit mode fetches the full detail (`GET /api/admin/cart-rules/{id}`)
 * to seed schedule, restrictions and linked products. Usage history lives on the list.
 */
@Component({
  selector: 'app-admin-promotion-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/promotions" class="text-decoration-none">← {{ 'promotions.title' | translate }}</a>
    </nav>
    <app-page-header [title]="(isNew() ? 'promotions.new_title' : 'promotions.edit_title') | translate" />

    @if (!isNew() && loading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (!isNew() && loadError()) {
      <div class="alert alert-danger">{{ 'promotions.load_one_failed' | translate }}</div>
    } @else {
      <div class="row g-4">
        <div class="col-lg-8">
          <div class="card border-0 shadow-sm">
            <div class="card-body">
              @if (serverError(); as message) {
                <div class="alert alert-danger" role="alert">{{ message }}</div>
              }
              <form (submit)="onSubmit($event)" novalidate>
                <lib-form-field [label]="'common.name' | translate" controlId="pr-name" [required]="true" [error]="err(f.name())">
                  <input id="pr-name" type="text" class="form-control"
                    [class.is-invalid]="!!err(f.name())" [formField]="f.name" />
                </lib-form-field>
                <lib-form-field [label]="'common.description' | translate" controlId="pr-desc">
                  <textarea id="pr-desc" rows="2" class="form-control" [formField]="f.description"></textarea>
                </lib-form-field>

                <div class="row">
                  <div class="col-md-6">
                    <lib-form-field [label]="'promotions.discount_type' | translate" controlId="pr-rule">
                      <select id="pr-rule" class="form-select" [formField]="f.ruleToApply">
                        <option value="cart_fixed">{{ 'promotions.type_fixed' | translate }}</option>
                        <option value="by_percent">{{ 'promotions.type_percent' | translate }}</option>
                      </select>
                    </lib-form-field>
                  </div>
                  <div class="col-md-6">
                    <lib-form-field [label]="'promotions.amount' | translate" controlId="pr-amount" [error]="err(f.discountAmount())">
                      <input id="pr-amount" type="number" step="0.01" class="form-control"
                        [class.is-invalid]="!!err(f.discountAmount())" [formField]="f.discountAmount" />
                    </lib-form-field>
                  </div>
                </div>

                <div class="row">
                  <div class="col-md-6">
                    <lib-form-field [label]="'promotions.max_discount' | translate" controlId="pr-max"
                      [hint]="'promotions.max_discount_hint' | translate">
                      <input id="pr-max" type="number" step="0.01" class="form-control"
                        [formField]="f.maxDiscountAmount" />
                    </lib-form-field>
                  </div>
                  <div class="col-md-6">
                    <lib-form-field [label]="'promotions.coupon_code' | translate" controlId="pr-code">
                      <input id="pr-code" type="text" class="form-control" [formField]="f.couponCode" />
                    </lib-form-field>
                  </div>
                </div>

                <div class="row">
                  <div class="col-md-6">
                    <lib-form-field [label]="'promotions.starts' | translate" controlId="pr-start">
                      <input id="pr-start" type="date" class="form-control" [formField]="f.startOn" />
                    </lib-form-field>
                  </div>
                  <div class="col-md-6">
                    <lib-form-field [label]="'promotions.ends' | translate" controlId="pr-end">
                      <input id="pr-end" type="date" class="form-control" [formField]="f.endOn" />
                    </lib-form-field>
                  </div>
                </div>

                <div class="row">
                  <div class="col-md-6">
                    <lib-form-field [label]="'promotions.limit_per_coupon' | translate" controlId="pr-limit-coupon">
                      <input id="pr-limit-coupon" type="number" class="form-control"
                        [formField]="f.usageLimitPerCoupon" />
                    </lib-form-field>
                  </div>
                  <div class="col-md-6">
                    <lib-form-field [label]="'promotions.limit_per_customer' | translate" controlId="pr-limit-cust">
                      <input id="pr-limit-cust" type="number" class="form-control"
                        [formField]="f.usageLimitPerCustomer" />
                    </lib-form-field>
                  </div>
                </div>

                <div class="form-check form-switch">
                  <input id="pr-active" type="checkbox" class="form-check-input" [formField]="f.isActive" />
                  <label for="pr-active" class="form-check-label">{{ 'common.active' | translate }}</label>
                </div>
                <div class="form-check form-switch mb-3">
                  <input id="pr-coupon-req" type="checkbox" class="form-check-input"
                    [formField]="f.isCouponRequired" />
                  <label for="pr-coupon-req" class="form-check-label">{{ 'promotions.coupon_required' | translate }}</label>
                </div>

                <div class="form-label">{{ 'promotions.restrict_categories' | translate }}</div>
                <div class="border rounded p-2 mb-3" style="max-height: 9rem; overflow-y: auto">
                  @for (c of categories.value() ?? []; track c.id) {
                    <div class="form-check">
                      <input type="checkbox" class="form-check-input" id="pr-cat-{{ c.id }}"
                        [checked]="categoryIds().includes(c.id)"
                        (change)="toggleCategory(c.id)" />
                      <label class="form-check-label" for="pr-cat-{{ c.id }}">{{ c.name }}</label>
                    </div>
                  }
                </div>

                <lib-form-field [label]="'promotions.restrict_products' | translate" controlId="pr-prod-search"
                  [hint]="'promotions.restrict_products_hint' | translate">
                  <input id="pr-prod-search" type="text" class="form-control"
                    (input)="searchProducts($any($event.target).value)" />
                </lib-form-field>
                @if (searchResults().length) {
                  <div class="list-group mb-2">
                    @for (r of searchResults(); track r.id) {
                      <button type="button" class="list-group-item list-group-item-action"
                        (click)="addProduct(r)">
                        {{ r.name }}
                      </button>
                    }
                  </div>
                }
                @for (p of productRows(); track p.id) {
                  <div class="d-flex align-items-center justify-content-between border rounded px-2 py-1 mb-1">
                    <span class="small">{{ p.name }}</span>
                    <button type="button" class="btn-close" style="font-size: 0.6rem"
                      (click)="removeProduct(p.id)" [attr.aria-label]="'common.remove' | translate"></button>
                  </div>
                }

                <div class="form-actions">
                  <button libButton variant="primary" [disabled]="f().submitting()">
                    {{ (f().submitting() ? 'common.saving' : isNew() ? 'promotions.create' : 'common.save_changes') | translate }}
                  </button>
                  <a routerLink="/promotions" class="btn btn-outline-secondary">{{ 'common.cancel' | translate }}</a>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class AdminPromotionForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminPromotionsService);
  private readonly categoriesService = inject(AdminCategoriesService);
  private readonly productsService = inject(AdminProductsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly ruleId = computed(() => Number(this.idParam().get('id')));

  protected readonly categories = this.categoriesService.listResource(() => false);

  protected readonly loading = signal(false);
  protected readonly loadError = signal(false);
  protected readonly serverError = signal<string | null>(null);
  protected readonly categoryIds = signal<number[]>([]);
  protected readonly productRows = signal<LinkedProductRow[]>([]);
  protected readonly searchResults = signal<ProductQuickSearchItem[]>([]);

  protected readonly model = signal<RuleModel>(emptyModel());
  protected readonly f = form(this.model, (path) => {
    required(path.name, { message: 'Name is required' });
    min(path.discountAmount, 0, { message: 'Amount cannot be negative' });
  });
  protected readonly err = firstError;

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    if (!this.isNew()) {
      this.loading.set(true);
      this.service.get(this.ruleId()).subscribe({
        next: (detail) => {
          this.model.set({
            name: detail.name ?? '',
            description: detail.description ?? '',
            isActive: detail.isActive,
            startOn: detail.startOn?.slice(0, 10) ?? '',
            endOn: detail.endOn?.slice(0, 10) ?? '',
            isCouponRequired: detail.isCouponRequired,
            ruleToApply: detail.ruleToApply ?? 'cart_fixed',
            discountAmount: detail.discountAmount,
            maxDiscountAmount:
              detail.maxDiscountAmount === null ? '' : String(detail.maxDiscountAmount),
            usageLimitPerCoupon:
              detail.usageLimitPerCoupon === null ? '' : String(detail.usageLimitPerCoupon),
            usageLimitPerCustomer:
              detail.usageLimitPerCustomer === null ? '' : String(detail.usageLimitPerCustomer),
            couponCode: detail.couponCode ?? '',
          });
          this.categoryIds.set(detail.categoryIds);
          this.productRows.set(detail.products.map((p) => ({ id: p.id, name: p.name ?? '' })));
          this.loading.set(false);
        },
        error: () => {
          this.loadError.set(true);
          this.loading.set(false);
        },
      });
    }
  }

  protected toggleCategory(id: number): void {
    this.categoryIds.update((ids) =>
      ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id],
    );
  }

  protected searchProducts(query: string): void {
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }
    const trimmed = query.trim();
    if (trimmed.length < 2) {
      this.searchResults.set([]);
      return;
    }
    this.searchTimer = setTimeout(() => {
      this.productsService.quickSearch(trimmed).subscribe({
        next: (items) => this.searchResults.set(items),
        error: () => this.searchResults.set([]),
      });
    }, 250);
  }

  protected addProduct(item: ProductQuickSearchItem): void {
    if (!this.productRows().some((p) => p.id === item.id)) {
      this.productRows.update((rows) => [...rows, { id: item.id, name: item.name ?? '' }]);
    }
    this.searchResults.set([]);
  }

  protected removeProduct(id: number): void {
    this.productRows.update((rows) => rows.filter((p) => p.id !== id));
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      const body: CartRuleUpsertRequest = {
        name: m.name,
        description: m.description || null,
        isActive: m.isActive,
        startOn: m.startOn ? new Date(m.startOn).toISOString() : null,
        endOn: m.endOn ? new Date(m.endOn).toISOString() : null,
        isCouponRequired: m.isCouponRequired,
        ruleToApply: m.ruleToApply,
        discountAmount: Number(m.discountAmount),
        maxDiscountAmount: m.maxDiscountAmount.trim() === '' ? null : Number(m.maxDiscountAmount),
        usageLimitPerCoupon:
          m.usageLimitPerCoupon.trim() === '' ? null : Number(m.usageLimitPerCoupon),
        usageLimitPerCustomer:
          m.usageLimitPerCustomer.trim() === '' ? null : Number(m.usageLimitPerCustomer),
        couponCode: m.couponCode || null,
        categoryIds: this.categoryIds(),
        productIds: this.productRows().map((p) => p.id),
      };
      try {
        if (this.isNew()) {
          await firstValueFrom(this.service.create(body));
          this.toast.success(this.translate.instant('promotions.created_ok'));
        } else {
          await firstValueFrom(this.service.update(this.ruleId(), body));
          this.toast.success(this.translate.instant('promotions.updated_ok'));
        }
        await this.router.navigate(['/promotions']);
      } catch {
        this.serverError.set(this.translate.instant('promotions.save_failed'));
      }
      return undefined;
    });
  }
}
