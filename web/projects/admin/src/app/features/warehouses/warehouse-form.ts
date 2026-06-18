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
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/warehouses" class="text-decoration-none">← {{ 'warehouses.title' | translate }}</a>
    </nav>
    <app-page-header [title]="(isNew() ? 'warehouses.new_title' : 'warehouses.edit_title') | translate" />

    @if (!isNew() && list.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (!isNew() && list.error()) {
      <div class="alert alert-danger">{{ 'warehouses.load_one_failed' | translate }}</div>
    } @else {
      <div class="row g-4">
        <div class="col-lg-8">
          <div class="card border-0 shadow-sm">
            <div class="card-body">
              @if (serverError(); as message) {
                <div class="alert alert-danger" role="alert">{{ message }}</div>
              }
              <form (submit)="onSubmit($event)" novalidate>
                <lib-form-field [label]="'common.name' | translate" controlId="wh-name" [required]="true" [error]="err(f.name())">
                  <input id="wh-name" type="text" class="form-control"
                    [class.is-invalid]="!!err(f.name())" [formField]="f.name" />
                </lib-form-field>
                <div class="row">
                  <div class="col-md-6">
                    <lib-form-field [label]="'warehouses.contact_name' | translate" controlId="wh-contact">
                      <input id="wh-contact" type="text" class="form-control" [formField]="f.contactName" />
                    </lib-form-field>
                  </div>
                  <div class="col-md-6">
                    <lib-form-field [label]="'common.phone' | translate" controlId="wh-phone">
                      <input id="wh-phone" type="text" class="form-control" [formField]="f.phone" />
                    </lib-form-field>
                  </div>
                </div>
                <lib-form-field [label]="'warehouses.address1' | translate" controlId="wh-addr1">
                  <input id="wh-addr1" type="text" class="form-control" [formField]="f.addressLine1" />
                </lib-form-field>
                <lib-form-field [label]="'warehouses.address2' | translate" controlId="wh-addr2">
                  <input id="wh-addr2" type="text" class="form-control" [formField]="f.addressLine2" />
                </lib-form-field>
                <div class="row">
                  <div class="col-md-6">
                    <lib-form-field [label]="'common.city' | translate" controlId="wh-city">
                      <input id="wh-city" type="text" class="form-control" [formField]="f.city" />
                    </lib-form-field>
                  </div>
                  <div class="col-md-6">
                    <lib-form-field [label]="'common.zip' | translate" controlId="wh-zip">
                      <input id="wh-zip" type="text" class="form-control" [formField]="f.zipCode" />
                    </lib-form-field>
                  </div>
                </div>
                <lib-form-field [label]="'common.country' | translate" controlId="wh-country" [required]="true"
                  [error]="err(f.countryId())">
                  <select id="wh-country" class="form-select"
                    [class.is-invalid]="!!err(f.countryId())" [formField]="f.countryId"
                    (change)="onCountryChange($any($event.target).value)">
                    <option value="">{{ 'common.choose' | translate }}</option>
                    @for (c of countries.value() ?? []; track c.id) {
                      <option value="{{ c.id }}">{{ c.name }}</option>
                    }
                  </select>
                </lib-form-field>
                <lib-form-field [label]="'common.state' | translate" controlId="wh-state" [required]="true"
                  [error]="err(f.stateOrProvinceId())">
                  <select id="wh-state" class="form-select"
                    [class.is-invalid]="!!err(f.stateOrProvinceId())" [formField]="f.stateOrProvinceId">
                    <option value="">{{ 'common.choose' | translate }}</option>
                    @for (s of states(); track s.id) {
                      <option value="{{ s.id }}">{{ s.name }}</option>
                    }
                  </select>
                </lib-form-field>

                <div class="form-actions">
                  <button libButton variant="primary" [disabled]="f().submitting()">
                    {{ (f().submitting() ? 'common.saving' : isNew() ? 'warehouses.create' : 'common.save_changes') | translate }}
                  </button>
                  <a routerLink="/warehouses" class="btn btn-outline-secondary">{{ 'common.cancel' | translate }}</a>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    }
  `,
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
