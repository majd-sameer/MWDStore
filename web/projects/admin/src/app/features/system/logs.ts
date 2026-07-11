import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { AdminOperationsService } from 'data-access';
import { TranslatePipe } from '@ngx-translate/core';
import { TableCards } from 'ui';
import { PageHeader } from '../../shared/page-header';

/** Read-only system logs: activity log + most-searched queries (old ActivityLog/Search admin pages). */
@Component({
  selector: 'app-admin-logs',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, TranslatePipe, PageHeader, TableCards],
  template: `
    <app-page-header
      [title]="'logs.title' | translate"
      [subtitle]="'logs.subtitle' | translate"
    />

    <ul class="nav nav-tabs mb-3">
      <li class="nav-item">
        <button type="button" class="nav-link" [class.active]="tab() === 'activity'"
          (click)="tab.set('activity')">
          {{ 'logs.tab_activity' | translate }}
        </button>
      </li>
      <li class="nav-item">
        <button type="button" class="nav-link" [class.active]="tab() === 'search'"
          (click)="tab.set('search')">
          {{ 'logs.tab_search' | translate }}
        </button>
      </li>
    </ul>

    @if (tab() === 'activity') {
      <div class="card border-0 shadow-sm">
        <div class="card-body">
          <table class="table table-sm table-hover align-middle mb-0" libTableCards>
            <thead>
              <tr>
                <th>#</th>
                <th>{{ 'logs.col_type' | translate }}</th>
                <th>{{ 'logs.col_entity' | translate }}</th>
                <th>{{ 'logs.col_user' | translate }}</th>
                <th>{{ 'common.when' | translate }}</th>
              </tr>
            </thead>
            <tbody>
              @for (a of activities.value() ?? []; track a.id) {
                <tr>
                  <td>{{ a.id }}</td>
                  <td>{{ a.activityTypeName }}</td>
                  <td>{{ a.entityTypeId }} #{{ a.entityId }}</td>
                  <td>{{ a.userId }}</td>
                  <td class="small">{{ a.createdOn | date: 'medium' }}</td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="5" class="text-center text-body-secondary py-4">
                    {{ 'logs.no_activity' | translate }}
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    } @else {
      <div class="card border-0 shadow-sm">
        <div class="card-body">
          <table class="table table-sm table-hover align-middle mb-0" libTableCards>
            <thead>
              <tr>
                <th>{{ 'logs.col_query' | translate }}</th>
                <th class="text-end">{{ 'logs.col_count' | translate }}</th>
                <th>{{ 'logs.col_latest' | translate }}</th>
              </tr>
            </thead>
            <tbody>
              @for (q of queries.value() ?? []; track q.queryText) {
                <tr>
                  <td class="fw-medium">{{ q.queryText }}</td>
                  <td class="text-end">{{ q.count }}</td>
                  <td class="small">{{ q.latestCreatedOn | date: 'medium' }}</td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="3" class="text-center text-body-secondary py-4">
                    {{ 'logs.no_searches' | translate }}
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    }
  `,
})
export class AdminLogs {
  private readonly service = inject(AdminOperationsService);

  protected readonly tab = signal<'activity' | 'search'>('activity');
  protected readonly activities = this.service.activitiesResource();
  protected readonly queries = this.service.searchQueriesResource();
}
