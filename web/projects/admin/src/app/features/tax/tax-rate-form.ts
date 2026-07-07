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
  AdminTaxService,
  type StateOrProvinceLookupDto,
  type TaxRateUpsertRequest,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

interface RateFormModel {
  taxClassId: string;
  countryId: string;
  stateOrProvinceId: string;
  zipCode: string;
  rate: string;
}

function emptyModel(): RateFormModel {
  return { taxClassId: '', countryId: '', stateOrProvinceId: '', zipCode: '', rate: '0' };
}

/**
 * Create / edit a tax rate on its own page. The tax API has no single-fetch
 * endpoint for a rate, so edit mode seeds from the rates list resource. Tax
 * classes are managed back on the list page.
 */
@Component({
  selector: 'app-admin-tax-rate-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, RouterLink, TranslatePipe, PageHeader],
  templateUrl: './tax-rate-form.html',
})
export class AdminTaxRateForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminTaxService);
  private readonly locations = inject(AdminLocationsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly rateId = computed(() => Number(this.idParam().get('id')));

  protected readonly classes = this.service.classesResource();
  protected readonly rates = this.service.ratesResource();
  protected readonly countries = this.locations.countriesResource();
  protected readonly states = signal<StateOrProvinceLookupDto[]>([]);

  private readonly existing = computed(
    () => this.rates.value()?.find((r) => r.id === this.rateId()) ?? null,
  );

  protected readonly model = signal<RateFormModel>(emptyModel());
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
        taxClassId: String(r.taxClassId),
        countryId: r.countryId ?? '',
        stateOrProvinceId: r.stateOrProvinceId === null ? '' : String(r.stateOrProvinceId),
        zipCode: r.zipCode ?? '',
        rate: String(r.rate),
      });
      if (r.countryId) {
        this.locations.states(r.countryId).subscribe({
          next: (states) => this.states.set(states),
        });
      }
    });
  }

  protected patch(patch: Partial<RateFormModel>): void {
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
    if (!f.taxClassId) {
      this.toast.error(this.translate.instant('taxes.choose_class_first'));
      return;
    }
    this.saving.set(true);
    const body: TaxRateUpsertRequest = {
      taxClassId: Number(f.taxClassId),
      countryId: f.countryId || null,
      stateOrProvinceId: f.stateOrProvinceId ? Number(f.stateOrProvinceId) : null,
      zipCode: f.zipCode || null,
      rate: Number(f.rate) || 0,
    };
    const request = this.isNew()
      ? this.service.createRate(body)
      : this.service.updateRate(this.rateId(), body);
    request.subscribe({
      next: () => {
        this.toast.success(
          this.translate.instant(this.isNew() ? 'taxes.added_ok' : 'taxes.updated_ok'),
        );
        this.saving.set(false);
        void this.router.navigate(['/taxes']);
      },
      error: () => {
        this.toast.error(this.translate.instant('taxes.save_failed'));
        this.saving.set(false);
      },
    });
  }
}
