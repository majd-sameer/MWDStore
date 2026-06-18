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
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/locations" class="text-decoration-none">← {{ 'countries.title' | translate }}</a>
    </nav>
    <app-page-header [title]="(isNew() ? 'countries.new_title' : 'countries.edit_title') | translate" />

    @if (!isNew() && countries.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (!isNew() && !existing()) {
      <div class="alert alert-danger">{{ 'countries.load_one_failed' | translate }}</div>
    } @else {
      <div class="row g-4">
        <div class="col-lg-7">
          <div class="card border-0 shadow-sm">
            <div class="card-body">
              <div class="row">
                <div class="col-md-3 mb-3">
                  <label class="form-label" for="cty-id">{{ 'countries.iso_code' | translate }}</label>
                  <input id="cty-id" type="text" class="form-control" maxlength="3"
                    [value]="model().id" [disabled]="!isNew()"
                    (input)="patch({ id: $any($event.target).value })" />
                </div>
                <div class="col-md-6 mb-3">
                  <label class="form-label" for="cty-name">{{ 'common.name' | translate }}</label>
                  <input id="cty-name" type="text" class="form-control"
                    [value]="model().name" (input)="patch({ name: $any($event.target).value })" />
                </div>
                <div class="col-md-3 mb-3">
                  <label class="form-label" for="cty-code3">{{ 'countries.iso3' | translate }}</label>
                  <input id="cty-code3" type="text" class="form-control" maxlength="3"
                    [value]="model().code3" (input)="patch({ code3: $any($event.target).value })" />
                </div>
              </div>

              <div class="row">
                <div class="col-sm-6 col-lg-4">
                  <div class="form-check form-switch mb-2">
                    <input id="cty-ship" type="checkbox" class="form-check-input"
                      [checked]="model().isShippingEnabled"
                      (change)="patch({ isShippingEnabled: $any($event.target).checked })" />
                    <label class="form-check-label" for="cty-ship">
                      {{ 'countries.shipping_enabled' | translate }}
                    </label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input id="cty-bill" type="checkbox" class="form-check-input"
                      [checked]="model().isBillingEnabled"
                      (change)="patch({ isBillingEnabled: $any($event.target).checked })" />
                    <label class="form-check-label" for="cty-bill">
                      {{ 'countries.billing_enabled' | translate }}
                    </label>
                  </div>
                </div>
                <div class="col-sm-6 col-lg-4">
                  <div class="form-check form-switch mb-2">
                    <input id="cty-city" type="checkbox" class="form-check-input"
                      [checked]="model().isCityEnabled"
                      (change)="patch({ isCityEnabled: $any($event.target).checked })" />
                    <label class="form-check-label" for="cty-city">
                      {{ 'countries.city_field' | translate }}
                    </label>
                  </div>
                  <div class="form-check form-switch mb-2">
                    <input id="cty-zip" type="checkbox" class="form-check-input"
                      [checked]="model().isZipCodeEnabled"
                      (change)="patch({ isZipCodeEnabled: $any($event.target).checked })" />
                    <label class="form-check-label" for="cty-zip">
                      {{ 'countries.zip_field' | translate }}
                    </label>
                  </div>
                </div>
                <div class="col-sm-6 col-lg-4">
                  <div class="form-check form-switch mb-2">
                    <input id="cty-dist" type="checkbox" class="form-check-input"
                      [checked]="model().isDistrictEnabled"
                      (change)="patch({ isDistrictEnabled: $any($event.target).checked })" />
                    <label class="form-check-label" for="cty-dist">
                      {{ 'countries.district_field' | translate }}
                    </label>
                  </div>
                </div>
              </div>

              <div class="form-actions">
                <button type="button" libButton variant="primary" [disabled]="saving()" (click)="save()">
                  {{ (saving() ? 'common.saving' : isNew() ? 'countries.create' : 'common.save_changes') | translate }}
                </button>
                <a routerLink="/locations" class="btn btn-outline-secondary">{{ 'common.cancel' | translate }}</a>
              </div>
            </div>
          </div>
        </div>

        <div class="col-lg-5">
          @if (isNew()) {
            <div class="alert alert-light border">{{ 'countries.save_to_add_states' | translate }}</div>
          } @else {
            <div class="card border-0 shadow-sm">
              <div class="card-header bg-body fw-semibold">
                {{ 'countries.states_in' | translate: { id: countryId() } }}
              </div>
              <div class="card-body">
                @for (s of states(); track s.id) {
                  <div class="d-flex align-items-center gap-2 mb-2">
                    <input type="text" class="form-control form-control-sm" [value]="s.name"
                      (change)="renameState(s, $any($event.target).value)" />
                    <button type="button" class="btn btn-sm btn-outline-danger"
                      (click)="removeState(s)">✕</button>
                  </div>
                } @empty {
                  <p class="text-body-secondary small">{{ 'countries.no_states' | translate }}</p>
                }
                <div class="d-flex gap-2 mt-3">
                  <input type="text" class="form-control form-control-sm"
                    [placeholder]="'countries.new_state_ph' | translate" #stateName />
                  <button type="button" libButton variant="secondary" [outline]="true"
                    (click)="addState(stateName)">
                    {{ 'common.add' | translate }}
                  </button>
                </div>
              </div>
            </div>
          }
        </div>
      </div>
    }
  `,
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
