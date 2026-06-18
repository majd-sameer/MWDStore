import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  AdminProductAttributesService,
  type AdminProductAttributeDto,
  type AdminProductAttributeGroupDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Product attribute browser: the attribute list with a small group manager
 * alongside. Creating and editing an attribute happen on their own page
 * (`/product-attributes/new`, `/product-attributes/:id`); groups stay here
 * since they are a lightweight secondary entity.
 */
@Component({
  selector: 'app-admin-product-attributes',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Button, Icon, TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'attributes.title' | translate"
      [subtitle]="'attributes.subtitle' | translate"
    >
      <a routerLink="/product-attributes/new" class="btn btn-primary d-inline-flex align-items-center gap-1">
        <lib-icon name="plus" [size]="18" /> {{ 'attributes.new' | translate }}
      </a>
    </app-page-header>

    <div class="row g-4">
      <div class="col-lg-8">
        <div class="card border-0 shadow-sm">
          <div class="card-body">
            @if (list.isLoading()) {
              <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                  <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
                </div>
              </div>
            } @else if (list.error()) {
              <div class="alert alert-danger mb-0">{{ 'common.error_api' | translate }}</div>
            } @else if (list.value(); as rows) {
              <div class="table-responsive">
                <table class="table table-hover align-middle mb-0">
                  <thead>
                    <tr>
                      <th scope="col">{{ 'common.name' | translate }}</th>
                      <th scope="col">{{ 'attributes.col_group' | translate }}</th>
                      <th scope="col" class="text-end">{{ 'common.actions' | translate }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (a of rows; track a.id) {
                      <tr>
                        <td>
                          <a [routerLink]="['/product-attributes', a.id]" class="text-decoration-none fw-medium">{{ a.name }}</a>
                        </td>
                        <td class="text-body-secondary">{{ a.groupName }}</td>
                        <td class="text-end">
                          <span class="d-inline-flex gap-1">
                            <a [routerLink]="['/product-attributes', a.id]" class="action-btn" [title]="'common.edit' | translate">
                              <lib-icon name="pencil" [size]="15" [label]="'common.edit' | translate" />
                            </a>
                            <button
                              type="button"
                              class="action-btn action-btn-danger"
                              [title]="'common.delete' | translate"
                              [disabled]="deletingId() === a.id"
                              (click)="remove(a)"
                            >
                              <lib-icon name="trash" [size]="15" [label]="'common.delete' | translate" />
                            </button>
                          </span>
                        </td>
                      </tr>
                    } @empty {
                      <tr>
                        <td colspan="3">
                          <div class="empty-state">
                            <span class="empty-icon"><lib-icon name="box" [size]="26" /></span>
                            <div class="empty-title">{{ 'attributes.empty' | translate }}</div>
                            <a routerLink="/product-attributes/new" class="btn btn-primary btn-sm mt-2">
                              {{ 'attributes.create_first' | translate }}
                            </a>
                          </div>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            }
          </div>
        </div>
      </div>

      <div class="col-lg-4">
        <div class="card border-0 shadow-sm">
          <div class="card-header bg-body fw-semibold">{{ 'attributes.groups_title' | translate }}</div>
          <div class="card-body">
            @for (g of groups.value() ?? []; track g.id) {
              <div class="d-flex align-items-center justify-content-between border rounded px-2 py-1 mb-1">
                <span class="small">{{ g.name }}</span>
                <button type="button" class="btn-close" style="font-size: 0.6rem"
                  (click)="removeGroup(g)" [attr.aria-label]="'attributes.delete_group' | translate"></button>
              </div>
            } @empty {
              <p class="text-body-secondary small">{{ 'attributes.no_groups' | translate }}</p>
            }
            <div class="d-flex gap-2 mt-3">
              <input type="text" class="form-control form-control-sm"
                [placeholder]="'attributes.new_group_ph' | translate" #groupName />
              <button type="button" libButton variant="secondary" [outline]="true"
                (click)="addGroup(groupName); groupName.value = ''">
                {{ 'common.add' | translate }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class AdminProductAttributes {
  private readonly service = inject(AdminProductAttributesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource();
  protected readonly groups = this.service.groupsResource();
  protected readonly deletingId = signal<number | null>(null);

  protected remove(a: AdminProductAttributeDto): void {
    if (!confirm(this.translate.instant('attributes.confirm_delete', { name: a.name ?? '#' + a.id }))) {
      return;
    }
    this.deletingId.set(a.id);
    this.service.delete(a.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('attributes.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('attributes.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }

  protected addGroup(input: HTMLInputElement): void {
    const name = input.value.trim();
    if (!name) {
      return;
    }
    this.service.createGroup({ name }).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('attributes.group_created'));
        this.groups.reload();
      },
      error: () => this.toast.error(this.translate.instant('attributes.group_create_failed')),
    });
  }

  protected removeGroup(g: AdminProductAttributeGroupDto): void {
    if (!confirm(this.translate.instant('attributes.confirm_delete_group', { name: g.name ?? '' }))) {
      return;
    }
    this.service.deleteGroup(g.id).subscribe({
      next: () => {
        this.groups.reload();
      },
      error: () =>
        this.toast.error(this.translate.instant('attributes.group_delete_failed')),
    });
  }
}
