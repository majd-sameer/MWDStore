import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import {
  email,
  form,
  FormField as Control,
  required,
  submit,
} from '@angular/forms/signals';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  StorefrontFeaturesService,
  type ContactAreaPublicDto,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { Button, ToastService } from 'ui';

interface ContactModel {
  fullName: string;
  emailAddress: string;
  phoneNumber: string;
  contactAreaId: string;
  content: string;
}

function emptyModel(): ContactModel {
  return { fullName: '', emailAddress: '', phoneNumber: '', contactAreaId: '', content: '' };
}

/** Contact-us form (old Contacts module): topic select + message, lands in the admin inbox. */
@Component({
  selector: 'app-contact',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, TranslatePipe, Button],
  templateUrl: './contact.html',
  styleUrl: './contact.scss',
})
export class Contact {
  private readonly service = inject(StorefrontFeaturesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly areas = signal<ContactAreaPublicDto[]>([]);
  protected readonly sent = signal(false);

  protected readonly model = signal<ContactModel>(emptyModel());
  protected readonly f = form(this.model, (path) => {
    required(path.fullName);
    required(path.emailAddress);
    email(path.emailAddress);
    required(path.contactAreaId);
    required(path.content);
  });

  constructor() {
    this.service.contactAreas().subscribe({
      next: (areas) => this.areas.set(areas),
      error: () => this.areas.set([]),
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      const m = this.model();
      try {
        await firstValueFrom(
          this.service.submitContact({
            fullName: m.fullName,
            emailAddress: m.emailAddress,
            phoneNumber: m.phoneNumber || null,
            content: m.content,
            contactAreaId: Number(m.contactAreaId),
          }),
        );
        this.sent.set(true);
      } catch {
        this.toast.error(this.translate.instant('contact.error'));
      }
      return undefined;
    });
  }
}
