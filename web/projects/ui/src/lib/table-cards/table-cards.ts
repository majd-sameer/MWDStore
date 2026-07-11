import {
  AfterViewInit,
  Directive,
  ElementRef,
  inject,
  OnDestroy,
} from '@angular/core';

/**
 * Turns a Bootstrap table into stacked cards on narrow screens.
 *
 * Apply to any `<table libTableCards>`: the directive stamps each body cell
 * with a `data-label` attribute copied from its column header, and the
 * companion `.table-cards` CSS (in each app's `styles.scss`) renders rows as
 * cards below 768px — header text on the start side, cell value on the end
 * side. Labels re-stamp automatically when rows render or the language flips
 * (a MutationObserver watches the table), so translated headers stay correct.
 */
@Directive({
  selector: 'table[libTableCards]',
  host: { class: 'table-cards' },
})
export class TableCards implements AfterViewInit, OnDestroy {
  private readonly table = inject<ElementRef<HTMLTableElement>>(ElementRef);
  private observer: MutationObserver | null = null;
  private scheduled = false;

  ngAfterViewInit(): void {
    this.stamp();
    this.observer = new MutationObserver(() => {
      // Coalesce bursts (row re-renders, language switches) into one pass —
      // stamping mutates attributes, which would otherwise re-trigger us.
      if (this.scheduled) {
        return;
      }
      this.scheduled = true;
      requestAnimationFrame(() => {
        this.scheduled = false;
        this.stamp();
      });
    });
    this.observer.observe(this.table.nativeElement, {
      childList: true,
      subtree: true,
      characterData: true,
    });
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  private stamp(): void {
    const table = this.table.nativeElement;
    const headers = Array.from(table.querySelectorAll<HTMLTableCellElement>('thead th')).map(
      (th) => th.textContent?.trim() ?? '',
    );
    if (!headers.length) {
      return;
    }

    for (const row of Array.from(table.querySelectorAll<HTMLTableRowElement>('tbody tr'))) {
      let column = 0;
      for (const cell of Array.from(row.cells)) {
        const label = headers[column] ?? '';
        if (cell.colSpan > 1 || !label) {
          // Spanning cells (empty states) and unlabelled columns (actions)
          // render full-width without a label.
          if (cell.getAttribute('data-label') !== null) {
            cell.removeAttribute('data-label');
          }
        } else if (cell.getAttribute('data-label') !== label) {
          cell.setAttribute('data-label', label);
        }
        column += cell.colSpan;
      }
    }
  }
}
