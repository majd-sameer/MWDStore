import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { AdminOperationsService } from 'data-access';
import { TranslatePipe } from '@ngx-translate/core';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';
import { TableFooter } from '../../shared/table-footer';

/** Read-only system logs: activity log + most-searched queries (old ActivityLog/Search admin pages). */
@Component({
  selector: 'app-admin-logs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, TranslatePipe, PageHeader, TableSkeleton, TableFooter],
  templateUrl: './logs.html',
})
export class AdminLogs {
  private readonly service = inject(AdminOperationsService);

  protected readonly tab = signal<'activity' | 'search'>('activity');

  protected readonly page = signal(1);
  protected readonly pageSize = signal(50);

  private readonly activityQuery = computed(() => ({
    page: this.page(),
    pageSize: this.pageSize(),
  }));

  protected readonly activities = this.service.activitiesResource(this.activityQuery);
  protected readonly queries = this.service.searchQueriesResource();

  protected readonly activityRows = computed(() => this.activities.value()?.items ?? []);
  protected readonly activitiesTotal = computed(() => this.activities.value()?.total ?? 0);

  protected setPageSize(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }
}
