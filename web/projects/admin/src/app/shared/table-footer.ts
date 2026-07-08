import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
import { TranslatePipe } from '@ngx-translate/core';
import { Pagination } from 'ui';

/**
 * Data-table footer per the list spec: total count on the leading side, then a
 * "lines per page" select and numbered pagination on the trailing side. Purely
 * presentational — it owns no state, emitting `pageChange` / `pageSizeChange`.
 *
 * @example
 * <app-table-footer [total]="total()" [page]="page()" [pageSize]="pageSize()"
 *   (pageChange)="page.set($event)" (pageSizeChange)="pageSize.set($event)" />
 */
@Component({
  selector: 'app-table-footer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NgSelectModule, TranslatePipe, Pagination],
  template: `
    <div class="table-footer">
      <span class="table-footer-total">
        {{ 'common.total' | translate }} <strong>{{ total() }}</strong>
      </span>
      <div class="table-footer-controls">
        <span class="table-footer-perpage">
          <span>{{ 'common.lines_per_page' | translate }}</span>
          <ng-select
            class="perpage-select"
            [items]="pageSizeOptions()"
            [ngModel]="pageSize()"
            (ngModelChange)="pageSizeChange.emit($event)"
            [clearable]="false"
            [searchable]="false"
            appendTo="body"
            [attr.aria-label]="'common.lines_per_page' | translate"
          />
        </span>
        <lib-pagination
          [page]="page()"
          [pageSize]="pageSize()"
          [collectionSize]="total()"
          size="sm"
          (pageChange)="pageChange.emit($event)"
        />
      </div>
    </div>
  `,
})
export class TableFooter {
  readonly total = input(0);
  readonly page = input(1);
  readonly pageSize = input(15);
  readonly pageSizeOptions = input<number[]>([15, 30, 50, 100]);

  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();
}
