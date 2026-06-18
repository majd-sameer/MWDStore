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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
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

interface ProductFormModel {
  name: string;
  slug: string;
  sku: string;
  gtin: string;
  shortDescription: string;
  description: string;
  specification: string;
  metaTitle: string;
  metaKeywords: string;
  metaDescription: string;
  price: number;
  oldPrice: string;
  specialPrice: string;
  specialPriceStart: string;
  specialPriceEnd: string;
  stockQuantity: number;
  brandId: string;
  isPublished: boolean;
  isFeatured: boolean;
  isAllowToOrder: boolean;
  isCallForPricing: boolean;
  stockTrackingIsEnabled: boolean;
}

function emptyModel(): ProductFormModel {
  return {
    name: '',
    slug: '',
    sku: '',
    gtin: '',
    shortDescription: '',
    description: '',
    specification: '',
    metaTitle: '',
    metaKeywords: '',
    metaDescription: '',
    price: 0,
    oldPrice: '',
    specialPrice: '',
    specialPriceStart: '',
    specialPriceEnd: '',
    stockQuantity: 0,
    brandId: '',
    isPublished: true,
    isFeatured: false,
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
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/products" class="text-decoration-none">← {{ 'products.title' | translate }}</a>
    </nav>
    <app-page-header [title]="(isNew() ? 'product_form.new_title' : 'product_form.edit_title') | translate" />

    @if (!isNew() && existing.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (!isNew() && existing.error()) {
      <div class="alert alert-danger">{{ 'product_form.load_one_failed' | translate }}</div>
    } @else {
      @if (serverError(); as message) {
        <div class="alert alert-danger" role="alert">{{ message }}</div>
      }

      <form (submit)="onSubmit($event)" novalidate>
        <div class="row g-4">
          <div class="col-lg-8">
            <div class="card border-0 shadow-sm mb-4">
              <div class="card-body">
                <lib-form-field [label]="'common.name' | translate" controlId="name" [required]="true" [error]="err(f.name())">
                  <input id="name" type="text" class="form-control"
                    [class.is-invalid]="!!err(f.name())" [formField]="f.name" />
                </lib-form-field>

                <div class="row">
                  <div class="col-md-4">
                    <lib-form-field [label]="'common.slug' | translate" controlId="slug" [hint]="'common.slug_hint' | translate">
                      <input id="slug" type="text" class="form-control" [formField]="f.slug" />
                    </lib-form-field>
                  </div>
                  <div class="col-md-4">
                    <lib-form-field [label]="'product_form.sku' | translate" controlId="sku">
                      <input id="sku" type="text" class="form-control" [formField]="f.sku" />
                    </lib-form-field>
                  </div>
                  <div class="col-md-4">
                    <lib-form-field [label]="'product_form.gtin' | translate" controlId="gtin">
                      <input id="gtin" type="text" class="form-control" [formField]="f.gtin" />
                    </lib-form-field>
                  </div>
                </div>

                <lib-form-field [label]="'product_form.short_description' | translate" controlId="shortDescription">
                  <textarea id="shortDescription" rows="2" class="form-control"
                    [formField]="f.shortDescription"></textarea>
                </lib-form-field>

                <lib-form-field [label]="'common.description' | translate" controlId="description">
                  <textarea id="description" rows="5" class="form-control"
                    [formField]="f.description"></textarea>
                </lib-form-field>

                <lib-form-field [label]="'product_form.specification' | translate" controlId="specification">
                  <textarea id="specification" rows="3" class="form-control"
                    [formField]="f.specification"></textarea>
                </lib-form-field>
              </div>
            </div>

            <div class="card border-0 shadow-sm mb-4">
              <div class="card-header bg-body fw-semibold">{{ 'product_form.seo' | translate }}</div>
              <div class="card-body">
                <lib-form-field [label]="'pages.meta_title' | translate" controlId="metaTitle">
                  <input id="metaTitle" type="text" class="form-control" [formField]="f.metaTitle" />
                </lib-form-field>
                <lib-form-field [label]="'pages.meta_keywords' | translate" controlId="metaKeywords">
                  <input id="metaKeywords" type="text" class="form-control" [formField]="f.metaKeywords" />
                </lib-form-field>
                <lib-form-field [label]="'pages.meta_description' | translate" controlId="metaDescription">
                  <textarea id="metaDescription" rows="2" class="form-control"
                    [formField]="f.metaDescription"></textarea>
                </lib-form-field>
              </div>
            </div>

            <div class="card border-0 shadow-sm mb-4">
              <div class="card-header bg-body fw-semibold">{{ 'nav.attributes' | translate }}</div>
              <div class="card-body">
                <div class="d-flex gap-2 mb-3">
                  <select class="form-select w-auto flex-grow-1" #attrSelect>
                    <option value="">{{ 'product_form.choose_attribute' | translate }}</option>
                    @for (a of availableAttributes(); track a.id) {
                      <option value="{{ a.id }}">{{ a.groupName }} / {{ a.name }}</option>
                    }
                  </select>
                  <button type="button" libButton variant="secondary" [outline]="true"
                    (click)="addAttribute(attrSelect.value); attrSelect.value = ''">
                    {{ 'common.add' | translate }}
                  </button>
                </div>

                @for (row of attributeRows(); track row.attributeId) {
                  <div class="d-flex align-items-center gap-2 mb-2">
                    <span class="badge text-bg-light border" style="min-width: 10rem">{{ row.name }}</span>
                    <input type="text" class="form-control" [placeholder]="'common.value' | translate"
                      [value]="row.value"
                      (input)="setAttributeValue(row.attributeId, $any($event.target).value)" />
                    <button type="button" class="btn btn-sm btn-outline-danger"
                      (click)="removeAttribute(row.attributeId)">✕</button>
                  </div>
                } @empty {
                  <p class="text-body-secondary small mb-0">{{ 'product_form.no_attributes_added' | translate }}</p>
                }
              </div>
            </div>

            <div class="card border-0 shadow-sm mb-4">
              <div class="card-header bg-body fw-semibold">{{ 'product_form.options_title' | translate }}</div>
              <div class="card-body">
                <div class="d-flex gap-2 mb-3">
                  <select class="form-select w-auto flex-grow-1" #optSelect>
                    <option value="">{{ 'product_form.choose_option' | translate }}</option>
                    @for (o of availableOptions(); track o.id) {
                      <option value="{{ o.id }}">{{ o.name }}</option>
                    }
                  </select>
                  <button type="button" libButton variant="secondary" [outline]="true"
                    (click)="addOption(optSelect.value); optSelect.value = ''">
                    {{ 'product_form.add_option' | translate }}
                  </button>
                </div>

                @for (row of optionRows(); track row.optionId) {
                  <div class="border rounded p-3 mb-3">
                    <div class="d-flex align-items-center gap-3 mb-2">
                      <span class="fw-semibold">{{ row.name }}</span>
                      <select class="form-select form-select-sm w-auto"
                        [value]="row.displayType"
                        (change)="setOptionDisplayType(row.optionId, $any($event.target).value)">
                        <option value="text">{{ 'product_form.display_text' | translate }}</option>
                        <option value="color">{{ 'product_form.display_color' | translate }}</option>
                      </select>
                      <button type="button" class="btn btn-sm btn-outline-danger ms-auto"
                        (click)="removeOption(row.optionId)">{{ 'common.remove' | translate }}</button>
                    </div>
                    <div class="d-flex flex-wrap align-items-center gap-2">
                      @for (v of row.values; track v.key) {
                        <span class="badge text-bg-light border d-inline-flex align-items-center gap-1">
                          @if (row.displayType === 'color') {
                            <input type="color" class="form-control form-control-color p-0 border-0"
                              style="width: 1.2rem; height: 1.2rem"
                              [value]="v.display || '#000000'"
                              (change)="setOptionValueDisplay(row.optionId, v.key, $any($event.target).value)" />
                          }
                          {{ v.key }}
                          <button type="button" class="btn-close" style="font-size: 0.6rem"
                            (click)="removeOptionValue(row.optionId, v.key)"
                            [attr.aria-label]="'common.remove' | translate"></button>
                        </span>
                      }
                      <input type="text" class="form-control form-control-sm w-auto"
                        [placeholder]="'product_form.add_value_ph' | translate"
                        (keydown.enter)="addOptionValue(row.optionId, $any($event.target)); $event.preventDefault()" />
                    </div>
                  </div>
                }

                @if (optionRows().length) {
                  <button type="button" libButton variant="secondary" [outline]="true"
                    (click)="generateVariations()">
                    {{ 'product_form.generate_combinations' | translate }}
                  </button>
                }

                @if (variationRows().length) {
                  <div class="table-responsive mt-3">
                    <table class="table table-sm align-middle">
                      <thead>
                        <tr>
                          <th>{{ 'product_form.col_variation' | translate }}</th>
                          <th style="width: 9rem">{{ 'product_form.sku' | translate }}</th>
                          <th style="width: 8rem">{{ 'product_form.price' | translate }}</th>
                          <th style="width: 8rem">{{ 'product_form.old_price' | translate }}</th>
                          <th style="width: 3rem"></th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (v of variationRows(); track v.name) {
                          <tr>
                            <td>
                              <span class="fw-medium">{{ v.name }}</span>
                              <div class="small text-body-secondary">
                                @for (c of v.optionCombinations; track c.optionId) {
                                  <span class="me-2">{{ c.optionName }}: {{ c.value }}</span>
                                }
                              </div>
                            </td>
                            <td>
                              <input type="text" class="form-control form-control-sm" [value]="v.sku"
                                (input)="patchVariation(v.name, { sku: $any($event.target).value })" />
                            </td>
                            <td>
                              <input type="number" step="0.01" class="form-control form-control-sm" [value]="v.price"
                                (input)="patchVariation(v.name, { price: $any($event.target).valueAsNumber || 0 })" />
                            </td>
                            <td>
                              <input type="number" step="0.01" class="form-control form-control-sm" [value]="v.oldPrice"
                                (input)="patchVariation(v.name, { oldPrice: $any($event.target).value })" />
                            </td>
                            <td class="text-end">
                              <button type="button" class="btn btn-sm btn-outline-danger"
                                (click)="removeVariation(v.name)">✕</button>
                            </td>
                          </tr>
                        }
                      </tbody>
                    </table>
                  </div>
                }
              </div>
            </div>

            <div class="card border-0 shadow-sm mb-4">
              <div class="card-header bg-body fw-semibold">{{ 'product_form.related_title' | translate }}</div>
              <div class="card-body">
                <lib-form-field [label]="'product_form.search_products' | translate" controlId="linkSearch"
                  [hint]="'product_form.search_products_hint' | translate">
                  <input id="linkSearch" type="text" class="form-control"
                    (input)="searchProducts($any($event.target).value)" />
                </lib-form-field>

                @if (searchResults().length) {
                  <ul class="list-group mb-3">
                    @for (r of searchResults(); track r.id) {
                      <li class="list-group-item d-flex align-items-center justify-content-between">
                        <span>{{ r.name }}</span>
                        <span class="d-flex gap-1">
                          <button type="button" class="btn btn-sm btn-outline-primary"
                            (click)="addLink('related', r)">+ {{ 'product_form.related' | translate }}</button>
                          <button type="button" class="btn btn-sm btn-outline-primary"
                            (click)="addLink('crossSell', r)">+ {{ 'product_form.cross_sell' | translate }}</button>
                        </span>
                      </li>
                    }
                  </ul>
                }

                <div class="row">
                  <div class="col-md-6">
                    <h6 class="small text-uppercase text-body-secondary">{{ 'product_form.related' | translate }}</h6>
                    @for (p of relatedRows(); track p.id) {
                      <div class="d-flex align-items-center justify-content-between border rounded px-2 py-1 mb-1">
                        <span class="small">{{ p.name }}</span>
                        <button type="button" class="btn-close" style="font-size: 0.6rem"
                          (click)="removeLink('related', p.id)"
                          [attr.aria-label]="'common.remove' | translate"></button>
                      </div>
                    } @empty {
                      <p class="text-body-secondary small">{{ 'product_form.none' | translate }}</p>
                    }
                  </div>
                  <div class="col-md-6">
                    <h6 class="small text-uppercase text-body-secondary">{{ 'product_form.cross_sell' | translate }}</h6>
                    @for (p of crossSellRows(); track p.id) {
                      <div class="d-flex align-items-center justify-content-between border rounded px-2 py-1 mb-1">
                        <span class="small">{{ p.name }}</span>
                        <button type="button" class="btn-close" style="font-size: 0.6rem"
                          (click)="removeLink('crossSell', p.id)"
                          [attr.aria-label]="'common.remove' | translate"></button>
                      </div>
                    } @empty {
                      <p class="text-body-secondary small">{{ 'product_form.none' | translate }}</p>
                    }
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="col-lg-4">
            <div class="card border-0 shadow-sm mb-4">
              <div class="card-header bg-body fw-semibold">{{ 'product_form.images_title' | translate }}</div>
              <div class="card-body">
                <label class="form-label" for="thumbnailFile">{{ 'product_form.thumbnail' | translate }}</label>
                <div class="d-flex align-items-center gap-3 mb-3">
                  @if (thumbnail(); as t) {
                    <img [src]="t.url" [alt]="'product_form.thumbnail' | translate" class="rounded border"
                      style="width: 64px; height: 64px; object-fit: cover" />
                    <button type="button" class="btn btn-sm btn-outline-danger" (click)="clearThumbnail()">
                      {{ 'common.remove' | translate }}
                    </button>
                  } @else {
                    <span class="text-body-secondary small">{{ 'product_form.no_thumbnail' | translate }}</span>
                  }
                </div>
                <input id="thumbnailFile" type="file" class="form-control form-control-sm mb-4" accept="image/*"
                  [disabled]="uploading()"
                  (change)="onThumbnailSelected($event)" />

                <label class="form-label" for="galleryFiles">{{ 'product_form.gallery' | translate }}</label>
                <div class="d-flex flex-wrap gap-2 mb-2">
                  @for (g of gallery(); track g.mediaId) {
                    <div class="position-relative">
                      <img [src]="g.url" [alt]="g.caption ?? ('product_form.product_image' | translate)" class="rounded border"
                        style="width: 64px; height: 64px; object-fit: cover" />
                      <button type="button"
                        class="btn-close position-absolute top-0 end-0 bg-white rounded-circle"
                        style="font-size: 0.55rem"
                        (click)="removeGalleryItem(g.mediaId)"
                        [attr.aria-label]="'common.remove' | translate"></button>
                    </div>
                  } @empty {
                    <span class="text-body-secondary small">{{ 'product_form.no_images' | translate }}</span>
                  }
                </div>
                <input id="galleryFiles" type="file" class="form-control form-control-sm" accept="image/*" multiple
                  [disabled]="uploading()"
                  (change)="onGallerySelected($event)" />
                @if (uploading()) {
                  <div class="small text-body-secondary mt-2">{{ 'product_form.uploading' | translate }}</div>
                }
              </div>
            </div>

            <div class="card border-0 shadow-sm mb-4">
              <div class="card-header bg-body fw-semibold">{{ 'product_form.pricing_title' | translate }}</div>
              <div class="card-body">
                <lib-form-field [label]="'product_form.price' | translate" controlId="price" [required]="true" [error]="err(f.price())">
                  <input id="price" type="number" step="0.01" class="form-control"
                    [class.is-invalid]="!!err(f.price())" [formField]="f.price" />
                </lib-form-field>
                <lib-form-field [label]="'product_form.old_price' | translate" controlId="oldPrice">
                  <input id="oldPrice" type="number" step="0.01" class="form-control" [formField]="f.oldPrice" />
                </lib-form-field>
                <lib-form-field [label]="'product_form.special_price' | translate" controlId="specialPrice">
                  <input id="specialPrice" type="number" step="0.01" class="form-control"
                    [formField]="f.specialPrice" />
                </lib-form-field>
                <div class="row">
                  <div class="col-6">
                    <lib-form-field [label]="'product_form.special_start' | translate" controlId="specialPriceStart">
                      <input id="specialPriceStart" type="date" class="form-control"
                        [formField]="f.specialPriceStart" />
                    </lib-form-field>
                  </div>
                  <div class="col-6">
                    <lib-form-field [label]="'product_form.special_end' | translate" controlId="specialPriceEnd">
                      <input id="specialPriceEnd" type="date" class="form-control"
                        [formField]="f.specialPriceEnd" />
                    </lib-form-field>
                  </div>
                </div>
                <lib-form-field [label]="'product_form.stock_quantity' | translate" controlId="stockQuantity" [error]="err(f.stockQuantity())">
                  <input id="stockQuantity" type="number" class="form-control"
                    [class.is-invalid]="!!err(f.stockQuantity())" [formField]="f.stockQuantity" />
                </lib-form-field>
              </div>
            </div>

            <div class="card border-0 shadow-sm mb-4">
              <div class="card-header bg-body fw-semibold">{{ 'product_form.organisation_title' | translate }}</div>
              <div class="card-body">
                <lib-form-field [label]="'products.brand' | translate" controlId="brandId">
                  <select id="brandId" class="form-select" [formField]="f.brandId">
                    <option value="">{{ 'product_form.none_option' | translate }}</option>
                    @for (b of brands.value() ?? []; track b.id) {
                      <option value="{{ b.id }}">{{ b.name }}</option>
                    }
                  </select>
                </lib-form-field>

                <div class="form-label">{{ 'nav.categories' | translate }}</div>
                <div class="border rounded p-2 mb-3" style="max-height: 12rem; overflow-y: auto">
                  @for (c of categories.value() ?? []; track c.id) {
                    <div class="form-check">
                      <input type="checkbox" class="form-check-input" id="cat-{{ c.id }}"
                        [checked]="categoryIds().includes(c.id)"
                        (change)="toggleCategory(c.id)" />
                      <label class="form-check-label" for="cat-{{ c.id }}">{{ c.name }}</label>
                    </div>
                  } @empty {
                    <span class="text-body-secondary small">{{ 'product_form.no_categories_defined' | translate }}</span>
                  }
                </div>

                <div class="form-check form-switch">
                  <input id="isPublished" type="checkbox" class="form-check-input" [formField]="f.isPublished" />
                  <label for="isPublished" class="form-check-label">{{ 'common.published' | translate }}</label>
                </div>
                <div class="form-check form-switch">
                  <input id="isFeatured" type="checkbox" class="form-check-input" [formField]="f.isFeatured" />
                  <label for="isFeatured" class="form-check-label">{{ 'product_form.featured' | translate }}</label>
                </div>
                <div class="form-check form-switch">
                  <input id="isAllowToOrder" type="checkbox" class="form-check-input" [formField]="f.isAllowToOrder" />
                  <label for="isAllowToOrder" class="form-check-label">{{ 'product_form.allow_ordering' | translate }}</label>
                </div>
                <div class="form-check form-switch">
                  <input id="isCallForPricing" type="checkbox" class="form-check-input"
                    [formField]="f.isCallForPricing" />
                  <label for="isCallForPricing" class="form-check-label">{{ 'product_form.call_for_pricing' | translate }}</label>
                </div>
                <div class="form-check form-switch">
                  <input id="stockTrackingIsEnabled" type="checkbox" class="form-check-input"
                    [formField]="f.stockTrackingIsEnabled" />
                  <label for="stockTrackingIsEnabled" class="form-check-label">{{ 'product_form.track_stock' | translate }}</label>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="form-actions">
          <button libButton variant="primary" [disabled]="f().submitting() || uploading()">
            {{ (f().submitting() ? 'common.saving' : isNew() ? 'product_form.create' : 'common.save_changes') | translate }}
          </button>
          <a routerLink="/products" libButton variant="secondary" [outline]="true">{{ 'common.cancel' | translate }}</a>
          @if (uploading()) {
            <span class="ms-auto small text-body-secondary">{{ 'product_form.uploading_images' | translate }}</span>
          } @else {
            <span class="ms-auto small text-body-secondary d-none d-sm-inline">
              @if (isNew()) {
                {{ 'product_form.new_title' | translate }}
              } @else {
                {{ 'product_form.editing_label' | translate: { id: productIdLabel() } }}
              }
            </span>
          }
        </div>
      </form>
    }
  `,
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
    required(path.name, { message: 'Name is required' });
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
      name: p.name ?? '',
      slug: p.slug ?? '',
      sku: p.sku ?? '',
      gtin: p.gtin ?? '',
      shortDescription: p.shortDescription ?? '',
      description: p.description ?? '',
      specification: p.specification ?? '',
      metaTitle: p.metaTitle ?? '',
      metaKeywords: p.metaKeywords ?? '',
      metaDescription: p.metaDescription ?? '',
      price: p.price,
      oldPrice: p.oldPrice === null ? '' : String(p.oldPrice),
      specialPrice: p.specialPrice === null ? '' : String(p.specialPrice),
      specialPriceStart: p.specialPriceStart?.slice(0, 10) ?? '',
      specialPriceEnd: p.specialPriceEnd?.slice(0, 10) ?? '',
      stockQuantity: p.stockQuantity,
      brandId: p.brandId === null ? '' : String(p.brandId),
      isPublished: p.isPublished,
      isFeatured: p.isFeatured,
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

  protected addAttribute(idValue: string): void {
    const id = Number(idValue);
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

  protected addOption(idValue: string): void {
    const id = Number(idValue);
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

    const baseName = this.model().name.trim();
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
        name: m.name,
        slug: m.slug || null,
        sku: m.sku || null,
        gtin: m.gtin || null,
        shortDescription: m.shortDescription || null,
        description: m.description || null,
        specification: m.specification || null,
        metaTitle: m.metaTitle || null,
        metaKeywords: m.metaKeywords || null,
        metaDescription: m.metaDescription || null,
        price: Number(m.price),
        oldPrice: m.oldPrice.trim() === '' ? null : Number(m.oldPrice),
        specialPrice: m.specialPrice.trim() === '' ? null : Number(m.specialPrice),
        specialPriceStart: m.specialPriceStart ? new Date(m.specialPriceStart).toISOString() : null,
        specialPriceEnd: m.specialPriceEnd ? new Date(m.specialPriceEnd).toISOString() : null,
        stockQuantity: Number(m.stockQuantity),
        brandId: m.brandId.trim() === '' ? null : Number(m.brandId),
        isPublished: m.isPublished,
        isFeatured: m.isFeatured,
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
