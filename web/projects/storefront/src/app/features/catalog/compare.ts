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
  templateUrl: './compare.html',
  styleUrl: './compare.scss',
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
