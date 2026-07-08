import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { CdkConnectedOverlay, CdkOverlayOrigin, type ConnectedPosition } from '@angular/cdk/overlay';
import { TranslatePipe } from '@ngx-translate/core';
import { Icon } from 'ui';

export type FilterValue = string | number;

/** One selectable row in a {@link FilterDropdown}. */
export interface FilterOption {
  value: FilterValue;
  label: string;
}

/**
 * Multi-select filter dropdown from the list spec: a ghost trigger that shows a
 * primary count badge + "Selected" once picks exist, opening a checkbox popover
 * (outside-click / Esc to close). Presentational — emits `selectedChange`.
 *
 * @example
 * <app-filter-dropdown label="Bid Status" [options]="statusOptions"
 *   [selected]="status()" (selectedChange)="status.set($event)" />
 */
@Component({
  selector: 'app-filter-dropdown',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CdkConnectedOverlay, CdkOverlayOrigin, TranslatePipe, Icon],
  template: `
    <button
      type="button"
      class="filter-trigger"
      [class.has-selection]="selected().length > 0"
      [class.open]="open()"
      cdkOverlayOrigin
      #trigger="cdkOverlayOrigin"
      [attr.aria-expanded]="open()"
      aria-haspopup="listbox"
      (click)="toggle()"
    >
      <span class="filter-trigger-label">{{ label() }}</span>
      @if (selected().length > 0) {
        <span class="filter-count">{{ selected().length }}</span>
        <span class="filter-selected-word">{{ 'common.selected' | translate }}</span>
      }
      <lib-icon name="chevDown" [size]="15" class="filter-chevron" />
    </button>

    <ng-template
      cdkConnectedOverlay
      [cdkConnectedOverlayOrigin]="trigger"
      [cdkConnectedOverlayOpen]="open()"
      [cdkConnectedOverlayHasBackdrop]="true"
      cdkConnectedOverlayBackdropClass="cdk-overlay-transparent-backdrop"
      [cdkConnectedOverlayPositions]="positions"
      (backdropClick)="close()"
      (overlayKeydown)="onKeydown($event)"
    >
      <div class="filter-panel" role="listbox" aria-multiselectable="true">
        @for (opt of options(); track opt.value) {
          <label class="filter-option">
            <input
              type="checkbox"
              class="form-check-input"
              [checked]="isChecked(opt.value)"
              (change)="toggleOption(opt.value)"
            />
            <span>{{ opt.label }}</span>
          </label>
        } @empty {
          <div class="filter-panel-empty">{{ 'common.no_options' | translate }}</div>
        }
        @if (options().length && selected().length) {
          <div class="filter-panel-actions">
            <button type="button" class="btn btn-link btn-sm p-0" (click)="clear()">
              {{ 'common.clear' | translate }}
            </button>
          </div>
        }
      </div>
    </ng-template>
  `,
})
export class FilterDropdown {
  readonly label = input.required<string>();
  readonly options = input<FilterOption[]>([]);
  readonly selected = input<FilterValue[]>([]);

  readonly selectedChange = output<FilterValue[]>();

  protected readonly open = signal(false);

  protected readonly positions: ConnectedPosition[] = [
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 6 },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -6 },
  ];

  protected toggle(): void {
    this.open.update((o) => !o);
  }

  protected close(): void {
    this.open.set(false);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.close();
    }
  }

  protected isChecked(value: FilterValue): boolean {
    return this.selected().includes(value);
  }

  protected toggleOption(value: FilterValue): void {
    const current = this.selected();
    const next = current.includes(value)
      ? current.filter((v) => v !== value)
      : [...current, value];
    this.selectedChange.emit(next);
  }

  protected clear(): void {
    this.selectedChange.emit([]);
  }
}
