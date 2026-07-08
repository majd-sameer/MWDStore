import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NgSelectModule } from '@ng-select/ng-select';
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
  imports: [Button, RouterLink, TranslatePipe, PageHeader, FormsModule, NgSelectModule],
  templateUrl: './table-rate-form.html',
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

  /** State ids are numeric but the form model stores strings — map for ng-select strict `===` matching. */
  protected readonly stateItems = computed(() =>
    this.states().map((s) => ({ id: String(s.id), name: s.name })),
  );

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
