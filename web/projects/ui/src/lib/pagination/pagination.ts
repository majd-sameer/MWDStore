import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core';

type PageItem = number | 'ellipsis';

/**
 * Bootstrap pagination control. Purely presentational: it owns no page state —
 * it renders from `page` / `pageSize` / `collectionSize` and emits the
 * requested page through `pageChange`.
 *
 * @example
 * <lib-pagination [page]="page()" [pageSize]="20" [collectionSize]="total()"
 *                 (pageChange)="page.set($event)" />
 */
@Component({
  selector: 'lib-pagination',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav aria-label="Pagination">
      <ul class="pagination m-0" [class]="sizeClass()">
        <li class="page-item" [class.disabled]="current() === 1">
          <button
            type="button"
            class="page-link"
            aria-label="Previous"
            [disabled]="current() === 1"
            (click)="select(current() - 1)"
          >
            <span aria-hidden="true">&laquo;</span>
          </button>
        </li>

        @for (item of items(); track $index) {
          @if (item === 'ellipsis') {
            <li class="page-item disabled">
              <span class="page-link">&hellip;</span>
            </li>
          } @else {
            <li class="page-item" [class.active]="item === current()">
              <button
                type="button"
                class="page-link"
                [attr.aria-current]="item === current() ? 'page' : null"
                (click)="select(item)"
              >
                {{ item }}
              </button>
            </li>
          }
        }

        <li class="page-item" [class.disabled]="current() === pageCount()">
          <button
            type="button"
            class="page-link"
            aria-label="Next"
            [disabled]="current() === pageCount()"
            (click)="select(current() + 1)"
          >
            <span aria-hidden="true">&raquo;</span>
          </button>
        </li>
      </ul>
    </nav>
  `,
})
export class Pagination {
  readonly page = input(1);
  readonly pageSize = input(10);
  readonly collectionSize = input(0);
  /** Maximum number of numbered page links to show before collapsing with ellipses. */
  readonly maxSize = input(5);
  readonly size = input<'sm' | 'lg' | null>(null);

  readonly pageChange = output<number>();

  protected readonly pageCount = computed(() =>
    Math.max(1, Math.ceil(this.collectionSize() / this.pageSize())),
  );

  protected readonly current = computed(() =>
    Math.min(Math.max(this.page(), 1), this.pageCount()),
  );

  protected readonly sizeClass = computed(() => {
    const size = this.size();
    return size ? `pagination-${size}` : '';
  });

  protected readonly items = computed<PageItem[]>(() => {
    const total = this.pageCount();
    const max = Math.max(1, this.maxSize());

    if (total <= max) {
      return Array.from({ length: total }, (_, i) => i + 1);
    }

    const half = Math.floor(max / 2);
    let start = Math.max(1, this.current() - half);
    const end = Math.min(total, start + max - 1);
    start = Math.max(1, end - max + 1);

    const result: PageItem[] = [];
    if (start > 1) {
      result.push(1);
      if (start > 2) {
        result.push('ellipsis');
      }
    }
    for (let i = start; i <= end; i++) {
      result.push(i);
    }
    if (end < total) {
      if (end < total - 1) {
        result.push('ellipsis');
      }
      result.push(total);
    }
    return result;
  });

  protected select(page: number): void {
    if (page < 1 || page > this.pageCount() || page === this.current()) {
      return;
    }
    this.pageChange.emit(page);
  }
}
