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
import { AdminProductAttributesService } from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';
import { MultiLangInput, type MultiLangValue } from '../../shared/multi-lang-input';

interface AttributeModel {
  name: MultiLangValue;
  groupId: string;
}

/**
 * Create / edit a product attribute on its own page. The attributes API has no
 * single-fetch endpoint, so edit mode seeds from the list resource. Attribute
 * groups are managed back on the list page.
 */
@Component({
  selector: 'app-admin-product-attribute-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, NgSelectModule, FormField, Button, RouterLink, TranslatePipe, PageHeader, MultiLangInput],
  templateUrl: './product-attribute-form.html',
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
  /**
   * Group options with string ids so ng-select's strict `compareWith` matches
   * the string `groupId` field (native `<option value>` was implicitly string).
   */
  protected readonly groupItems = computed(() =>
    (this.groups.value() ?? []).map((g) => ({ id: String(g.id), name: g.name })),
  );
  private readonly existing = computed(
    () => this.list.value()?.find((a) => a.id === this.attributeId()) ?? null,
  );

  protected readonly model = signal<AttributeModel>({ name: { ar: '', en: '' }, groupId: '' });
  protected readonly f = form(this.model, (path) => {
    required(path.name.ar, { message: 'Name is required' });
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
      this.model.set({ name: { ar: a.name ?? '', en: a.nameEn ?? '' }, groupId: String(a.groupId) });
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      const body = { name: m.name.ar, nameEn: m.name.en || null, groupId: Number(m.groupId) };
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
