import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  email,
  form,
  FormField as Control,
  required,
  submit,
} from '@angular/forms/signals';
import { DomSanitizer, type SafeResourceUrl } from '@angular/platform-browser';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  StorefrontFeaturesService,
  type ContactAreaPublicDto,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { Breadcrumb, Button, FormField, Icon, type IconName } from 'ui';
import { FooterContentStore } from '../../core/footer-content.store';
import { firstError } from '../../shared/field-error';

interface ContactModel {
  fullName: string;
  emailAddress: string;
  phoneNumber: string;
  contactAreaId: string;
  content: string;
}

/** A social channel; its URL comes from the `footer-social` CMS block (the same
 *  admin-managed section the footer reads), so it renders only when configured. */
interface SocialLink {
  readonly key: string;
  readonly icon: IconName;
  readonly label: string;
}

function emptyModel(): ContactModel {
  return { fullName: '', emailAddress: '', phoneNumber: '', contactAreaId: '', content: '' };
}

/**
 * Contact-us page (old Contacts module). Redesigned as a two-column layout that
 * mirrors the storefront chrome: a full-bleed ivory page-header band with
 * breadcrumbs, then a "reach us" info column (admin-managed social links,
 * working hours and an embedded Google map) beside a carded message form.
 * Submissions land in the admin inbox. Copy is keyed (ar/en) and layout uses
 * logical properties so RTL mirrors.
 */
@Component({
  selector: 'app-contact',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, TranslatePipe, Breadcrumb, Button, FormField, Icon],
  templateUrl: './contact.html',
  styleUrl: './contact.scss',
})
export class Contact {
  private readonly service = inject(StorefrontFeaturesService);
  private readonly translate = inject(TranslateService);
  private readonly sanitizer = inject(DomSanitizer);
  protected readonly cms = inject(FooterContentStore);

  protected readonly areas = signal<ContactAreaPublicDto[]>([]);
  protected readonly sent = signal(false);
  protected readonly sendError = signal<string | null>(null);

  protected readonly model = signal<ContactModel>(emptyModel());
  // Messages are i18n keys, resolved reactively by the `translate` pipe in the
  // template — so they re-translate live when the language is switched.
  protected readonly f = form(this.model, (path) => {
    required(path.fullName, { message: 'contact.err_name' });
    required(path.emailAddress, { message: 'contact.err_email' });
    email(path.emailAddress, { message: 'contact.err_email_invalid' });
    required(path.contactAreaId, { message: 'contact.err_area' });
    required(path.content, { message: 'contact.err_message' });
  });

  protected readonly nameError = computed(() => firstError(this.f.fullName()));
  protected readonly emailError = computed(() => firstError(this.f.emailAddress()));
  protected readonly areaError = computed(() => firstError(this.f.contactAreaId()));
  protected readonly messageError = computed(() => firstError(this.f.content()));

  /** Same fixed platforms the footer offers; each renders only when its
   *  `footer-social` block carries a URL an admin has set. */
  protected readonly socials: readonly SocialLink[] = [
    { key: 'facebook', icon: 'facebook', label: 'Facebook' },
    { key: 'instagram', icon: 'instagram', label: 'Instagram' },
    { key: 'twitter', icon: 'twitter', label: 'X (Twitter)' },
    { key: 'youtube', icon: 'youtube', label: 'YouTube' },
    { key: 'tiktok', icon: 'tiktok', label: 'TikTok' },
    { key: 'whatsapp', icon: 'whatsapp', label: 'WhatsApp' },
  ];

  protected readonly hasSocial = computed(() =>
    this.socials.some((s) => !!this.cms.block('footer-social', s.key)?.linkUrl),
  );

  protected socialUrl(key: string): string | null {
    return this.cms.block('footer-social', key)?.linkUrl ?? null;
  }

  /** Google-maps embed for the address. The place query lives in i18n
   *  (`contact.map_query`) so the client can retarget it without a code change;
   *  the URL is sanitized as a trusted resource so Angular allows the iframe. */
  private readonly mapQuery = toSignal(this.translate.stream('contact.map_query'));
  protected readonly mapUrl = computed<SafeResourceUrl | null>(() => {
    const query = this.mapQuery();
    if (!query || query === 'contact.map_query') {
      return null;
    }
    const url = `https://maps.google.com/maps?q=${encodeURIComponent(query)}&z=14&output=embed`;
    return this.sanitizer.bypassSecurityTrustResourceUrl(url);
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
      this.sendError.set(null);
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
        this.sendError.set('contact.error');
      }
      return undefined;
    });
  }

  /** Clears the confirmation so the visitor can compose another message. */
  protected reset(): void {
    this.model.set(emptyModel());
    this.sendError.set(null);
    this.sent.set(false);
  }
}
