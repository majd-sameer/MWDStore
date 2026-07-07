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
  AdminSystemService,
  type StateOrProvinceLookupDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

interface CountryModel {
  id: string;
  name: string;
  code3: string;
  isShippingEnabled: boolean;
  isBillingEnabled: boolean;
  isCityEnabled: boolean;
  isZipCodeEnabled: boolean;
  isDistrictEnabled: boolean;
}

function emptyModel(): CountryModel {
  return {
    id: '',
    name: '',
    code3: '',
    isShippingEnabled: true,
    isBillingEnabled: true,
    isCityEnabled: true,
    isZipCodeEnabled: true,
    isDistrictEnabled: false,
  };
}

/**
 * Create / edit a country on its own page. The countries API has no single-fetch
 * endpoint, so edit mode seeds from the list resource. States are managed inline
 * once the country exists; creating a new country lands you on its edit page.
 */
@Component({
  selector: 'app-admin-country-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, RouterLink, TranslatePipe, PageHeader],
  templateUrl: './country-form.html',
})
export class AdminCountryForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminSystemService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  protected readonly countryId = computed(() => this.idParam().get('id') ?? '');

  protected readonly countries = this.service.countriesResource();
  protected readonly existing = computed(
    () => this.countries.value()?.find((c) => c.id === this.countryId()) ?? null,
  );

  protected readonly model = signal<CountryModel>(emptyModel());
  protected readonly states = signal<StateOrProvinceLookupDto[]>([]);
  protected readonly saving = signal(false);

  private seeded = false;

  constructor() {
    effect(() => {
      if (this.isNew() || this.seeded) {
        return;
      }
      const c = this.existing();
      if (!c) {
        return;
      }
      this.seeded = true;
      this.model.set({
        id: c.id,
        name: c.name ?? '',
        code3: c.code3 ?? '',
        isShippingEnabled: c.isShippingEnabled,
        isBillingEnabled: c.isBillingEnabled,
        isCityEnabled: c.isCityEnabled,
        isZipCodeEnabled: c.isZipCodeEnabled,
        isDistrictEnabled: c.isDistrictEnabled,
      });
      this.loadStates();
    });
  }

  protected patch(patch: Partial<CountryModel>): void {
    this.model.update((m) => ({ ...m, ...patch }));
  }

  private loadStates(): void {
    this.service.states(this.countryId()).subscribe({
      next: (states) => this.states.set(states),
      error: () => this.states.set([]),
    });
  }

  protected save(): void {
    const m = this.model();
    if (this.isNew()) {
      const id = m.id.trim().toUpperCase();
      const name = m.name.trim();
      if (!id || !name) {
        this.toast.error(this.translate.instant('countries.id_name_required'));
        return;
      }
      this.saving.set(true);
      this.service.createCountry({ id, name }).subscribe({
        next: () => {
          this.toast.success(this.translate.instant('countries.created_ok'));
          this.saving.set(false);
          void this.router.navigate(['/locations', id]);
        },
        error: () => {
          this.toast.error(this.translate.instant('countries.create_failed'));
          this.saving.set(false);
        },
      });
      return;
    }
    this.saving.set(true);
    this.service
      .updateCountry(this.countryId(), {
        name: m.name || this.countryId(),
        code3: m.code3 || null,
        isBillingEnabled: m.isBillingEnabled,
        isShippingEnabled: m.isShippingEnabled,
        isCityEnabled: m.isCityEnabled,
        isZipCodeEnabled: m.isZipCodeEnabled,
        isDistrictEnabled: m.isDistrictEnabled,
      })
      .subscribe({
        next: () => {
          this.toast.success(this.translate.instant('countries.updated_ok'));
          this.saving.set(false);
          void this.router.navigate(['/locations']);
        },
        error: () => {
          this.toast.error(this.translate.instant('countries.update_failed'));
          this.saving.set(false);
        },
      });
  }

  protected addState(input: HTMLInputElement): void {
    const name = input.value.trim();
    if (!name) {
      return;
    }
    this.service.createState(this.countryId(), { name }).subscribe({
      next: () => {
        input.value = '';
        this.loadStates();
      },
      error: () => this.toast.error(this.translate.instant('countries.state_create_failed')),
    });
  }

  protected renameState(s: StateOrProvinceLookupDto, name: string): void {
    const trimmed = name.trim();
    if (!trimmed) {
      return;
    }
    this.service.updateState(s.id, { name: trimmed }).subscribe({
      next: () => this.toast.success(this.translate.instant('countries.state_updated')),
      error: () => this.toast.error(this.translate.instant('countries.state_update_failed')),
    });
  }

  protected removeState(s: StateOrProvinceLookupDto): void {
    if (!confirm(this.translate.instant('countries.confirm_delete_state', { name: s.name ?? '' }))) {
      return;
    }
    this.service.deleteState(s.id).subscribe({
      next: () => this.loadStates(),
      error: () => this.toast.error(this.translate.instant('countries.state_delete_failed')),
    });
  }
}
