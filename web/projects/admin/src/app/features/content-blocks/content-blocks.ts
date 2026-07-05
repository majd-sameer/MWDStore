import { ChangeDetectionStrategy, Component, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { inject } from '@angular/core';
import { AdminContentBlocksService, type AdminContentBlockDto } from 'data-access';
import { TranslatePipe } from '@ngx-translate/core';
import { Icon } from 'ui';
import { PageHeader } from '../../shared/page-header';

interface ContentBlockGroup {
  readonly prefix: string;
  readonly blocks: readonly AdminContentBlockDto[];
}

/** The key segment before the first dot (e.g. `home.hero` -> `home`). */
function prefixOf(key: string): string {
  const dot = key.indexOf('.');
  return dot < 0 ? key : key.slice(0, dot);
}

/**
 * Content blocks browser: the fixed set of homepage blocks (seeded, no create/delete), grouped by
 * key prefix (currently just `home`). Editing happens on its own page (`/content-blocks/:id`).
 */
@Component({
  selector: 'app-admin-content-blocks',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Icon, TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'content_blocks.title' | translate"
      [subtitle]="'content_blocks.subtitle' | translate"
    />

    @if (list.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (list.error()) {
      <div class="alert alert-danger mb-0">{{ 'common.error_api' | translate }}</div>
    } @else {
      @for (group of groups(); track group.prefix) {
        <div class="card border-0 shadow-sm mb-4">
          <div class="card-header bg-transparent border-0 pt-3">
            <h2 class="h6 mb-0 text-uppercase text-body-secondary">{{ group.prefix }}</h2>
          </div>
          <div class="card-body pt-0">
            <div class="table-responsive">
              <table class="table table-hover align-middle mb-0">
                <thead>
                  <tr>
                    <th scope="col">{{ 'content_blocks.col_key' | translate }}</th>
                    <th scope="col">{{ 'content_blocks.col_title' | translate }}</th>
                    <th scope="col" class="text-end">{{ 'content_blocks.col_order' | translate }}</th>
                    <th scope="col">{{ 'common.status' | translate }}</th>
                    <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (b of group.blocks; track b.id) {
                    <tr>
                      <td><code class="small">{{ b.key }}</code></td>
                      <td>
                        <a [routerLink]="['/content-blocks', b.id]" class="text-decoration-none fw-medium">
                          {{ b.title || b.key }}
                        </a>
                      </td>
                      <td class="text-end">{{ b.sortOrder }}</td>
                      <td>
                        @if (b.isPublished) {
                          <span class="badge text-bg-success">{{ 'common.published' | translate }}</span>
                        } @else {
                          <span class="badge text-bg-secondary">{{ 'common.hidden' | translate }}</span>
                        }
                      </td>
                      <td class="text-end">
                        <a
                          [routerLink]="['/content-blocks', b.id]"
                          class="action-btn"
                          [title]="'common.edit' | translate"
                        >
                          <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                        </a>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      } @empty {
        <div class="card border-0 shadow-sm">
          <div class="card-body">
            <div class="empty-state">
              <span class="empty-icon"><lib-icon name="box" [size]="26" /></span>
              <div class="empty-title">{{ 'content_blocks.empty' | translate }}</div>
            </div>
          </div>
        </div>
      }
    }
  `,
})
export class AdminContentBlocks {
  private readonly service = inject(AdminContentBlocksService);

  protected readonly list = this.service.listResource();

  protected readonly groups = computed<ContentBlockGroup[]>(() => {
    const rows = this.list.value() ?? [];
    const byPrefix = new Map<string, AdminContentBlockDto[]>();
    for (const block of rows) {
      const prefix = prefixOf(block.key);
      const bucket = byPrefix.get(prefix);
      if (bucket) {
        bucket.push(block);
      } else {
        byPrefix.set(prefix, [block]);
      }
    }
    return [...byPrefix.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([prefix, blocks]) => ({
        prefix,
        blocks: blocks.sort((a, b) => a.sortOrder - b.sortOrder),
      }));
  });
}
