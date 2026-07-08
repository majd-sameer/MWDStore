import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  OwlDateTimeModule,
  OwlNativeDateTimeModule,
} from '@danielmoncada/angular-datetime-picker';
import { AdminAuditService, type AdminAuditQuery } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { LanguageService } from 'core';
import { Icon } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';
import { TableFooter } from '../../shared/table-footer';
import { FilterDropdown, type FilterOption, type FilterValue } from '../../shared/filter-dropdown';
import { dayBoundary } from '../../shared/date-range';

const DEFAULT_PAGE_SIZE = 50;

/** One before/after pair rendered in the detail drawer. */
interface DiffRow {
  readonly key: string;
  readonly oldValue: string | null;
  readonly newValue: string | null;
}

/**
 * Read-only view of the append-only audit trail (`GET /api/admin/audit-logs`,
 * Settings-gated). Server-side multi-select filters for action, area, plus actor
 * and date range; a side drawer renders the changed properties old-vs-new from
 * the stored JSON. The endpoint returns a paged envelope with a total count, so
 * pagination is numbered — matching the orders list convention.
 */
@Component({
  selector: 'app-admin-audit-log',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    FormsModule,
    OwlDateTimeModule,
    OwlNativeDateTimeModule,
    Icon,
    TranslatePipe,
    PageHeader,
    TableSkeleton,
    TableFooter,
    FilterDropdown,
  ],
  templateUrl: './audit-log.html',
  styles: [
    `
      .audit-backdrop {
        position: fixed;
        inset: 0;
        border: 0;
        padding: 0;
        cursor: pointer;
        background: rgba(0, 0, 0, 0.35);
        z-index: 1045;
      }
      .audit-drawer {
        position: fixed;
        inset-block: 0;
        inset-inline-end: 0;
        inline-size: min(480px, 100vw);
        background: var(--surface);
        box-shadow: -8px 0 24px rgba(0, 0, 0, 0.12);
        z-index: 1046;
        display: flex;
        flex-direction: column;
        overflow-y: auto;
      }
      .diff-old {
        color: #b62f45;
        text-decoration: line-through;
        word-break: break-word;
      }
      .diff-new {
        color: var(--green-strong);
        word-break: break-word;
      }
      .chip-user {
        font-variant-numeric: tabular-nums;
      }
    `,
  ],
})
export class AdminAuditLog {
  private readonly service = inject(AdminAuditService);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService);

  protected readonly term = signal('');
  protected readonly actions = signal<FilterValue[]>([]);
  protected readonly areas = signal<FilterValue[]>([]);
  protected readonly from = signal<Date | null>(null);
  protected readonly to = signal<Date | null>(null);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(DEFAULT_PAGE_SIZE);

  protected readonly selectedId = signal<number | null>(null);

  /** Action values offered by the server (aligned with the server's Action values). */
  private readonly actionKeys = ['Create', 'Update', 'Delete', 'StockOut', 'Login'] as const;

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  private readonly query = computed<AdminAuditQuery>(() => ({
    search: this.term() || undefined,
    actions: this.actions() as string[],
    areas: this.areas() as string[],
    from: dayBoundary(this.from(), false),
    to: dayBoundary(this.to(), true),
    page: this.page(),
    pageSize: this.pageSize(),
  }));

  protected readonly logs = this.service.listResource(this.query);
  protected readonly detail = this.service.getResource(this.selectedId);

  protected readonly rows = computed(() => this.logs.value()?.items ?? []);
  protected readonly total = computed(() => this.logs.value()?.total ?? 0);

  /** Action filter options, re-labelled when the console language switches. */
  protected readonly actionOptions = computed<FilterOption[]>(() => {
    this.language.lang();
    return this.actionKeys.map((value) => ({
      value,
      label: this.translate.instant('audit.actions.' + value),
    }));
  });

  protected readonly hasFilters = computed(
    () =>
      Boolean(this.term()) ||
      this.actions().length > 0 ||
      this.areas().length > 0 ||
      this.from() !== null ||
      this.to() !== null,
  );

  /** Distinct areas present on the loaded page, for the area filter options. */
  protected readonly areaValues = computed(() => {
    const set = new Set<string>();
    for (const row of this.rows()) {
      set.add(row.area);
    }
    return [...set].sort();
  });

  /** Area filter options derived from the loaded rows. */
  protected readonly areaOptions = computed<FilterOption[]>(() =>
    this.areaValues().map((a) => ({ value: a, label: a })),
  );

  /** Union of the changed properties, paired old-vs-new, parsed from the detail JSON. */
  protected readonly diffRows = computed<DiffRow[]>(() => {
    const entry = this.detail.value();
    if (!entry) {
      return [];
    }
    const oldValues = parseJson(entry.oldValuesJson);
    const newValues = parseJson(entry.newValuesJson);
    const keys = [...new Set([...Object.keys(oldValues), ...Object.keys(newValues)])].sort();
    return keys.map((key) => ({
      key,
      oldValue: key in oldValues ? stringify(oldValues[key]) : null,
      newValue: key in newValues ? stringify(newValues[key]) : null,
    }));
  });

  protected badge(action: string): string {
    switch (action) {
      case 'Create':
        return 'text-bg-success';
      case 'Update':
        return 'text-bg-warning';
      case 'Delete':
        return 'text-bg-danger';
      case 'StockOut':
        return 'text-bg-info';
      case 'Login':
        return 'text-bg-secondary';
      default:
        return 'text-bg-light';
    }
  }

  protected onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value.trim();
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
    }
    this.searchTimer = setTimeout(() => {
      this.term.set(value);
      this.page.set(1);
    }, 300);
  }

  protected setActions(values: FilterValue[]): void {
    this.actions.set(values);
    this.page.set(1);
  }

  protected setAreas(values: FilterValue[]): void {
    this.areas.set(values);
    this.page.set(1);
  }

  protected setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }

  protected setFrom(value: Date | null): void {
    this.from.set(value);
    this.page.set(1);
  }

  protected setTo(value: Date | null): void {
    this.to.set(value);
    this.page.set(1);
  }

  protected clearFilters(): void {
    this.term.set('');
    this.actions.set([]);
    this.areas.set([]);
    this.from.set(null);
    this.to.set(null);
    this.page.set(1);
  }

  protected open(id: number): void {
    this.selectedId.set(id);
  }

  protected close(): void {
    this.selectedId.set(null);
  }
}

function parseJson(raw: string | null): Record<string, unknown> {
  if (!raw) {
    return {};
  }
  try {
    const parsed = JSON.parse(raw);
    return parsed && typeof parsed === 'object' ? (parsed as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

function stringify(value: unknown): string {
  if (value === null || value === undefined) {
    return '—';
  }
  if (typeof value === 'object') {
    return JSON.stringify(value);
  }
  return String(value);
}
