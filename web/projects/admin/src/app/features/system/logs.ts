import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { AdminOperationsService } from 'data-access';
import { TranslatePipe } from '@ngx-translate/core';
import { PageHeader } from '../../shared/page-header';

/** Read-only system logs: activity log + most-searched queries (old ActivityLog/Search admin pages). */
@Component({
  selector: 'app-admin-logs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, TranslatePipe, PageHeader],
  templateUrl: './logs.html',
})
export class AdminLogs {
  private readonly service = inject(AdminOperationsService);

  protected readonly tab = signal<'activity' | 'search'>('activity');
  protected readonly activities = this.service.activitiesResource();
  protected readonly queries = this.service.searchQueriesResource();
}
