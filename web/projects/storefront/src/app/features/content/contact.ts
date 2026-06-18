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
  template: `
    <div class="container py-4 contact">
      <h1 class="page-title">{{ 'contact.title' | translate }}</h1>
      <p class="text-body-secondary">{{ 'contact.subtitle' | translate }}</p>

      @if (sent()) {
        <div class="alert alert-success">{{ 'contact.sent' | translate }}</div>
      } @else {
        <form (submit)="onSubmit($event)" novalidate>
          <div class="row g-3">
            <div class="col-md-6">
              <label class="form-label" for="ct-name">{{ 'contact.name' | translate }}</label>
              <input id="ct-name" type="text" class="form-control" [formField]="f.fullName" />
            </div>
            <div class="col-md-6">
              <label class="form-label" for="ct-email">{{ 'contact.email' | translate }}</label>
              <input id="ct-email" type="email" class="form-control" [formField]="f.emailAddress" />
            </div>
            <div class="col-md-6">
              <label class="form-label" for="ct-phone">{{ 'contact.phone' | translate }}</label>
              <input id="ct-phone" type="text" class="form-control" [formField]="f.phoneNumber" />
            </div>
            <div class="col-md-6">
              <label class="form-label" for="ct-area">{{ 'contact.area' | translate }}</label>
              <select id="ct-area" class="form-select" [formField]="f.contactAreaId">
                <option value="">—</option>
                @for (a of areas(); track a.id) {
                  <option value="{{ a.id }}">{{ a.name }}</option>
                }
              </select>
            </div>
            <div class="col-12">
              <label class="form-label" for="ct-message">{{ 'contact.message' | translate }}</label>
              <textarea id="ct-message" rows="5" class="form-control" [formField]="f.content"></textarea>
            </div>
            <div class="col-12">
              <button libButton variant="dark" [disabled]="f().submitting()">
                {{ 'contact.send' | translate }}
              </button>
            </div>
          </div>
        </form>
      }
    </div>
  `,
  styles: `
    .contact {
      max-inline-size: 44rem;
    }
    .page-title {
      font-size: 1.8rem;
      font-weight: 700;
      letter-spacing: -0.02em;
      margin-block-end: 0.25rem;
    }
  `,
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
