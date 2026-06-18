import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from '../icon/icon';

export interface BreadcrumbItem {
  readonly label: string;
  /** RouterLink target. Omit for the current (last) item. */
  readonly link?: string | readonly unknown[];
}

/**
 * Breadcrumb trail. Links every item except the last (the current page). The
 * separator uses the directional `chevEnd` icon so the trail mirrors in RTL.
 *
 * @example
 * <lib-breadcrumb [items]="[{ label: 'Home', link: '/' }, { label: category().name }]" />
 */
@Component({
  selector: 'lib-breadcrumb',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon],
  template: `
    <nav [attr.aria-label]="ariaLabel()">
      <ol class="ui-breadcrumb">
        @for (item of items(); track $index; let last = $last) {
          <li class="ui-breadcrumb__item">
            @if (item.link && !last) {
              <a [routerLink]="item.link" class="ui-breadcrumb__link">{{ item.label }}</a>
            } @else {
              <span [attr.aria-current]="last ? 'page' : null">{{ item.label }}</span>
            }
            @if (!last) {
              <lib-icon name="chevEnd" [size]="14" class="ui-breadcrumb__sep" />
            }
          </li>
        }
      </ol>
    </nav>
  `,
})
export class Breadcrumb {
  readonly items = input.required<readonly BreadcrumbItem[]>();
  readonly ariaLabel = input('Breadcrumb');
}
