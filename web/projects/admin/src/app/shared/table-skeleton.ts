import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Loading placeholder for a data table — shimmer bars laid out in rows, sized
 * per column so it echoes the table it stands in for. Drop it into the
 * `@if (isLoading())` branch instead of a spinner.
 *
 * @example
 * <app-table-skeleton [rows]="8" [columns]="[3, 2, 1, 1, 2]" />
 */
@Component({
  selector: 'app-table-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="table-skeleton" role="status" [attr.aria-label]="ariaLabel()">
      @for (row of rowList(); track $index) {
        <div class="table-skeleton-row">
          @for (weight of columns(); track $index) {
            <span class="table-skeleton-bar" [style.flex-grow]="weight"></span>
          }
        </div>
      }
    </div>
  `,
})
export class TableSkeleton {
  /** Number of placeholder rows. */
  readonly rows = input(6);
  /** Relative width weight per column (bars flex-grow by these). */
  readonly columns = input<number[]>([3, 2, 1, 1, 2]);
  readonly ariaLabel = input('Loading');

  protected readonly rowList = computed(() => Array.from({ length: this.rows() }));
}
