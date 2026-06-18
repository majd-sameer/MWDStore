import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  AdminLocationsService,
  AdminShippingService,
  type StateOrProvinceLookupDto,
  type TableRateUpsertRequest,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

interface TableRateFormModel {
  shippingProviderId: string;
  countryId: string;
  stateOrProvinceId: string;
  zipCode: string;
  minOrderSubtotal: string;
  shippingPrice: string;
  note: string;
}

function emptyModel(): TableRateFormModel {
  return {
    shippingProviderId: '',
    countryId: '',
    stateOrProvinceId: '',
    zipCode: '',
    minOrderSubtotal: '0',
    shippingPrice: '0',
    note: '',
  };
}

/**
 * Create / edit a shipping table rate on its own page. The shipping API has no
 * single-fetch endpoint for a rate, so edit mode seeds from the table-rates list
 * resource. Providers and the free-shipping threshold are managed on the list page.
 */
@Component({
  selector: 'app-admin-table-rate-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/shipping" class="text-decoration-none">← {{ 'shipping.title' | translate }}</a>
    </nav>
    <app-page-header [title]="(isNew() ? 'shipping.new_title' : 'shipping.edit_title') | translate" />

    @if (!isNew() && rates.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (!isNew() && rates.error()) {
      <div class="alert alert-danger">{{ 'shipping.load_one_failed' | translate }}</div>
    } @else {
      <div class="row g-4">
        <div class="col-lg-7">
          <div class="card border-0 shadow-sm">
            <div class="card-body">
              <div class="mb-3">
                <label class="form-label" for="tr-provider">{{ 'shipping.col_provider' | translate }}</label>
                <select id="tr-provider" class="form-select"
                  [value]="model().shippingProviderId"
                  (change)="patch({ shippingProviderId: $any($event.target).value })">
                  <option value="">{{ 'shipping.choose_provider' | translate }}</option>
                  @for (p of carriers(); track p.id) {
                    <option value="{{ p.id }}">{{ p.name }}</option>
                  }
                </select>
              </div>
              <div class="row">
                <div class="col-md-6 mb-3">
                  <label class="form-label" for="tr-country">{{ 'common.country' | translate }}</label>
                  <select id="tr-country" class="form-select"
                    [value]="model().countryId"
                    (change)="onCountryChange($any($event.target).value)">
                    <option value="">{{ 'common.any' | translate }}</option>
                    @for (c of countries.value() ?? []; track c.id) {
                      <option value="{{ c.id }}">{{ c.name }}</option>
                    }
                  </select>
                </div>
                <div class="col-md-6 mb-3">
                  <label class="form-label" for="tr-state">{{ 'common.state' | translate }}</label>
                  <select id="tr-state" class="form-select"
                    [value]="model().stateOrProvinceId"
                    (change)="patch({ stateOrProvinceId: $any($event.target).value })">
                    <option value="">{{ 'common.any' | translate }}</option>
                    @for (s of states(); track s.id) {
                      <option value="{{ s.id }}">{{ s.name }}</option>
                    }
                  </select>
                </div>
              </div>
              <div class="row">
                <div class="col-md-4 mb-3">
                  <label class="form-label" for="tr-zip">{{ 'common.zip' | translate }}</label>
                  <input id="tr-zip" type="text" class="form-control"
                    [value]="model().zipCode"
                    (input)="patch({ zipCode: $any($event.target).value })" />
                </div>
                <div class="col-md-4 mb-3">
                  <label class="form-label" for="tr-min">{{ 'shipping.col_min_subtotal' | translate }}</label>
                  <input id="tr-min" type="number" step="0.01" class="form-control"
                    [value]="model().minOrderSubtotal"
                    (input)="patch({ minOrderSubtotal: $any($event.target).value })" />
                </div>
                <div class="col-md-4 mb-3">
                  <label class="form-label" for="tr-price">{{ 'shipping.shipping_price' | translate }}</label>
                  <input id="tr-price" type="number" step="0.01" class="form-control"
                    [value]="model().shippingPrice"
                    (input)="patch({ shippingPrice: $any($event.target).value })" />
                </div>
              </div>
              <div class="mb-3">
                <label class="form-label" for="tr-note">{{ 'common.note' | translate }}</label>
                <input id="tr-note" type="text" class="form-control"
                  [value]="model().note"
                  (input)="patch({ note: $any($event.target).value })" />
              </div>

              <div class="form-actions">
                <button type="button" libButton variant="primary"
                  [disabled]="saving() || !model().shippingProviderId" (click)="save()">
                  {{ (saving() ? 'common.saving' : isNew() ? 'shipping.create' : 'common.save_changes') | translate }}
                </button>
                <a routerLink="/shipping" class="btn btn-outline-secondary">{{ 'common.cancel' | translate }}</a>
              </div>
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class AdminTableRateForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminShippingService);
  private readonly locations = inject(AdminLocationsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly rateId = computed(() => Number(this.idParam().get('id')));

  protected readonly rates = this.service.tableRatesResource();
  protected readonly countries = this.locations.countriesResource();
  protected readonly states = signal<StateOrProvinceLookupDto[]>([]);

  /** Carriers a rate can belong to — every provider except the "Free" one (which has no rate rows). */
  protected readonly carriers = computed(
    () => (this.providers.value() ?? []).filter((p) => p.id !== 'Free'),
  );
  private readonly providers = this.service.providersResource();

  private readonly existing = computed(
    () => this.rates.value()?.find((r) => r.id === this.rateId()) ?? null,
  );

  protected readonly model = signal<TableRateFormModel>(emptyModel());
  protected readonly saving = signal(false);

  private seeded = false;

  constructor() {
    effect(() => {
      if (this.isNew() || this.seeded) {
        return;
      }
      const r = this.existing();
      if (!r) {
        return;
      }
      this.seeded = true;
      this.model.set({
        shippingProviderId: r.shippingProviderId ?? '',
        countryId: r.countryId ?? '',
        stateOrProvinceId: r.stateOrProvinceId === null ? '' : String(r.stateOrProvinceId),
        zipCode: r.zipCode ?? '',
        minOrderSubtotal: String(r.minOrderSubtotal),
        shippingPrice: String(r.shippingPrice),
        note: r.note ?? '',
      });
      if (r.countryId) {
        this.locations.states(r.countryId).subscribe({
          next: (states) => this.states.set(states),
        });
      }
    });
  }

  protected patch(patch: Partial<TableRateFormModel>): void {
    this.model.update((f) => ({ ...f, ...patch }));
  }

  protected onCountryChange(countryId: string): void {
    this.patch({ countryId, stateOrProvinceId: '' });
    this.states.set([]);
    if (countryId) {
      this.locations.states(countryId).subscribe({
        next: (states) => this.states.set(states),
        error: () => this.states.set([]),
      });
    }
  }

  protected save(): void {
    const f = this.model();
    this.saving.set(true);
    const body: TableRateUpsertRequest = {
      shippingProviderId: f.shippingProviderId,
      countryId: f.countryId || null,
      stateOrProvinceId: f.stateOrProvinceId ? Number(f.stateOrProvinceId) : null,
      zipCode: f.zipCode || null,
      minOrderSubtotal: Number(f.minOrderSubtotal) || 0,
      shippingPrice: Number(f.shippingPrice) || 0,
      note: f.note || null,
    };
    const request = this.isNew()
      ? this.service.createTableRate(body)
      : this.service.updateTableRate(this.rateId(), body);
    request.subscribe({
      next: () => {
        this.toast.success(
          this.translate.instant(this.isNew() ? 'shipping.added_ok' : 'shipping.updated_ok'),
        );
        this.saving.set(false);
        void this.router.navigate(['/shipping']);
      },
      error: () => {
        this.toast.error(this.translate.instant('shipping.save_failed'));
        this.saving.set(false);
      },
    });
  }
}
