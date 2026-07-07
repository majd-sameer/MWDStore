import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { AdminAuditService, type AdminAuditQuery } from 'data-access';
import { TranslatePipe } from '@ngx-translate/core';
import { Icon } from 'ui';
import { PageHeader } from '../../shared/page-header';

const PAGE_SIZE = 50;

/** One before/after pair rendered in the detail drawer. */
interface DiffRow {
  readonly key: string;
  readonly oldValue: string | null;
  readonly newValue: string | null;
}

/**
 * Read-only view of the append-only audit trail (`GET /api/admin/audit-logs`,
 * Settings-gated). Server-side filters for action, area, actor and date range;
 * a side drawer renders the changed properties old-vs-new from the stored JSON.
 * The endpoint returns a bare array (no total), so paging advances while a full
 * page comes back — matching the products/orders list convention.
 */
@Component({
  selector: 'app-admin-audit-log',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, Icon, TranslatePipe, PageHeader],
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

  protected readonly term = signal('');
  protected readonly action = signal<string>('');
  protected readonly area = signal<string>('');
  protected readonly from = signal<string>('');
  protected readonly to = signal<string>('');
  protected readonly page = signal(1);

  protected readonly selectedId = signal<number | null>(null);

  /** Action segments offered as filter chips (aligned with the server's Action values). */
  protected readonly actions = ['Create', 'Update', 'Delete', 'StockOut', 'Login'] as const;

  private searchTimer: ReturnType<typeof setTimeout> | null = null;

  private readonly query = computed<AdminAuditQuery>(() => ({
    search: this.term() || undefined,
    action: this.action() || undefined,
    area: this.area() || undefined,
    from: this.from() ? `${this.from()}T00:00:00` : undefined,
    to: this.to() ? `${this.to()}T23:59:59` : undefined,
    page: this.page(),
    pageSize: PAGE_SIZE,
  }));

  protected readonly logs = this.service.listResource(this.query);
  protected readonly detail = this.service.getResource(this.selectedId);

  protected readonly hasMore = computed(
    () => (this.logs.value()?.length ?? 0) === PAGE_SIZE,
  );

  protected readonly hasFilters = computed(
    () =>
      Boolean(this.term()) ||
      this.action() !== '' ||
      this.area() !== '' ||
      this.from() !== '' ||
      this.to() !== '',
  );

  /** Distinct areas present on the loaded page, for the area select. */
  protected readonly areas = computed(() => {
    const set = new Set<string>();
    for (const row of this.logs.value() ?? []) {
      set.add(row.area);
    }
    return [...set].sort();
  });

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

  protected setAction(value: string): void {
    this.action.set(this.action() === value ? '' : value);
    this.page.set(1);
  }

  protected setArea(event: Event): void {
    this.area.set((event.target as HTMLSelectElement).value);
    this.page.set(1);
  }

  protected setFrom(event: Event): void {
    this.from.set((event.target as HTMLInputElement).value);
    this.page.set(1);
  }

  protected setTo(event: Event): void {
    this.to.set((event.target as HTMLInputElement).value);
    this.page.set(1);
  }

  protected clearFilters(): void {
    this.term.set('');
    this.action.set('');
    this.area.set('');
    this.from.set('');
    this.to.set('');
    this.page.set(1);
  }

  protected prev(): void {
    this.page.update((p) => Math.max(1, p - 1));
  }

  protected next(): void {
    if (this.hasMore()) {
      this.page.update((p) => p + 1);
    }
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
