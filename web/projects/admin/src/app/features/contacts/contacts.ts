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
  templateUrl: './contacts.html',
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
