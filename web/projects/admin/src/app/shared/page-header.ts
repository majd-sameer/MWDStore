import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Standard admin page header — the band every console page opens with, modelled
 * on the products list: a title + optional subtitle on the leading edge and a
 * right-aligned actions area (projected content) for the primary "New …" button.
 *
 * Usage:
 * ```html
 * <app-page-header [title]="'products.title' | translate"
 *                  [subtitle]="'products.subtitle' | translate">
 *   <a routerLink="/products/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
 *     <lib-icon name="plus" [size]="18" /> {{ 'products.new' | translate }}
 *   </a>
 * </app-page-header>
 * ```
 *
 * Pages pass already-resolved strings (translate them in the caller), so this
 * stays framework-pure and works for both translated and plain-text pages.
 */
@Component({
  selector: 'app-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './page-header.html',
})
export class PageHeader {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>();
}
