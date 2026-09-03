import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  HostListener,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { AuthService, LanguageService, MoneyPipe } from 'core';
import {
  CatalogService,
  StorefrontFeaturesService,
  type ProductDetailCategory,
  type ProductDetailModel,
  type ProductDetailVariation,
  type ProductListItem,
  type ReviewDto,
} from 'data-access';
import {
  Breadcrumb,
  type BreadcrumbItem,
  Button,
  Icon,
  type IconName,
  Stars,
  Stepper,
  Tile,
  ToastService,
} from 'ui';
import { CartStore, type CartProduct } from '../../core/cart.store';
import { SeoService } from '../../core/seo.service';
import { ProductCard } from '../../shared/product-card';
import { announceCartError } from '../../core/cart-messages';
import { CartDrawerService } from '../../core/cart-drawer.service';

/**
 * Product page: breadcrumb, gradient gallery, origin/title/rating, price with
 * saving, optional variations, quantity stepper + add-to-bag, the
 * description / specification / details bands and a related-products grid.
 * Keeps the existing data + variation/stock logic; copy is keyed; layout logical.
 */
@Component({
  selector: 'app-product-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MoneyPipe,
    DatePipe,
    TranslatePipe,
    Breadcrumb,
    Button,
    Icon,
    Stars,
    Stepper,
    Tile,
    ProductCard,
  ],
  templateUrl: './product-detail.html',
  styleUrl: './product-detail.scss',
})
export class ProductDetail {
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly cart = inject(CartStore);
  private readonly seo = inject(SeoService);
  private readonly toast = inject(ToastService);
  private readonly cartDrawer = inject(CartDrawerService);
  private readonly translate = inject(TranslateService);
  private readonly features = inject(StorefrontFeaturesService);
  private readonly auth = inject(AuthService);
  private readonly language = inject(LanguageService);

  /** Active locale for review dates; prices stay Western (en-US). */
  protected readonly locale = computed(() => (this.language.lang() === 'ar' ? 'ar' : 'en-US'));

  protected readonly isAuthenticated = this.auth.isAuthenticated;
  protected readonly reviews = signal<ReviewDto[]>([]);
  protected readonly reviewRating = signal(5);
  protected readonly reviewTitle = signal('');
  protected readonly reviewComment = signal('');
  protected readonly submittingReview = signal(false);
  protected readonly reviewSubmitted = signal(false);

  private readonly id = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  private readonly productId = computed(() => Number(this.id().get('id')));

  protected readonly product = this.catalog.productResource(this.productId);

  protected readonly selectedVariationId = signal<number | null>(null);
  protected readonly quantity = signal(1);
  protected readonly adding = signal(false);
  /** Optimistic heart-fill state (the wishlist API has no "remove"). */
  protected readonly wished = signal(false);

  /** Fixed trust badges shown under the buy row. */
  protected readonly perks: ReadonlyArray<{ icon: IconName; title: string; text: string }> = [
    { icon: 'hands', title: 'product.perks.handmade_title', text: 'product.perks.handmade_text' },
    { icon: 'shield', title: 'product.perks.verified_title', text: 'product.perks.verified_text' },
    { icon: 'truck', title: 'product.perks.delivery_title', text: 'product.perks.delivery_text' },
    { icon: 'lock', title: 'product.perks.secure_title', text: 'product.perks.secure_text' },
  ];

  /** First category — drives the eyebrow, breadcrumb and "view all" link. */
  protected readonly category = computed<ProductDetailCategory | null>(
    () => this.product.value()?.categories?.[0] ?? null,
  );

  /** Producing center / origin place — the brand name, else a generic label. */
  protected readonly originPlace = computed(
    () =>
      this.product.value()?.brand?.name ??
      this.translate.instant('product.origin_default'),
  );

  private readonly selectedVariation = computed<ProductDetailVariation | null>(() => {
    const id = this.selectedVariationId();
    const variations = this.product.value()?.variations ?? [];
    return variations.find((variation) => variation.id === id) ?? null;
  });

  /** Index of the image shown in the main stage / lightbox (slider position). */
  protected readonly activeIndex = signal(0);
  /** Hover-zoom state on the main stage. */
  protected readonly zooming = signal(false);
  /** transform-origin (in %) following the cursor while zoomed. */
  protected readonly zoomOrigin = signal('50% 50%');
  /** Fullscreen lightbox open state. */
  protected readonly lightboxOpen = signal(false);

  /** thumbnail + gallery of the product (variation images take over when one is selected). */
  protected readonly galleryImages = computed<string[]>(() => {
    const p = this.product.value();
    if (!p) {
      return [];
    }
    const variation = this.selectedVariation();
    const urls = [
      ...(variation?.thumbnailImageUrl ? [variation.thumbnailImageUrl] : []),
      ...(variation?.imageUrls ?? []),
      ...(p.thumbnailImageUrl ? [p.thumbnailImageUrl] : []),
      ...(p.imageUrls ?? []),
    ];
    return [...new Set(urls)];
  });

  /** Image currently shown in the main stage, derived from the slider index. */
  protected readonly activeImage = computed<string | null>(
    () => this.galleryImages()[this.activeIndex()] ?? null,
  );

  protected readonly activePrice = computed(
    () =>
      this.selectedVariation()?.calculatedProductPrice ??
      this.product.value()?.calculatedProductPrice ?? {
        price: 0,
        oldPrice: null,
        percentOfSaving: 0,
      },
  );

  protected readonly canOrder = computed(() => {
    const p = this.product.value();
    if (!p) {
      return false;
    }
    const source = this.selectedVariation() ?? p;
    const tracksStock =
      'stockTrackingIsEnabled' in source ? source.stockTrackingIsEnabled : false;
    const inStock = !tracksStock || (source.stockQuantity ?? 0) > 0;
    return p.isAllowToOrder && inStock;
  });

  /**
   * Upper bound for the quantity stepper: what is in stock, **less whatever the bag already holds**
   * of this exact product (or variation). Without that subtraction the stepper happily offers all 5
   * of a 5-in-stock product to a shopper whose bag already has 4, and the add then silently caps —
   * this way the ceiling the shopper sees is the one that will actually apply.
   */
  protected readonly maxQty = computed(() => {
    const p = this.product.value();
    const source = this.selectedVariation() ?? p;
    if (!source) {
      return 99;
    }
    const tracksStock =
      'stockTrackingIsEnabled' in source ? source.stockTrackingIsEnabled : false;
    if (!tracksStock || source.stockQuantity <= 0) {
      return 99;
    }
    const inBag =
      this.cart.items().find((item) => item.productId === source.id)?.quantity ?? 0;
    return Math.max(1, source.stockQuantity - inBag);
  });

  /** True once the bag holds every unit in stock — the add button has nothing left to add. */
  protected readonly allStockInBag = computed(() => {
    const p = this.product.value();
    const source = this.selectedVariation() ?? p;
    if (!source) {
      return false;
    }
    const tracksStock =
      'stockTrackingIsEnabled' in source ? source.stockTrackingIsEnabled : false;
    if (!tracksStock) {
      return false;
    }
    const inBag =
      this.cart.items().find((item) => item.productId === source.id)?.quantity ?? 0;
    return inBag >= Math.max(0, source.stockQuantity);
  });

  constructor() {
    effect(() => {
      const p = this.product.value();
      if (!p) {
        return;
      }
      this.seo.update({
        title: p.metaTitle || p.name || 'Product',
        description: p.metaDescription || this.stripHtml(p.shortDescription) || undefined,
        type: 'product',
      });
    });

    // Reviews + recently-viewed tracking follow the product id. The token is
    // memory-only, so isAuthenticated() is always false during SSR — the
    // tracking POST only ever fires in the browser.
    effect(() => {
      const id = this.productId();
      if (!Number.isFinite(id)) {
        return;
      }
      // Fresh product → reset the buy state and scroll to top (browser only).
      this.quantity.set(1);
      this.selectedVariationId.set(null);
      this.wished.set(false);
      if (typeof window !== 'undefined') {
        window.scrollTo(0, 0);
      }
      this.reviewSubmitted.set(false);
      this.features.reviews(id).subscribe({
        next: (reviews) => this.reviews.set(reviews),
        error: () => this.reviews.set([]),
      });
      if (this.isAuthenticated()) {
        this.features.recordView(id).subscribe({ error: () => undefined });
      }
    });

    // The gallery changes when the product loads or a variation is picked —
    // snap the slider back to the first image and drop any active zoom.
    effect(() => {
      this.galleryImages();
      this.activeIndex.set(0);
      this.zooming.set(false);
    });

    // Lock background scroll while the lightbox overlay is open (browser only).
    effect(() => {
      if (typeof document === 'undefined') {
        return;
      }
      document.body.style.overflow = this.lightboxOpen() ? 'hidden' : '';
    });
  }

  /** Move the slider to a specific thumbnail. */
  protected selectImage(index: number): void {
    this.activeIndex.set(index);
  }

  /** Slider: previous image (wraps around). */
  protected prev(): void {
    const count = this.galleryImages().length;
    if (count > 1) {
      this.activeIndex.update((i) => (i - 1 + count) % count);
    }
  }

  /** Slider: next image (wraps around). */
  protected next(): void {
    const count = this.galleryImages().length;
    if (count > 1) {
      this.activeIndex.update((i) => (i + 1) % count);
    }
  }

  /** Track the cursor over the stage so the zoom origin follows the pointer. */
  protected onZoomMove(event: MouseEvent): void {
    const el = event.currentTarget as HTMLElement;
    const rect = el.getBoundingClientRect();
    const x = ((event.clientX - rect.left) / rect.width) * 100;
    const y = ((event.clientY - rect.top) / rect.height) * 100;
    this.zoomOrigin.set(`${x}% ${y}%`);
  }

  protected openLightbox(): void {
    if (this.activeImage()) {
      this.lightboxOpen.set(true);
    }
  }

  /** Close the lightbox only when the backdrop itself (not its content) is clicked. */
  protected onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) {
      this.closeLightbox();
    }
  }

  protected closeLightbox(): void {
    this.lightboxOpen.set(false);
  }

  /** Keyboard control for the lightbox (Esc to close, arrows to slide). */
  @HostListener('document:keydown', ['$event'])
  protected onKeydown(event: KeyboardEvent): void {
    if (!this.lightboxOpen()) {
      return;
    }
    if (event.key === 'Escape') {
      this.closeLightbox();
    } else if (event.key === 'ArrowRight') {
      this.next();
    } else if (event.key === 'ArrowLeft') {
      this.prev();
    }
  }

  protected addToWishlist(productId: number): void {
    this.features.addToWishlist({ productId }).subscribe({
      next: () => this.toast.success(this.translate.instant('wishlist.added')),
      error: () => this.toast.error(this.translate.instant('wishlist.error')),
    });
  }

  protected addToCompare(productId: number): void {
    this.features.addToComparison(productId).subscribe({
      next: () => this.toast.success(this.translate.instant('compare.added')),
      error: () => this.toast.error(this.translate.instant('compare.error')),
    });
  }

  protected submitReview(productId: number): void {
    const comment = this.reviewComment().trim();
    if (!comment) {
      this.toast.error(this.translate.instant('common.error'));
      return;
    }
    this.submittingReview.set(true);
    this.features
      .submitReview(productId, {
        title: this.reviewTitle().trim() || null,
        comment,
        rating: this.reviewRating(),
      })
      .subscribe({
        next: () => {
          this.submittingReview.set(false);
          this.reviewSubmitted.set(true);
        },
        error: () => {
          this.submittingReview.set(false);
          this.toast.error(this.translate.instant('reviews.error'));
        },
      });
  }

  protected crumbs(p: ProductDetailModel): BreadcrumbItem[] {
    const cat = this.category();
    return [
      { label: this.translate.instant('common.home'), link: '/' },
      { label: this.translate.instant('shop.title'), link: '/shop' },
      ...(cat?.name ? [{ label: cat.name, link: '/shop' }] : []),
      { label: p.name ?? '' },
    ];
  }

  /** Heart toggle: fills optimistically and persists the add when authenticated. */
  protected toggleWish(productId: number): void {
    this.wished.update((on) => !on);
    if (this.wished()) {
      this.addToWishlist(productId);
    }
  }

  protected addToCart(product: ProductDetailModel): void {
    const variation = this.selectedVariation();
    const cartProduct: CartProduct = variation
      ? {
          id: variation.id,
          name: variation.name ?? product.name,
          thumbnailImageUrl: variation.thumbnailImageUrl ?? product.thumbnailImageUrl,
          calculatedProductPrice: variation.calculatedProductPrice,
          stockQuantity: variation.stockQuantity,
          isAllowToOrder: variation.isAllowToOrder,
          stockTrackingIsEnabled: variation.stockTrackingIsEnabled,
        }
      : product;
    this.adding.set(true);
    this.cart.add(cartProduct, this.quantity()).subscribe({
      next: (cart) => {
        this.adding.set(false);
        this.cartDrawer.showAdded(cart);
      },
      error: (error) => {
        this.adding.set(false);
        announceCartError(this.toast, this.translate, error);
      },
    });
  }

  protected quickAdd(product: ProductListItem): void {
    this.cart.add(product).subscribe({
      next: (cart) => this.cartDrawer.showAdded(cart),
      error: (error) => announceCartError(this.toast, this.translate, error),
    });
  }

  /** First letter of the reviewer's name for the avatar bubble (falls back to a dot). */
  protected initial(name: string | null): string {
    return name?.trim()?.charAt(0) || '•';
  }

  private stripHtml(value: string | null): string {
    return value ? value.replace(/<[^>]*>/g, '').trim() : '';
  }
}
