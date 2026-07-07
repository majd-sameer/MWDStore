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
  templateUrl: './promotion-form.html',
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
