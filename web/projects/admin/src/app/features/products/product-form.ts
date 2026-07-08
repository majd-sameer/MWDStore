import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
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
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NgSelectModule } from '@ng-select/ng-select';
import {
  OwlDateTimeModule,
  OwlNativeDateTimeModule,
} from '@danielmoncada/angular-datetime-picker';
import {
  AdminBrandsService,
  AdminCategoriesService,
  AdminMediaService,
  AdminProductAttributesService,
  AdminProductOptionsService,
  AdminProductsService,
  type AdminProductDetail,
  type ProductQuickSearchItem,
  type ProductUpsertRequest,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { firstError } from '../../shared/field-error';
import { MultiLangInput, type MultiLangValue } from '../../shared/multi-lang-input';

interface ProductFormModel {
  name: MultiLangValue;
  slug: string;
  sku: string;
  gtin: string;
  shortDescription: MultiLangValue;
  description: MultiLangValue;
  specification: MultiLangValue;
  metaTitle: MultiLangValue;
  metaKeywords: MultiLangValue;
  metaDescription: MultiLangValue;
  price: number;
  oldPrice: string;
  specialPrice: string;
  specialPriceStart: Date | null;
  specialPriceEnd: Date | null;
  stockQuantity: number;
  brandId: string;
  isPublished: boolean;
  isFeatured: boolean;
  isSignature: boolean;
  signatureSortOrder: number;
  isAllowToOrder: boolean;
  isCallForPricing: boolean;
  stockTrackingIsEnabled: boolean;
}

function emptyModel(): ProductFormModel {
  return {
    name: { ar: '', en: '' },
    slug: '',
    sku: '',
    gtin: '',
    shortDescription: { ar: '', en: '' },
    description: { ar: '', en: '' },
    specification: { ar: '', en: '' },
    metaTitle: { ar: '', en: '' },
    metaKeywords: { ar: '', en: '' },
    metaDescription: { ar: '', en: '' },
    price: 0,
    oldPrice: '',
    specialPrice: '',
    specialPriceStart: null,
    specialPriceEnd: null,
    stockQuantity: 0,
    brandId: '',
    isPublished: true,
    isFeatured: false,
    isSignature: false,
    signatureSortOrder: 0,
    isAllowToOrder: true,
    isCallForPricing: false,
    stockTrackingIsEnabled: true,
  };
}

interface GalleryItem {
  mediaId: number;
  url: string;
  caption: string | null;
}

interface OptionValueRow {
  key: string;
  display: string;
}

interface OptionRow {
  optionId: number;
  name: string;
  displayType: 'text' | 'color';
  values: OptionValueRow[];
}

interface CombinationRow {
  optionId: number;
  optionName: string;
  value: string;
  sortIndex: number;
}

interface VariationRow {
  name: string;
  sku: string;
  gtin: string;
  price: number;
  oldPrice: string;
  thumbnailImageId: number | null;
  optionCombinations: CombinationRow[];
}

interface LinkedProductRow {
  id: number;
  name: string;
}

interface AttributeRow {
  attributeId: number;
  name: string;
  groupName: string;
  value: string;
}

/**
 * Create / edit a product. The `:id` route param is either `new` (create) or a
 * numeric id (edit, seeded from `GET /api/admin/products/{id}`). Beyond the
 * scalar fields this form manages categories, media uploads, attribute values,
 * options + generated variations and related/cross-sell links — the same
 * surface as the old admin's product form.
 */
@Component({
  selector: 'app-admin-product-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    Control,
    FormsModule,
    NgSelectModule,
    OwlDateTimeModule,
    OwlNativeDateTimeModule,
    FormField,
    Button,
    RouterLink,
    TranslatePipe,
    PageHeader,
    MultiLangInput,
  ],
  templateUrl: './product-form.html',
})
export class AdminProductForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminProductsService);
  private readonly brandsService = inject(AdminBrandsService);
  private readonly categoriesService = inject(AdminCategoriesService);
  private readonly mediaService = inject(AdminMediaService);
  private readonly optionsService = inject(AdminProductOptionsService);
  private readonly attributesService = inject(AdminProductAttributesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly productId = computed(() => Number(this.idParam().get('id')));
  protected readonly productIdLabel = computed(() => this.idParam().get('id') ?? '');

  protected readonly existing = this.service.getResource(this.productId);
  protected readonly brands = this.brandsService.listResource(() => false);
  protected readonly categories = this.categoriesService.listResource(() => false);
  protected readonly allOptions = this.optionsService.listResource();
  protected readonly allAttributes = this.attributesService.listResource();

  protected readonly model = signal<ProductFormModel>(emptyModel());
  protected readonly f = form(this.model, (path) => {
    required(path.name.ar, { message: 'Name is required' });
    min(path.price, 0, { message: 'Price cannot be negative' });
    min(path.stockQuantity, 0, { message: 'Stock cannot be negative' });
  });

  // Dynamic collections, managed outside the signal form.
  protected readonly categoryIds = signal<number[]>([]);
  protected readonly thumbnail = signal<GalleryItem | null>(null);
  protected readonly gallery = signal<GalleryItem[]>([]);
  protected readonly attributeRows = signal<AttributeRow[]>([]);
  protected readonly optionRows = signal<OptionRow[]>([]);
  protected readonly variationRows = signal<VariationRow[]>([]);
  protected readonly relatedRows = signal<LinkedProductRow[]>([]);
  protected readonly crossSellRows = signal<LinkedProductRow[]>([]);
  protected readonly searchResults = signal<ProductQuickSearchItem[]>([]);
  protected readonly uploading = signal(false);

  protected readonly availableAttributes = computed(() => {
    const used = new Set(this.attributeRows().map((r) => r.attributeId));
    return (this.allAttributes.value() ?? []).filter((a) => !used.has(a.id));
  });

  protected readonly availableOptions = computed(() => {
    const used = new Set(this.optionRows().map((r) => r.optionId));
    return (this.allOptions.value() ?? []).filter((o) => !used.has(o.id));
  });

  /** Pending picks in the "add attribute / add option" ng-selects (numeric ids). */
  protected readonly pendingAttributeId = signal<number | null>(null);
  protected readonly pendingOptionId = signal<number | null>(null);

  /** Brand options with string ids so ng-select's strict compare matches the string `brandId` field. */
  protected readonly brandItems = computed(() =>
    (this.brands.value() ?? []).map((b) => ({ id: String(b.id), name: b.name })),
  );

  /** Option display-type choices for the per-option ng-select (translated labels). */
  protected readonly displayTypeOptions = [
    { value: 'text', key: 'product_form.display_text' },
    { value: 'color', key: 'product_form.display_color' },
  ];

  protected readonly serverError = signal<string | null>(null);
  protected readonly err = firstError;

  private seeded = false;
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    // Seed the form once the product detail arrives (edit mode only).
    effect(() => {
      if (this.isNew() || this.seeded) {
        return;
      }
      const p = this.existing.value();
      if (!p) {
        return;
      }
      this.seeded = true;
      this.seedFrom(p);
    });
  }

  private seedFrom(p: AdminProductDetail): void {
    this.model.set({
      name: { ar: p.name ?? '', en: p.nameEn ?? '' },
      slug: p.slug ?? '',
      sku: p.sku ?? '',
      gtin: p.gtin ?? '',
      shortDescription: { ar: p.shortDescription ?? '', en: p.shortDescriptionEn ?? '' },
      description: { ar: p.description ?? '', en: p.descriptionEn ?? '' },
      specification: { ar: p.specification ?? '', en: p.specificationEn ?? '' },
      metaTitle: { ar: p.metaTitle ?? '', en: p.metaTitleEn ?? '' },
      metaKeywords: { ar: p.metaKeywords ?? '', en: p.metaKeywordsEn ?? '' },
      metaDescription: { ar: p.metaDescription ?? '', en: p.metaDescriptionEn ?? '' },
      price: p.price,
      oldPrice: p.oldPrice === null ? '' : String(p.oldPrice),
      specialPrice: p.specialPrice === null ? '' : String(p.specialPrice),
      specialPriceStart: p.specialPriceStart ? new Date(p.specialPriceStart) : null,
      specialPriceEnd: p.specialPriceEnd ? new Date(p.specialPriceEnd) : null,
      stockQuantity: p.stockQuantity,
      brandId: p.brandId === null ? '' : String(p.brandId),
      isPublished: p.isPublished,
      isFeatured: p.isFeatured,
      isSignature: p.isSignature,
      signatureSortOrder: p.signatureSortOrder,
      isAllowToOrder: p.isAllowToOrder,
      isCallForPricing: p.isCallForPricing,
      stockTrackingIsEnabled: p.stockTrackingIsEnabled,
    });

    this.categoryIds.set(p.categoryIds ?? []);
    this.thumbnail.set(
      p.thumbnailImageId !== null && p.thumbnailUrl !== null
        ? { mediaId: p.thumbnailImageId, url: p.thumbnailUrl, caption: null }
        : null,
    );
    this.gallery.set(
      p.media.map((m) => ({ mediaId: m.mediaId, url: m.url, caption: m.caption })),
    );
    this.attributeRows.set(
      p.attributes.map((a) => ({
        attributeId: a.attributeId,
        name: a.name ?? '',
        groupName: a.groupName ?? '',
        value: a.value ?? '',
      })),
    );
    this.optionRows.set(
      p.options.map((o) => ({
        optionId: o.optionId,
        name: o.name ?? '',
        displayType: o.displayType === 'color' ? 'color' : 'text',
        values: o.values.map((v) => ({ key: v.key, display: v.display ?? '' })),
      })),
    );
    this.variationRows.set(
      p.variations.map((v) => ({
        name: v.name ?? '',
        sku: v.sku ?? '',
        gtin: v.gtin ?? '',
        price: v.price,
        oldPrice: v.oldPrice === null ? '' : String(v.oldPrice),
        thumbnailImageId: v.thumbnailImageId,
        optionCombinations: v.optionCombinations.map((c) => ({
          optionId: c.optionId,
          optionName: c.optionName ?? '',
          value: c.value ?? '',
          sortIndex: c.sortIndex,
        })),
      })),
    );
    this.relatedRows.set(
      p.relatedProducts.map((r) => ({ id: r.id, name: r.name ?? '' })),
    );
    this.crossSellRows.set(
      p.crossSellProducts.map((r) => ({ id: r.id, name: r.name ?? '' })),
    );
  }

  // ----- Categories ---------------------------------------------------------

  protected toggleCategory(id: number): void {
    this.categoryIds.update((ids) =>
      ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id],
    );
  }

  // ----- Media --------------------------------------------------------------

  protected onThumbnailSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    this.uploading.set(true);
    this.mediaService.upload(file).subscribe({
      next: (m) => {
        this.thumbnail.set({ mediaId: m.id, url: m.url, caption: m.caption });
        this.uploading.set(false);
        input.value = '';
      },
      error: () => {
        this.toast.error(this.translate.instant('product_form.upload_failed'));
        this.uploading.set(false);
      },
    });
  }

  protected clearThumbnail(): void {
    this.thumbnail.set(null);
  }

  protected onGallerySelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    if (!files.length) {
      return;
    }
    this.uploading.set(true);
    let remaining = files.length;
    for (const file of files) {
      this.mediaService.upload(file).subscribe({
        next: (m) => {
          this.gallery.update((g) => [...g, { mediaId: m.id, url: m.url, caption: m.caption }]);
          if (--remaining === 0) {
            this.uploading.set(false);
            input.value = '';
          }
        },
        error: () => {
          this.toast.error(this.translate.instant('product_form.upload_failed'));
          if (--remaining === 0) {
            this.uploading.set(false);
          }
        },
      });
    }
  }

  protected removeGalleryItem(mediaId: number): void {
    this.gallery.update((g) => g.filter((m) => m.mediaId !== mediaId));
  }

  // ----- Attributes ----------------------------------------------------------

  protected addAttribute(id: number | null): void {
    if (id === null) {
      return;
    }
    const attribute = (this.allAttributes.value() ?? []).find((a) => a.id === id);
    if (!attribute || this.attributeRows().some((r) => r.attributeId === id)) {
      return;
    }
    this.attributeRows.update((rows) => [
      ...rows,
      {
        attributeId: attribute.id,
        name: attribute.name ?? '',
        groupName: attribute.groupName ?? '',
        value: '',
      },
    ]);
  }

  protected setAttributeValue(attributeId: number, value: string): void {
    this.attributeRows.update((rows) =>
      rows.map((r) => (r.attributeId === attributeId ? { ...r, value } : r)),
    );
  }

  protected removeAttribute(attributeId: number): void {
    this.attributeRows.update((rows) => rows.filter((r) => r.attributeId !== attributeId));
  }

  // ----- Options & variations -------------------------------------------------

  protected addOption(id: number | null): void {
    if (id === null) {
      return;
    }
    const option = (this.allOptions.value() ?? []).find((o) => o.id === id);
    if (!option || this.optionRows().some((r) => r.optionId === id)) {
      return;
    }
    this.optionRows.update((rows) => [
      ...rows,
      { optionId: option.id, name: option.name ?? '', displayType: 'text', values: [] },
    ]);
  }

  protected removeOption(optionId: number): void {
    this.optionRows.update((rows) => rows.filter((r) => r.optionId !== optionId));
  }

  protected setOptionDisplayType(optionId: number, displayType: string): void {
    this.optionRows.update((rows) =>
      rows.map((r) =>
        r.optionId === optionId
          ? { ...r, displayType: displayType === 'color' ? 'color' : 'text' }
          : r,
      ),
    );
  }

  protected addOptionValue(optionId: number, input: HTMLInputElement): void {
    const key = input.value.trim();
    if (!key) {
      return;
    }
    this.optionRows.update((rows) =>
      rows.map((r) =>
        r.optionId === optionId && !r.values.some((v) => v.key === key)
          ? { ...r, values: [...r.values, { key, display: '' }] }
          : r,
      ),
    );
    input.value = '';
  }

  protected removeOptionValue(optionId: number, key: string): void {
    this.optionRows.update((rows) =>
      rows.map((r) =>
        r.optionId === optionId
          ? { ...r, values: r.values.filter((v) => v.key !== key) }
          : r,
      ),
    );
  }

  protected setOptionValueDisplay(optionId: number, key: string, display: string): void {
    this.optionRows.update((rows) =>
      rows.map((r) =>
        r.optionId === optionId
          ? { ...r, values: r.values.map((v) => (v.key === key ? { ...v, display } : v)) }
          : r,
      ),
    );
  }

  /** Cartesian product of all option values -> one variation row per combination. */
  protected generateVariations(): void {
    const options = this.optionRows().filter((r) => r.values.length > 0);
    if (!options.length) {
      this.toast.error(this.translate.instant('product_form.add_option_value_first'));
      return;
    }

    let combos: CombinationRow[][] = [[]];
    options.forEach((option, index) => {
      combos = combos.flatMap((combo) =>
        option.values.map((v) => [
          ...combo,
          { optionId: option.optionId, optionName: option.name, value: v.key, sortIndex: index },
        ]),
      );
    });

    const baseName = this.model().name.ar.trim();
    const basePrice = Number(this.model().price) || 0;
    const existing = this.variationRows();

    const rows = combos.map((optionCombinations) => {
      const name = `${baseName} ${optionCombinations.map((c) => c.value).join(', ')}`.trim();
      return (
        existing.find((v) => v.name === name) ?? {
          name,
          sku: '',
          gtin: '',
          price: basePrice,
          oldPrice: '',
          thumbnailImageId: null,
          optionCombinations,
        }
      );
    });

    this.variationRows.set(rows);
  }

  protected patchVariation(name: string, patch: Partial<VariationRow>): void {
    this.variationRows.update((rows) =>
      rows.map((v) => (v.name === name ? { ...v, ...patch } : v)),
    );
  }

  protected removeVariation(name: string): void {
    this.variationRows.update((rows) => rows.filter((v) => v.name !== name));
  }

  // ----- Related / cross-sell --------------------------------------------------

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
      this.service.quickSearch(trimmed).subscribe({
        next: (items) =>
          this.searchResults.set(items.filter((i) => i.id !== this.productId())),
        error: () => this.searchResults.set([]),
      });
    }, 250);
  }

  protected addLink(kind: 'related' | 'crossSell', item: ProductQuickSearchItem): void {
    const target = kind === 'related' ? this.relatedRows : this.crossSellRows;
    if (target().some((p) => p.id === item.id)) {
      return;
    }
    target.update((rows) => [...rows, { id: item.id, name: item.name ?? '' }]);
  }

  protected removeLink(kind: 'related' | 'crossSell', id: number): void {
    const target = kind === 'related' ? this.relatedRows : this.crossSellRows;
    target.update((rows) => rows.filter((p) => p.id !== id));
  }

  // ----- Submit -----------------------------------------------------------------

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      const body: ProductUpsertRequest = {
        name: m.name.ar,
        nameEn: m.name.en || null,
        slug: m.slug || null,
        sku: m.sku || null,
        gtin: m.gtin || null,
        shortDescription: m.shortDescription.ar || null,
        shortDescriptionEn: m.shortDescription.en || null,
        description: m.description.ar || null,
        descriptionEn: m.description.en || null,
        specification: m.specification.ar || null,
        specificationEn: m.specification.en || null,
        metaTitle: m.metaTitle.ar || null,
        metaTitleEn: m.metaTitle.en || null,
        metaKeywords: m.metaKeywords.ar || null,
        metaKeywordsEn: m.metaKeywords.en || null,
        metaDescription: m.metaDescription.ar || null,
        metaDescriptionEn: m.metaDescription.en || null,
        price: Number(m.price),
        oldPrice: m.oldPrice.trim() === '' ? null : Number(m.oldPrice),
        specialPrice: m.specialPrice.trim() === '' ? null : Number(m.specialPrice),
        specialPriceStart: m.specialPriceStart ? m.specialPriceStart.toISOString() : null,
        specialPriceEnd: m.specialPriceEnd ? m.specialPriceEnd.toISOString() : null,
        stockQuantity: Number(m.stockQuantity),
        // ng-select clears to null (not ''), so treat any falsy brand as "none".
        brandId: m.brandId ? Number(m.brandId) : null,
        isPublished: m.isPublished,
        isFeatured: m.isFeatured,
        isSignature: m.isSignature,
        signatureSortOrder: Number(m.signatureSortOrder) || 0,
        isAllowToOrder: m.isAllowToOrder,
        isCallForPricing: m.isCallForPricing,
        stockTrackingIsEnabled: m.stockTrackingIsEnabled,
        categoryIds: this.categoryIds(),
        thumbnailImageId: this.thumbnail()?.mediaId ?? null,
        mediaIds: this.gallery().map((g) => g.mediaId),
        attributes: this.attributeRows().map((r) => ({
          attributeId: r.attributeId,
          value: r.value || null,
        })),
        options: this.optionRows().map((r) => ({
          optionId: r.optionId,
          displayType: r.displayType,
          values: r.values.map((v) => ({ key: v.key, display: v.display || null })),
        })),
        variations: this.variationRows().map((v) => ({
          name: v.name,
          sku: v.sku || null,
          gtin: v.gtin || null,
          price: Number(v.price),
          oldPrice: v.oldPrice.trim() === '' ? null : Number(v.oldPrice),
          thumbnailImageId: v.thumbnailImageId,
          optionCombinations: v.optionCombinations.map((c) => ({
            optionId: c.optionId,
            value: c.value,
            sortIndex: c.sortIndex,
          })),
        })),
        relatedProductIds: this.relatedRows().map((p) => p.id),
        crossSellProductIds: this.crossSellRows().map((p) => p.id),
      };
      try {
        if (this.isNew()) {
          await firstValueFrom(this.service.create(body));
          this.toast.success(this.translate.instant('product_form.created_ok'));
        } else {
          await firstValueFrom(this.service.update(this.productId(), body));
          this.toast.success(this.translate.instant('product_form.updated_ok'));
        }
        await this.router.navigate(['/products']);
      } catch {
        this.serverError.set(this.translate.instant('product_form.save_failed'));
      }
      return undefined;
    });
  }
}
