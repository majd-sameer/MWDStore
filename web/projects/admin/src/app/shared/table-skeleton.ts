import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';


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
  readonly rows = input(6);
  readonly columns = input<number[]>([3, 2, 1, 1, 2]);
  readonly ariaLabel = input('Loading');

  protected readonly rowList = computed(() => Array.from({ length: this.rows() }));
}
