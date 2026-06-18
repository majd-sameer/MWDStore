import { MoneyPipe } from 'core';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import {
  StorefrontFeaturesService,
  type ComparisonProductDto,
} from 'data-access';
import { Button, Tile, ToastService } from 'ui';

/** Side-by-side product comparison (max 4), with the union of all spec attributes as rows. */
@Component({
  selector: 'app-compare',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MoneyPipe, TranslatePipe, RouterLink, Button, Tile],
  template: `
    <div class="container py-4">
      <h1 class="page-title">{{ 'compare.title' | translate }}</h1>

      @if (loading()) {
        <div class="text-center py-5">
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
          </div>
        </div>
      } @else if (!products().length) {
        <div class="text-center py-5">
          <p class="text-body-secondary">{{ 'compare.empty' | translate }}</p>
          <a libButton variant="dark" routerLink="/shop">{{ 'cart.browse' | translate }}</a>
        </div>
      } @else {
        <div class="table-responsive">
          <table class="table align-middle">
            <thead>
              <tr>
                <th scope="col" class="cmp-attr">{{ 'compare.attribute' | translate }}</th>
                @for (p of products(); track p.productId) {
                  <th scope="col" class="cmp-col">
                    <a [routerLink]="['/products', p.productId]">
                      <lib-tile [src]="p.thumbnailUrl" [seed]="p.name ?? p.productId"
                        [alt]="p.name" ratio="1x1" />
                    </a>
                    <a class="cmp-name" [routerLink]="['/products', p.productId]">{{ p.name }}</a>
                    <div class="tabular-nums fw-bold">{{ p.price | money }}</div>
                    <button type="button" class="btn btn-sm btn-outline-danger mt-1"
                      (click)="remove(p.productId)">
                      {{ 'compare.remove' | translate }}
                    </button>
                  </th>
                }
              </tr>
            </thead>
            <tbody>
              @for (attr of attributeNames(); track attr) {
                <tr>
                  <th scope="row" class="fw-medium">{{ attr }}</th>
                  @for (p of products(); track p.productId) {
                    <td>{{ valueFor(p, attr) ?? '—' }}</td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
  styles: `
    .page-title {
      font-size: 1.6rem;
      font-weight: 700;
      letter-spacing: -0.02em;
      margin-block-end: 1.25rem;
    }
    .cmp-attr {
      inline-size: 12rem;
    }
    .cmp-col {
      min-inline-size: 11rem;
      max-inline-size: 14rem;
    }
    .cmp-name {
      display: block;
      margin-block-start: 0.4rem;
      font-weight: 600;
      text-decoration: none;
      color: var(--ink, inherit);
    }
  `,
})
export class Compare {
  private readonly service = inject(StorefrontFeaturesService);
  private readonly toast = inject(ToastService);

  protected readonly products = signal<ComparisonProductDto[]>([]);
  protected readonly loading = signal(true);

  protected readonly attributeNames = computed(() => {
    const names = new Set<string>();
    for (const product of this.products()) {
      for (const attribute of product.attributes) {
        if (attribute.name) {
          names.add(attribute.name);
        }
      }
    }
    return [...names];
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.service.comparison().subscribe({
      next: (products) => {
        this.products.set(products);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected valueFor(product: ComparisonProductDto, attributeName: string): string | null {
    return product.attributes.find((a) => a.name === attributeName)?.value ?? null;
  }

  protected remove(productId: number): void {
    this.service.removeFromComparison(productId).subscribe({
      next: () =>
        this.products.update((products) => products.filter((p) => p.productId !== productId)),
      error: () => this.toast.error('compare.error'),
    });
  }
}
