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
  template: `
    @if (product.isLoading()) {
      <div class="state">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (product.error()) {
      <div class="alert alert-danger">
        {{ 'product.not_found' | translate }}
        <a routerLink="/shop">{{ 'product.back_to_shop' | translate }}</a>
      </div>
    } @else if (product.value(); as p) {
      <lib-breadcrumb [items]="crumbs(p)" />

      <div class="pdp">
        <!-- Gallery (sticky on wide screens): slider + hover-zoom + lightbox -->
        <div class="pdp-gallery">
          <div class="pdp-stage">
            @if (activeImage(); as img) {
              <div
                class="pdp-stage-img"
                [class.is-zoom]="zooming()"
                (mouseenter)="zooming.set(true)"
                (mousemove)="onZoomMove($event)"
                (mouseleave)="zooming.set(false)"
                (click)="openLightbox()"
                (keydown.enter)="openLightbox()"
                role="button"
                tabindex="0"
                [attr.aria-label]="'common.zoom' | translate"
              >
                <img class="pdp-stage-src" [src]="img" [alt]="p.name"
                  [style.transform-origin]="zoomOrigin()" />
                <span class="pdp-zoom-hint" aria-hidden="true"><lib-icon name="search" [size]="16" /></span>
              </div>
            } @else {
              <lib-tile class="pdp-main" [seed]="p.name ?? p.id" [alt]="p.name" ratio="1x1" />
            }

            @if (galleryImages().length > 1) {
              <button type="button" class="pdp-nav prev" (click)="prev(); $event.stopPropagation()"
                [attr.aria-label]="'common.prev' | translate">
                <lib-icon name="chevStart" [size]="22" />
              </button>
              <button type="button" class="pdp-nav next" (click)="next(); $event.stopPropagation()"
                [attr.aria-label]="'common.next' | translate">
                <lib-icon name="chevEnd" [size]="22" />
              </button>
              <span class="pdp-count tabular-nums">{{ activeIndex() + 1 }} / {{ galleryImages().length }}</span>
            }
          </div>

          @if (galleryImages().length) {
            <div class="pdp-thumbs">
              @for (url of galleryImages(); track url; let i = $index) {
                <button type="button" class="pdp-thumb-btn" [class.is-on]="i === activeIndex()"
                  (click)="selectImage(i)" [attr.aria-label]="p.name">
                  <lib-tile [src]="url" [seed]="p.name ?? p.id" ratio="1x1" />
                </button>
              }
            </div>
          } @else {
            <div class="pdp-thumbs" aria-hidden="true">
              @for (i of [1, 2, 3, 4]; track i) {
                <lib-tile [seed]="(p.name ?? '') + i" ratio="1x1" />
              }
            </div>
          }
        </div>

        <!-- Info -->
        <div class="pdp-info">
          @if (category(); as cat) {
            <a class="pdp-cat" [routerLink]="['/shop']" [queryParams]="{ category: cat.slug }">{{ cat.name }}</a>
          }
          <h1 class="pdp-title">{{ p.name }}</h1>

          <div class="pdp-meta">
            @if (p.ratingAverage; as rating) {
              <span class="pdp-rating">
                <lib-stars [rating]="rating" />
                <span class="tabular-nums">{{ rating.toFixed(1) }}</span>
              </span>
              <span class="dot"></span>
            }
            <span class="muted">{{ 'product.reviews' | translate: { count: p.reviewsCount } }}</span>
            <span class="dot"></span>
            <span class="badge-stock"><lib-icon name="check" [size]="13" /> {{ 'product.in_stock' | translate }}</span>
          </div>

          <div class="pdp-price">
            <span class="pdp-now tabular-nums">{{ activePrice().price | money }}</span>
            @if (activePrice().oldPrice; as oldPrice) {
              <span class="pdp-old tabular-nums">{{ oldPrice | money }}</span>
              <span class="badge-sale">
                {{ 'product.save' | translate: { percent: activePrice().percentOfSaving } }}
              </span>
            }
          </div>

          <p class="pdp-desc">
            @if (p.shortDescription) {
              <span [innerHTML]="p.shortDescription"></span>
            }
            {{ 'product.handmade_note' | translate }}
          </p>

          <!-- Origin / provenance card -->
          <div class="pdp-origin">
            <span class="pdp-origin-ic"><lib-icon name="pin" [size]="22" /></span>
            <span class="pdp-origin-text">
              <b>{{ 'product.made_in' | translate: { place: originPlace() } }}</b>
              <span class="muted">{{ 'product.origin_note' | translate }}</span>
            </span>
          </div>

          @if (p.variations?.length) {
            <div class="pdp-variations" role="group" [attr.aria-label]="'product.options' | translate">
              <div class="pdp-label">{{ 'product.options' | translate }}</div>
              <div class="pdp-chips">
                @for (variation of p.variations; track variation.id) {
                  <button
                    type="button"
                    class="chip"
                    [class.active]="variation.id === selectedVariationId()"
                    [disabled]="variation.stockTrackingIsEnabled && variation.stockQuantity === 0"
                    (click)="selectedVariationId.set(variation.id)"
                  >
                    {{ variation.name }}
                  </button>
                }
              </div>
            </div>
          }

          @if (p.isCallForPricing) {
            <div class="alert alert-secondary">{{ 'product.call_for_pricing_long' | translate }}</div>
          } @else {
            <div class="pdp-buy">
              <lib-stepper [(value)]="quantity" [min]="1" [max]="maxQty()" />
              <button
                libButton
                variant="success"
                size="lg"
                class="pdp-add"
                [disabled]="!canOrder() || adding()"
                (click)="addToCart(p)"
              >
                <lib-icon name="bag" [size]="18" class="me-2" />
                {{ (canOrder() ? 'product.add' : 'product.out_of_stock') | translate }}
              </button>
              <button type="button" class="pdp-wish" [class.is-on]="wished()"
                [attr.aria-pressed]="wished()" [attr.aria-label]="'product.wishlist' | translate"
                (click)="toggleWish(p.id)">
                <lib-icon name="heart" [size]="20" />
              </button>
            </div>
          }

          <!-- Trust perks -->
          <div class="pdp-perks">
            @for (perk of perks; track perk.title) {
              <div class="pdp-perk">
                <span class="pdp-perk-ic"><lib-icon [name]="perk.icon" [size]="20" /></span>
                <span class="pdp-perk-text">
                  <b>{{ perk.title | translate }}</b>
                  <small class="muted">{{ perk.text | translate }}</small>
                </span>
              </div>
            }
          </div>
        </div>
      </div>

      <!-- Extra content (description / specification / attributes) as detail bands -->
      @if (p.description || p.specification || p.attributes?.length) {
        <section class="pdp-detail2">
          @if (p.description) {
            <div class="formcard desc">
              <h3>{{ 'product.description' | translate }}</h3>
              <div class="craft-body" [innerHTML]="p.description"></div>
            </div>
          }
          @if (p.specification) {
            <div class="formcard desc">
              <h3>{{ 'product.specification' | translate }}</h3>
              <div class="craft-body" [innerHTML]="p.specification"></div>
            </div>
          }
          @if (p.attributes?.length) {
            <div class="formcard">
              <h3>{{ 'product.details' | translate }}</h3>
              <ul class="speclist">
                @for (attribute of p.attributes; track attribute.name) {
                  <li><span class="muted">{{ attribute.name }}</span><b>{{ attribute.value }}</b></li>
                }
              </ul>
            </div>
          }
        </section>
      }
      <section class="pdp-reviews">
        <div class="sec-head">
          <h2 class="pdp-related-title">{{ 'reviews.title' | translate }}</h2>
          @if (reviews().length) {
            <span class="reviews-count">
              {{ 'product.reviews' | translate: { count: reviews().length } }}
            </span>
          }
        </div>

        @if (reviews().length) {
          <div class="review-list">
            @for (review of reviews(); track review.id) {
              <article class="review-card">
                <span class="review-avatar" aria-hidden="true">{{ initial(review.reviewerName) }}</span>
                <div class="review-main">
                  <div class="review-head">
                    <span class="review-author">{{ review.reviewerName }}</span>
                    <span class="review-date">{{ review.createdOn | date: 'mediumDate' : '' : locale() }}</span>
                  </div>
                  <lib-stars [rating]="review.rating" />
                  @if (review.title) {
                    <div class="review-title">{{ review.title }}</div>
                  }
                  <p class="review-body">{{ review.comment }}</p>
                </div>
              </article>
            }
          </div>
        } @else {
          <div class="review-empty">{{ 'reviews.empty' | translate }}</div>
        }

        @if (isAuthenticated()) {
          @if (reviewSubmitted()) {
            <div class="review-thanks">
              <lib-icon name="check" [size]="18" /> {{ 'reviews.submitted' | translate }}
            </div>
          } @else {
            <div class="formcard review-form">
              <h3>{{ 'reviews.write' | translate }}</h3>
              <div class="review-grid">
                <div class="field rating-field">
                  <label class="field-label" for="rv-rating">{{ 'reviews.rating' | translate }}</label>
                  <select id="rv-rating" class="form-control"
                          [value]="reviewRating()"
                          (change)="reviewRating.set(+$any($event.target).value)">
                    @for (n of [5, 4, 3, 2, 1]; track n) {
                      <option [value]="n">{{ n }} ★</option>
                    }
                  </select>
                </div>
                <div class="field title-field">
                  <label class="field-label" for="rv-title">{{ 'reviews.your_title' | translate }}</label>
                  <input id="rv-title" type="text" class="form-control"
                         [value]="reviewTitle()" (input)="reviewTitle.set($any($event.target).value)" />
                </div>
                <div class="field comment-field">
                  <label class="field-label" for="rv-comment">{{ 'reviews.your_comment' | translate }}</label>
                  <textarea id="rv-comment" rows="4" class="form-control"
                            [value]="reviewComment()" (input)="reviewComment.set($any($event.target).value)"></textarea>
                </div>
                <div class="form-actions">
                  <button type="button" libButton variant="dark"
                          [disabled]="submittingReview()" (click)="submitReview(p.id)">
                    {{ 'reviews.submit' | translate }}
                  </button>
                </div>
              </div>
            </div>
          }
        } @else {
          <p class="review-signin">{{ 'reviews.signin' | translate }}</p>
        }
      </section>
      @if (p.relatedProducts?.length) {
        <section class="pdp-related mt-5">
          <div class="sec-head">
            <h2 class="pdp-related-title">{{ 'product.related' | translate }}</h2>
            @if (category(); as cat) {
              <a class="sec-link" [routerLink]="['/shop']" [queryParams]="{ category: cat.slug }">
                {{ 'shop.title' | translate }} <lib-icon name="arrowEnd" [size]="16" />
              </a>
            }
          </div>
          <div class="row row-cols-2 row-cols-md-4 g-4">
            @for (related of p.relatedProducts; track related.id) {
              <div class="col">
                <app-product-card [product]="related" (addToCart)="quickAdd($event)" />
              </div>
            }
          </div>
        </section>
      }



      <!-- Fullscreen lightbox: zoomed product view with slider controls -->
      @if (lightboxOpen() && activeImage(); as img) {
        <div class="pdp-lightbox" role="dialog" aria-modal="true"
          (click)="onBackdropClick($event)" (keydown.escape)="closeLightbox()">
          <button type="button" class="lb-btn lb-close" (click)="closeLightbox()"
            [attr.aria-label]="'common.close' | translate">
            <lib-icon name="x" [size]="24" />
          </button>
          @if (galleryImages().length > 1) {
            <button type="button" class="lb-btn lb-nav prev" (click)="prev()"
              [attr.aria-label]="'common.prev' | translate">
              <lib-icon name="chevStart" [size]="30" />
            </button>
            <button type="button" class="lb-btn lb-nav next" (click)="next()"
              [attr.aria-label]="'common.next' | translate">
              <lib-icon name="chevEnd" [size]="30" />
            </button>
          }
          <img class="lb-img" [src]="img" [alt]="p.name" />
          @if (galleryImages().length > 1) {
            <span class="lb-count tabular-nums">{{ activeIndex() + 1 }} / {{ galleryImages().length }}</span>
          }
        </div>
      }
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .state {
      text-align: center;
      padding-block: 5rem;
    }
    .muted {
      color: var(--ink-2);
    }

    /* ---- Hero ---- */
    .pdp {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 50px;
      align-items: start;
      padding-block: 36px;
    }
    .pdp-gallery {
      position: sticky;
      top: 96px;
    }
    .pdp-main {
      display: block;
      border-radius: var(--r-xl);
      overflow: hidden;
      box-shadow: var(--shadow-md);
    }

    /* ---- Main stage (slider + hover zoom) ---- */
    .pdp-stage {
      position: relative;
    }
    .pdp-stage-img {
      position: relative;
      display: block;
      aspect-ratio: 1 / 1;
      inline-size: 100%;
      overflow: hidden;
      border-radius: var(--r-xl);
      box-shadow: var(--shadow-md);
      background: var(--surface-2);
      cursor: zoom-in;
    }
    .pdp-stage-src {
      inline-size: 100%;
      block-size: 100%;
      object-fit: cover;
      display: block;
      transition: transform 0.18s ease;
      will-change: transform;
    }
    .pdp-stage-img.is-zoom .pdp-stage-src {
      transform: scale(2.3);
      transition: none;
    }
    .pdp-stage-img:focus-visible {
      outline: 2px solid var(--accent);
      outline-offset: 3px;
    }
    .pdp-zoom-hint {
      position: absolute;
      inset-block-end: 12px;
      inset-inline-end: 12px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 36px;
      block-size: 36px;
      border-radius: 50%;
      background: color-mix(in srgb, var(--ink) 62%, transparent);
      color: #fff;
      pointer-events: none;
      opacity: 0.85;
      transition: opacity 0.18s ease;
    }
    .pdp-stage-img.is-zoom .pdp-zoom-hint {
      opacity: 0;
    }
    .pdp-nav {
      position: absolute;
      inset-block-start: 50%;
      transform: translateY(-50%);
      z-index: 2;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 42px;
      block-size: 42px;
      border: 1px solid var(--line);
      border-radius: 50%;
      background: color-mix(in srgb, var(--surface) 88%, transparent);
      color: var(--ink);
      cursor: pointer;
      box-shadow: var(--shadow-sm);
      opacity: 0;
      transition: opacity 0.18s ease, background 0.18s ease;
    }
    .pdp-stage:hover .pdp-nav,
    .pdp-nav:focus-visible {
      opacity: 1;
    }
    .pdp-nav:hover {
      background: var(--surface);
    }
    .pdp-nav.prev {
      inset-inline-start: 12px;
    }
    .pdp-nav.next {
      inset-inline-end: 12px;
    }
    .pdp-count {
      position: absolute;
      inset-block-end: 12px;
      inset-inline-start: 12px;
      z-index: 2;
      padding-block: 2px;
      padding-inline: 10px;
      border-radius: 999px;
      background: color-mix(in srgb, var(--ink) 62%, transparent);
      color: #fff;
      font-size: 0.78rem;
      font-weight: 600;
    }
    .pdp-thumbs {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 12px;
      margin-block-start: 14px;
    }
    .pdp-thumb-btn {
      padding: 0;
      background: none;
      cursor: pointer;
      border: 2px solid transparent;
      border-radius: var(--r);
      overflow: hidden;
      line-height: 0;
    }
    .pdp-thumb-btn.is-on {
      border-color: var(--green);
    }
    .pdp-thumb-btn:focus-visible {
      outline: 2px solid var(--accent);
      outline-offset: 2px;
    }

    /* ---- Lightbox ---- */
    .pdp-lightbox {
      position: fixed;
      inset: 0;
      z-index: 1080;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: clamp(1rem, 4vw, 3rem);
      background: color-mix(in srgb, #000 86%, transparent);
      backdrop-filter: blur(2px);
      animation: lb-fade 0.18s ease;
    }
    @keyframes lb-fade {
      from { opacity: 0; }
      to { opacity: 1; }
    }
    .lb-img {
      max-inline-size: min(92vw, 1100px);
      max-block-size: 88vh;
      object-fit: contain;
      border-radius: var(--r);
      box-shadow: 0 24px 60px rgba(0, 0, 0, 0.5);
    }
    .lb-btn {
      position: absolute;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border: none;
      border-radius: 50%;
      background: rgba(255, 255, 255, 0.12);
      color: #fff;
      cursor: pointer;
      transition: background 0.18s ease;
    }
    .lb-btn:hover {
      background: rgba(255, 255, 255, 0.24);
    }
    .lb-close {
      inset-block-start: clamp(1rem, 3vw, 2rem);
      inset-inline-end: clamp(1rem, 3vw, 2rem);
      inline-size: 46px;
      block-size: 46px;
    }
    .lb-nav {
      inset-block-start: 50%;
      transform: translateY(-50%);
      inline-size: 54px;
      block-size: 54px;
    }
    .lb-nav.prev {
      inset-inline-start: clamp(0.5rem, 3vw, 2rem);
    }
    .lb-nav.next {
      inset-inline-end: clamp(0.5rem, 3vw, 2rem);
    }
    .lb-count {
      position: absolute;
      inset-block-end: clamp(1rem, 3vw, 2rem);
      inset-inline-start: 50%;
      transform: translateX(-50%);
      color: rgba(255, 255, 255, 0.85);
      font-size: 0.85rem;
      font-weight: 600;
    }

    /* ---- Info column ---- */
    .pdp-info {
      align-self: start;
    }
    .pdp-cat {
      color: var(--accent);
      font-size: 0.9rem;
      font-weight: 600;
      text-decoration: none;
    }
    .pdp-cat:hover {
      text-decoration: underline;
    }
    .pdp-title {
      font-weight: 700;
      font-size: clamp(1.7rem, 3.2vw, 2.4rem);
      letter-spacing: -0.02em;
      margin-block: 8px 0;
    }
    .pdp-meta {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 12px;
      margin-block-start: 14px;
      color: var(--ink-2);
      font-size: 0.92rem;
    }
    .pdp-rating {
      display: inline-flex;
      align-items: center;
      gap: 8px;
    }
    .pdp-meta .dot {
      inline-size: 4px;
      block-size: 4px;
      border-radius: 50%;
      background: var(--line-strong);
    }
    .badge-stock {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      background: var(--green-soft);
      color: var(--green-strong);
      padding-block: 3px;
      padding-inline: 10px;
      border-radius: 999px;
      font-weight: 600;
      font-size: 0.82rem;
    }
    .pdp-price {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 12px;
      margin-block-start: 22px;
    }
    .pdp-now {
      font-size: 2rem;
      font-weight: 700;
      color: var(--ink);
    }
    .pdp-old {
      color: var(--ink-3);
      text-decoration: line-through;
    }
    .badge-sale {
      background: #f6e2dc;
      color: #b0492c;
      padding-block: 4px;
      padding-inline: 12px;
      border-radius: 999px;
      font-weight: 600;
      font-size: 0.82rem;
    }
    .pdp-desc {
      color: var(--ink-2);
      line-height: 1.8;
      margin-block-start: 22px;
    }

    /* ---- Origin card ---- */
    .pdp-origin {
      //display: flex;
      align-items: center;
      gap: 14px;
      background: var(--surface-2);
      border-radius: var(--r);
      padding: 16px 18px;
      margin-block-start: 24px;
      display: none;
    }
    .pdp-origin-ic {
      flex: 0 0 auto;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 46px;
      block-size: 46px;
      border-radius: 50%;
      background: var(--navy);
      color: #fff;
    }
    .pdp-origin-text {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .pdp-origin-text b {
      color: var(--ink);
    }
    .pdp-origin-text .muted {
      font-size: 0.86rem;
    }

    /* ---- Variations ---- */
    .pdp-variations {
      margin-block-start: 22px;
    }
    .pdp-label {
      font-weight: 600;
      margin-block-end: 0.5rem;
    }
    .pdp-chips {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .chip {
      border: 1px solid var(--line);
      border-radius: 999px;
      background: var(--surface);
      color: var(--ink);
      padding-block: 0.45rem;
      padding-inline: 1rem;
      font-weight: 500;
      cursor: pointer;
    }
    .chip.active {
      background: var(--ink);
      border-color: var(--ink);
      color: var(--surface);
    }
    .chip:disabled {
      opacity: 0.4;
      cursor: not-allowed;
    }

    /* ---- Buy row ---- */
    .pdp-buy {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 14px;
      margin-block-start: 24px;
    }
    .pdp-add {
      flex: 1;
      min-inline-size: 200px;
    }
    .pdp-wish {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 52px;
      block-size: 52px;
      border: 1.5px solid var(--line-strong);
      border-radius: var(--r);
      background: var(--surface);
      color: var(--ink-2);
      cursor: pointer;
    }
    .pdp-wish:hover {
      border-color: var(--ink-3);
    }
    .pdp-wish.is-on {
      color: #c0392b;
      border-color: #e2b6ac;
    }

    /* ---- Perks ---- */
    .pdp-perks {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 14px;
      margin-block-start: 28px;
      padding-block-start: 24px;
      border-block-start: 1px solid var(--line);
    }
    .pdp-perk {
      display: flex;
      align-items: flex-start;
      gap: 12px;
    }
    .pdp-perk-ic {
      flex: 0 0 auto;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 40px;
      block-size: 40px;
      border-radius: 50%;
      background: var(--green-soft);
      color: var(--green-strong);
    }
    .pdp-perk-text {
      display: flex;
      flex-direction: column;
    }
    .pdp-perk-text small {
      font-size: 0.82rem;
    }

    /* ---- Details band ---- */
    .pdp-detail2 {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 20px;
      margin-block-end: 3rem;
    }
    .formcard {
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      padding: 26px;
    }
    .formcard h3 {
      font-weight: 700;
      font-size: 1.15rem;
      margin-block-end: 1rem;
    }
    .speclist {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    .speclist li {
      display: flex;
      justify-content: space-between;
      gap: 16px;
      padding-block: 11px;
      border-block-end: 1px solid var(--line);
    }
    .speclist li:last-child {
      border-block-end: 0;
    }
    .speclist b {
      color: var(--ink);
    }
    .craft-body {
      line-height: 1.8;
    }

    /* ---- Related / reviews ---- */
    .sec-head {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: 1rem;
      margin-block-end: 1.5rem;
    }
    .pdp-related-title {
      font-weight: 700;
      font-size: clamp(1.5rem, 3vw, 2rem);
      letter-spacing: -0.02em;
      margin: 0;
    }
    .sec-link {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      color: var(--accent);
      font-weight: 600;
      text-decoration: none;
      white-space: nowrap;
    }
    .sec-link:hover {
      text-decoration: underline;
    }
    .pdp-reviews {
      /*margin-block: 3rem 4rem;
      max-inline-size: 48rem;*/
      width: 100%;
    }
    .reviews-count {
      color: var(--ink-2);
      font-size: 0.92rem;
      font-weight: 600;
      white-space: nowrap;
    }
    .review-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      margin-block-end: 2rem;
    }
    .review-card {
      display: flex;
      gap: 14px;
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      padding: 1.1rem 1.25rem;
    }
    .review-avatar {
      flex: 0 0 auto;
      inline-size: 42px;
      block-size: 42px;
      border-radius: 50%;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      background: var(--green-soft);
      color: var(--green-strong);
      font-weight: 700;
      text-transform: uppercase;
    }
    .review-main {
      flex: 1;
      min-inline-size: 0;
    }
    .review-head {
      display: flex;
      align-items: baseline;
      justify-content: space-between;
      gap: 0.75rem;
      margin-block-end: 0.4rem;
    }
    .review-author {
      font-weight: 700;
      color: var(--ink);
    }
    .review-date {
      font-size: 0.8rem;
      color: var(--ink-3);
      white-space: nowrap;
    }
    .review-title {
      font-weight: 600;
      margin-block-start: 0.45rem;
    }
    .review-body {
      margin: 0.3rem 0 0;
      color: var(--ink-2);
      line-height: 1.7;
    }
    .review-empty {
      background: var(--surface-2);
      border-radius: var(--r);
      padding: 1.75rem;
      text-align: center;
      color: var(--ink-2);
      margin-block-end: 2rem;
    }

    /* ---- Write-a-review form ---- */
    .review-form {
      margin-block-start: 0.5rem;
    }
    .review-form h3 {
      font-weight: 700;
      font-size: 1.15rem;
      margin-block-end: 1.25rem;
    }
    .review-grid {
      display: grid;
      grid-template-columns: 150px 1fr;
      gap: 1rem;
    }
    .comment-field,
    .form-actions {
      grid-column: 1 / -1;
    }
    .field {
      display: flex;
      flex-direction: column;
      gap: 0.4rem;
    }
    .field-label {
      font-weight: 600;
      font-size: 0.9rem;
      color: var(--ink);
    }
    .review-form .form-control {
      inline-size: 100%;
      border: 1.5px solid var(--line);
      border-radius: var(--r-sm);
      padding: 0.7rem 0.9rem;
      background: var(--surface);
      color: var(--ink);
      font: inherit;
    }
    .review-form .form-control:focus {
      outline: none;
      border-color: var(--navy);
      box-shadow: none;
    }
    .review-thanks {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      background: var(--green-soft);
      color: var(--green-strong);
      border-radius: var(--r);
      padding: 0.85rem 1.25rem;
      font-weight: 600;
    }
    .review-signin {
      color: var(--ink-2);
      font-size: 0.92rem;
    }

    /* ---- Responsive ---- */
    @media (max-width: 980px) {
      .pdp {
        grid-template-columns: 1fr;
        gap: 30px;
      }
      .pdp-gallery {
        position: static;
      }
    }
    @media (max-width: 760px) {
      .pdp-detail2 {
        grid-template-columns: 1fr;
      }
    }
    @media (max-width: 600px) {
      .pdp-perks {
        grid-template-columns: 1fr;
      }
      .review-grid {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class ProductDetail {
  private readonly catalog = inject(CatalogService);
  private readonly route = inject(ActivatedRoute);
  private readonly cart = inject(CartStore);
  private readonly seo = inject(SeoService);
  private readonly toast = inject(ToastService);
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

  /** Upper bound for the quantity stepper (tracked stock, else generous cap). */
  protected readonly maxQty = computed(() => {
    const p = this.product.value();
    const source = this.selectedVariation() ?? p;
    if (!source) {
      return 99;
    }
    const tracksStock =
      'stockTrackingIsEnabled' in source ? source.stockTrackingIsEnabled : false;
    return tracksStock && source.stockQuantity > 0 ? source.stockQuantity : 99;
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
      next: () => {
        this.adding.set(false);
        this.toast.success(this.translate.instant('product.added'));
      },
      error: () => {
        this.adding.set(false);
        this.toast.error(this.translate.instant('common.error'));
      },
    });
  }

  protected quickAdd(product: ProductListItem): void {
    this.cart.add(product).subscribe({
      next: () => this.toast.success(this.translate.instant('product.added')),
      error: () => this.toast.error(this.translate.instant('common.error')),
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
