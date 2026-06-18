import {
  ChangeDetectionStrategy,
  Component,
  inject,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import {
  AdminOperationsService,
  type AdminContactAreaDto,
  type AdminContactDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/** Contact submissions inbox + contact-area management (old Contacts module). */
@Component({
  selector: 'app-admin-contacts',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, DatePipe, TranslatePipe, PageHeader],
  template: `
    <app-page-header
      [title]="'contacts.title' | translate"
      [subtitle]="'contacts.subtitle' | translate"
    />

    <div class="row g-4">
      <div class="col-lg-8">
        <div class="card border-0 shadow-sm">
          <div class="card-header bg-body fw-semibold">{{ 'contacts.submissions' | translate }}</div>
          <div class="card-body">
            @if (list.isLoading()) {
              <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                  <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
                </div>
              </div>
            } @else {
              @for (c of list.value() ?? []; track c.id) {
                <div class="border rounded p-3 mb-2">
                  <div class="d-flex justify-content-between align-items-start">
                    <div>
                      <span class="fw-medium">{{ c.fullName }}</span>
                      <span class="badge text-bg-light border ms-2">{{ c.contactAreaName }}</span>
                      <div class="small text-body-secondary">
                        {{ c.emailAddress }}@if (c.phoneNumber) { · {{ c.phoneNumber }}}
                      </div>
                    </div>
                    <div class="text-end">
                      <div class="small text-body-secondary">{{ c.createdOn | date: 'medium' }}</div>
                      <button type="button" class="btn btn-sm btn-outline-danger mt-1"
                        (click)="remove(c)">{{ 'common.delete' | translate }}</button>
                    </div>
                  </div>
                  <p class="mb-0 mt-2 small">{{ c.content }}</p>
                </div>
              } @empty {
                <p class="text-body-secondary mb-0">{{ 'contacts.no_submissions' | translate }}</p>
              }
            }
          </div>
        </div>
      </div>

      <div class="col-lg-4">
        <div class="card border-0 shadow-sm">
          <div class="card-header bg-body fw-semibold">{{ 'contacts.areas_title' | translate }}</div>
          <div class="card-body">
            @for (a of areas.value() ?? []; track a.id) {
              <div class="d-flex align-items-center gap-2 mb-2">
                <input type="text" class="form-control form-control-sm" [value]="a.name"
                  (change)="renameArea(a, $any($event.target).value)" />
                <button type="button" class="btn btn-sm btn-outline-danger"
                  (click)="removeArea(a)">✕</button>
              </div>
            } @empty {
              <p class="text-body-secondary small">{{ 'contacts.no_areas' | translate }}</p>
            }
            <div class="d-flex gap-2 mt-3">
              <input type="text" class="form-control form-control-sm"
                [placeholder]="'contacts.new_area_ph' | translate" #areaName />
              <button type="button" libButton variant="secondary" [outline]="true"
                (click)="addArea(areaName)">
                {{ 'common.add' | translate }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class AdminContacts {
  private readonly service = inject(AdminOperationsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.contactsResource();
  protected readonly areas = this.service.contactAreasResource();

  protected remove(c: AdminContactDto): void {
    if (!confirm(this.translate.instant('contacts.confirm_delete_submission'))) {
      return;
    }
    this.service.deleteContact(c.id).subscribe({
      next: () => this.list.reload(),
      error: () => this.toast.error(this.translate.instant('contacts.delete_submission_failed')),
    });
  }

  protected addArea(input: HTMLInputElement): void {
    const name = input.value.trim();
    if (!name) {
      return;
    }
    this.service.createContactArea({ name }).subscribe({
      next: () => {
        input.value = '';
        this.areas.reload();
      },
      error: () => this.toast.error(this.translate.instant('contacts.area_create_failed')),
    });
  }

  protected renameArea(a: AdminContactAreaDto, name: string): void {
    const trimmed = name.trim();
    if (!trimmed) {
      return;
    }
    this.service.updateContactArea(a.id, { name: trimmed }).subscribe({
      next: () => this.toast.success(this.translate.instant('contacts.area_updated')),
      error: () => this.toast.error(this.translate.instant('contacts.area_update_failed')),
    });
  }

  protected removeArea(a: AdminContactAreaDto): void {
    if (!confirm(this.translate.instant('contacts.confirm_delete_area', { name: a.name ?? '' }))) {
      return;
    }
    this.service.deleteContactArea(a.id).subscribe({
      next: () => this.areas.reload(),
      error: () => this.toast.error(this.translate.instant('contacts.area_delete_failed')),
    });
  }
}
