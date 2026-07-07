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
  templateUrl: './breadcrumb.html',
})
export class Breadcrumb {
  readonly items = input.required<readonly BreadcrumbItem[]>();
  readonly ariaLabel = input('Breadcrumb');
}
