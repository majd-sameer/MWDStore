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
import { NgSelectModule } from '@ng-select/ng-select';
import {
  AdminLocationsService,
  AdminWarehousesService,
  type StateOrProvinceLookupDto,
  type WarehouseUpsertRequest,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';

interface WarehouseModel {
  name: string;
  contactName: string;
  phone: string;
  addressLine1: string;
  addressLine2: string;
  city: string;
  zipCode: string;
  countryId: string;
  stateOrProvinceId: string;
}

function emptyModel(): WarehouseModel {
  return {
    name: '',
    contactName: '',
    phone: '',
    addressLine1: '',
    addressLine2: '',
    city: '',
    zipCode: '',
    countryId: '',
    stateOrProvinceId: '',
  };
}

/**
 * Create / edit a warehouse on its own page (mirrors the product form). The
 * warehouse API has no single-fetch endpoint, so edit mode seeds from the list
 * resource (the list DTO carries every editable field). Saving returns to the list.
 */
@Component({
  selector: 'app-admin-warehouse-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader, NgSelectModule],
  templateUrl: './warehouse-form.html',
})
export class AdminWarehouseForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminWarehousesService);
  private readonly locations = inject(AdminLocationsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly warehouseId = computed(() => Number(this.idParam().get('id')));

  protected readonly list = this.service.listResource();
  protected readonly countries = this.locations.countriesResource();
  protected readonly states = signal<StateOrProvinceLookupDto[]>([]);
  protected readonly stateItems = computed(() =>
    this.states().map((s) => ({ id: String(s.id), name: s.name })),
  );

  private readonly existing = computed(
    () => this.list.value()?.find((w) => w.id === this.warehouseId()) ?? null,
  );

  protected readonly model = signal<WarehouseModel>(emptyModel());
  protected readonly f = form(this.model, (path) => {
    required(path.name, { message: 'Name is required' });
    required(path.countryId, { message: 'Country is required' });
    required(path.stateOrProvinceId, { message: 'State is required' });
  });
  protected readonly err = firstError;
  protected readonly serverError = signal<string | null>(null);

  private seeded = false;

  constructor() {
    effect(() => {
      if (this.isNew() || this.seeded) {
        return;
      }
      const w = this.existing();
      if (!w) {
        return;
      }
      this.seeded = true;
      this.model.set({
        name: w.name ?? '',
        contactName: w.contactName ?? '',
        phone: w.phone ?? '',
        addressLine1: w.addressLine1 ?? '',
        addressLine2: w.addressLine2 ?? '',
        city: w.city ?? '',
        zipCode: w.zipCode ?? '',
        countryId: w.countryId,
        stateOrProvinceId: String(w.stateOrProvinceId),
      });
      this.locations.states(w.countryId).subscribe({
        next: (states) => this.states.set(states),
      });
    });
  }

  protected onCountryChange(countryId: string): void {
    this.model.update((m) => ({ ...m, stateOrProvinceId: '' }));
    this.states.set([]);
    if (countryId) {
      this.locations.states(countryId).subscribe({
        next: (states) => this.states.set(states),
        error: () => this.states.set([]),
      });
    }
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      const body: WarehouseUpsertRequest = {
        name: m.name,
        contactName: m.contactName || null,
        phone: m.phone || null,
        addressLine1: m.addressLine1 || null,
        addressLine2: m.addressLine2 || null,
        city: m.city || null,
        zipCode: m.zipCode || null,
        countryId: m.countryId,
        stateOrProvinceId: Number(m.stateOrProvinceId),
      };
      try {
        if (this.isNew()) {
          await firstValueFrom(this.service.create(body));
          this.toast.success(this.translate.instant('warehouses.created_ok'));
        } else {
          await firstValueFrom(this.service.update(this.warehouseId(), body));
          this.toast.success(this.translate.instant('warehouses.updated_ok'));
        }
        await this.router.navigate(['/warehouses']);
      } catch {
        this.serverError.set(this.translate.instant('warehouses.save_failed'));
      }
      return undefined;
    });
  }
}
