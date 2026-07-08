import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { Icon } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { StatusPill, type StatusTone } from '../../shared/status-pill';
import { AvatarCell } from '../../shared/avatar-cell';
import { TableSkeleton } from '../../shared/table-skeleton';
import { TableFooter } from '../../shared/table-footer';
import { FilterDropdown, type FilterOption, type FilterValue } from '../../shared/filter-dropdown';

interface DemoRow {
  requestId: string;
  requestType: string;
  policyholder: string;
  policyholderEmail: string;
  vehicleOwner: string;
  bidAmount: number | null;
  bidStatus: { label: string; tone: StatusTone };
  requestStatus: { label: string; tone: StatusTone };
  timeLeft: string | null;
}

const ROWS: DemoRow[] = [
  {
    requestId: 'RQ-100482', requestType: 'Repair', policyholder: 'Layla Haddad',
    policyholderEmail: 'layla.haddad@example.com', vehicleOwner: 'Layla Haddad', bidAmount: 1240,
    bidStatus: { label: 'Open Bid', tone: 'info' }, requestStatus: { label: 'Pending', tone: 'warning' },
    timeLeft: '2h 15m',
  },
  {
    requestId: 'RQ-100479', requestType: 'Total Loss', policyholder: 'Omar Nasser',
    policyholderEmail: 'omar.nasser@example.com', vehicleOwner: 'Nasser Trading Co.', bidAmount: 8600,
    bidStatus: { label: 'Closed Bid', tone: 'neutral' }, requestStatus: { label: 'Resolved', tone: 'success' },
    timeLeft: null,
  },
  {
    requestId: 'RQ-100475', requestType: 'Repair', policyholder: 'Sara Khalil',
    policyholderEmail: 'sara.khalil@example.com', vehicleOwner: 'Sara Khalil', bidAmount: null,
    bidStatus: { label: 'Pending', tone: 'warning' }, requestStatus: { label: 'Pending', tone: 'warning' },
    timeLeft: '18h 40m',
  },
  {
    requestId: 'RQ-100470', requestType: 'Inspection', policyholder: 'Yousef Odeh',
    policyholderEmail: 'yousef.odeh@example.com', vehicleOwner: 'Yousef Odeh', bidAmount: 320,
    bidStatus: { label: 'Open Bid', tone: 'info' }, requestStatus: { label: 'In Review', tone: 'info' },
    timeLeft: '45m',
  },
  {
    requestId: 'RQ-100461', requestType: 'Total Loss', policyholder: 'Dana Mansour',
    policyholderEmail: 'dana.mansour@example.com', vehicleOwner: 'Mansour Holdings', bidAmount: 5400,
    bidStatus: { label: 'Closed Bid', tone: 'neutral' }, requestStatus: { label: 'Rejected', tone: 'danger' },
    timeLeft: null,
  },
];

type DemoState = 'populated' | 'skeleton' | 'empty';

/**
 * Living reference for the shared data-table components (spec §6). Demonstrates
 * FilterDropdown, StatusPill, AvatarCell, TableSkeleton and TableFooter wired
 * together, and lets you flip between populated / skeleton / empty states.
 * Reachable at /design/tables (no sidebar link).
 */
@Component({
  selector: 'app-table-showcase',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, PageHeader, StatusPill, AvatarCell, TableSkeleton, TableFooter, FilterDropdown],
  templateUrl: './table-showcase.html',
})
export class TableShowcase {
  protected readonly state = signal<DemoState>('populated');
  protected setState(s: DemoState): void {
    this.state.set(s);
  }

  protected readonly page = signal(1);
  protected readonly pageSize = signal(15);

  // Multi-select filter demo -------------------------------------------------
  protected readonly bidStatusOptions: FilterOption[] = [
    { value: 'pending', label: 'Pending' },
    { value: 'open', label: 'Open Bid' },
    { value: 'closed', label: 'Closed Bid' },
  ];
  protected readonly requestTypeOptions: FilterOption[] = [
    { value: 'repair', label: 'Repair' },
    { value: 'total_loss', label: 'Total Loss' },
    { value: 'inspection', label: 'Inspection' },
  ];
  protected readonly bidStatus = signal<FilterValue[]>([]);
  protected readonly requestType = signal<FilterValue[]>(['repair']);

  protected readonly hasFilters = computed(
    () => this.bidStatus().length > 0 || this.requestType().length > 0,
  );
  protected clearFilters(): void {
    this.bidStatus.set([]);
    this.requestType.set([]);
  }

  protected readonly rows = ROWS;
  protected readonly skeletonColumns = [2, 2, 3, 2, 2, 2, 2, 1];
}
