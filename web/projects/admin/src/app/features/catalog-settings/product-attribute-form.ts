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
  AdminProductAttributesService,
  type AdminProductAttributeDto,
  type ProductAttributeUpsertRequest,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';

/** `AdminProductAttributeDto`/`ProductAttributeUpsertRequest` don't yet declare the English overlay
 * fields in the shared `data-access` models — extend them locally (structural typing lets the
 * existing service methods accept the wider request/response shape without changes there). */
interface AdminProductAttributeDtoEn extends AdminProductAttributeDto {
  nameEn: string | null;
  hasEnglish: boolean;
}

interface ProductAttributeUpsertRequestEn extends ProductAttributeUpsertRequest {
  nameEn?: string | null;
}

interface AttributeModel {
  name: string;
  groupId: string;
  nameEn: string;
}

/**
 * Create / edit a product attribute on its own page. The attributes API has no
 * single-fetch endpoint, so edit mode seeds from the list resource. Attribute
 * groups are managed back on the list page. The Arabic name is the base entity
 * column; the English name is the `LocalizedContentProperty` overlay, written
 * in the same create/update call.
 */
@Component({
  selector: 'app-admin-product-attribute-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/product-attributes" class="text-decoration-none">← {{ 'attributes.title' | translate }}</a>
    </nav>
    <app-page-header [title]="(isNew() ? 'attributes.new_title' : 'attributes.edit_title') | translate" />

    @if (!isNew() && list.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (!isNew() && list.error()) {
      <div class="alert alert-danger">{{ 'attributes.load_one_failed' | translate }}</div>
    } @else {
      <div class="row g-4">
        <div class="col-lg-7">
          <div class="card border-0 shadow-sm">
            <div class="card-body">
              @if (serverError(); as message) {
                <div class="alert alert-danger" role="alert">{{ message }}</div>
              }
              <form (submit)="onSubmit($event)" novalidate>
                <lib-form-field [label]="'attributes.col_group' | translate" controlId="attr-group" [required]="true" [error]="err(f.groupId())">
                  <select id="attr-group" class="form-select"
                    [class.is-invalid]="!!err(f.groupId())" [formField]="f.groupId">
                    <option value="">{{ 'attributes.choose_group' | translate }}</option>
                    @for (g of groups.value() ?? []; track g.id) {
                      <option value="{{ g.id }}">{{ g.name }}</option>
                    }
                  </select>
                </lib-form-field>

                <div class="row">
                  <div class="col-md-6">
                    <h2 class="h6 text-body-secondary text-uppercase mb-3">
                      {{ 'attributes.base_lang' | translate }}
                    </h2>
                    <lib-form-field [label]="'common.name' | translate" controlId="attr-name" [required]="true" [error]="err(f.name())">
                      <input id="attr-name" type="text" class="form-control" dir="rtl"
                        [class.is-invalid]="!!err(f.name())" [formField]="f.name" />
                    </lib-form-field>
                  </div>
                  <div class="col-md-6">
                    <h2 class="h6 text-body-secondary text-uppercase mb-3">
                      {{ 'attributes.english' | translate }}
                    </h2>
                    <lib-form-field [label]="'common.name' | translate" controlId="attr-name-en">
                      <input id="attr-name-en" type="text" class="form-control" dir="ltr" [formField]="f.nameEn" />
                    </lib-form-field>
                  </div>
                </div>

                <div class="form-actions">
                  <button libButton variant="primary" [disabled]="f().submitting()">
                    {{ (f().submitting() ? 'common.saving' : isNew() ? 'attributes.create' : 'common.save_changes') | translate }}
                  </button>
                  <a routerLink="/product-attributes" class="btn btn-outline-secondary">{{ 'common.cancel' | translate }}</a>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class AdminProductAttributeForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminProductAttributesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly attributeId = computed(() => Number(this.idParam().get('id')));

  protected readonly list = this.service.listResource();
  protected readonly groups = this.service.groupsResource();
  private readonly existing = computed(
    () =>
      (this.list.value() as AdminProductAttributeDtoEn[] | undefined)?.find(
        (a) => a.id === this.attributeId(),
      ) ?? null,
  );

  protected readonly model = signal<AttributeModel>({ name: '', groupId: '', nameEn: '' });
  protected readonly f = form(this.model, (path) => {
    required(path.name, { message: 'Name is required' });
    required(path.groupId, { message: 'Group is required' });
  });
  protected readonly err = firstError;
  protected readonly serverError = signal<string | null>(null);

  private seeded = false;

  constructor() {
    effect(() => {
      if (this.isNew() || this.seeded) {
        return;
      }
      const a = this.existing();
      if (!a) {
        return;
      }
      this.seeded = true;
      this.model.set({ name: a.name ?? '', groupId: String(a.groupId), nameEn: a.nameEn ?? '' });
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const body: ProductAttributeUpsertRequestEn = {
        name: this.model().name,
        groupId: Number(this.model().groupId),
        nameEn: this.model().nameEn || null,
      };
      try {
        if (this.isNew()) {
          await firstValueFrom(this.service.create(body));
          this.toast.success(this.translate.instant('attributes.created_ok'));
        } else {
          await firstValueFrom(this.service.update(this.attributeId(), body));
          this.toast.success(this.translate.instant('attributes.updated_ok'));
        }
        await this.router.navigate(['/product-attributes']);
      } catch {
        this.serverError.set(this.translate.instant('attributes.save_failed'));
      }
      return undefined;
    });
  }
}
