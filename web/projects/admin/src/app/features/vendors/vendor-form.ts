import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  form,
  FormField as Control,
  required,
  submit,
} from '@angular/forms/signals';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  AdminOperationsService,
  type VendorUpsertRequest,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';

interface VendorModel {
  name: string;
  slug: string;
  email: string;
  description: string;
  isActive: boolean;
}

function emptyModel(): VendorModel {
  return { name: '', slug: '', email: '', description: '', isActive: true };
}

/**
 * Create / edit a vendor on its own page (mirrors the product form). The vendor
 * API has no single-fetch endpoint, so edit mode seeds from the list resource
 * (the list DTO already carries every editable field). Saving returns to the list.
 */
@Component({
  selector: 'app-admin-vendor-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader],
  templateUrl: './vendor-form.html',
})
export class AdminVendorForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminOperationsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly vendorId = computed(() => Number(this.idParam().get('id')));

  protected readonly list = this.service.vendorsResource();
  private readonly existing = computed(
    () => this.list.value()?.find((v) => v.id === this.vendorId()) ?? null,
  );

  protected readonly model = signal<VendorModel>(emptyModel());
  protected readonly f = form(this.model, (path) => {
    required(path.name, { message: 'Name is required' });
  });
  protected readonly err = firstError;
  protected readonly serverError = signal<string | null>(null);

  private seeded = false;

  constructor() {
    effect(() => {
      if (this.isNew() || this.seeded) {
        return;
      }
      const v = this.existing();
      if (!v) {
        return;
      }
      this.seeded = true;
      this.model.set({
        name: v.name ?? '',
        slug: v.slug ?? '',
        email: v.email ?? '',
        description: v.description ?? '',
        isActive: v.isActive,
      });
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      const body: VendorUpsertRequest = {
        name: m.name,
        slug: m.slug || null,
        email: m.email || null,
        description: m.description || null,
        isActive: m.isActive,
      };
      try {
        if (this.isNew()) {
          await firstValueFrom(this.service.createVendor(body));
          this.toast.success(this.translate.instant('vendors.created_ok'));
        } else {
          await firstValueFrom(this.service.updateVendor(this.vendorId(), body));
          this.toast.success(this.translate.instant('vendors.updated_ok'));
        }
        await this.router.navigate(['/vendors']);
      } catch {
        this.serverError.set(this.translate.instant('vendors.save_failed'));
      }
      return undefined;
    });
  }
}
