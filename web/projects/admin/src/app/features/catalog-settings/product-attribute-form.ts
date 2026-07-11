import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import {
  form,
  FormField as Control,
  required,
  submit,
} from '@angular/forms/signals';
import { NgSelectModule } from '@ng-select/ng-select';
import {
  AdminProductAttributesService,
  type AdminProductAttributeDto,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, Icon, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { MultiLangInput, type MultiLangValue } from '../../shared/multi-lang-input';

interface AttributeModel {
  name: MultiLangValue;
  groupId: string;
}

/**
 * Create / edit a product attribute inside an offcanvas panel. Opened by the
 * attribute list via `NgbOffcanvas.open(AdminProductAttributeForm)`, which calls
 * {@link init} with the row being edited (or `null` to create). Reports back
 * through {@link saved} / {@link cancelled} so the list can close + refresh.
 */
@Component({
  selector: 'app-admin-product-attribute-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, NgSelectModule, FormField, Button, Icon, TranslatePipe, MultiLangInput],
  templateUrl: './product-attribute-form.html',
})
export class AdminProductAttributeForm {
  private readonly service = inject(AdminProductAttributesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  /** Emitted after a successful create/update — the host closes + reloads. */
  readonly saved = output<void>();
  /** Emitted when the user cancels or closes the panel without saving. */
  readonly cancelled = output<void>();

  protected readonly groups = this.service.groupsResource();
  /**
   * Group options with string ids so ng-select's strict `compareWith` matches
   * the string `groupId` field (native `<option value>` was implicitly string).
   */
  protected readonly groupItems = computed(() =>
    (this.groups.value() ?? []).map((g) => ({ id: String(g.id), name: g.name })),
  );

  /** The attribute being edited, or `null` while creating. */
  private readonly editing = signal<AdminProductAttributeDto | null>(null);
  protected readonly isNew = computed(() => this.editing() === null);

  protected readonly model = signal<AttributeModel>({ name: { ar: '', en: '' }, groupId: '' });
  protected readonly f = form(this.model, (path) => {
    required(path.name.ar, { message: 'Name is required' });
    required(path.groupId, { message: 'Group is required' });
  });
  protected readonly err = firstError;
  protected readonly serverError = signal<string | null>(null);

  /** Seed the panel for creating (`null`) or editing the given attribute. */
  init(attribute: AdminProductAttributeDto | null): void {
    this.editing.set(attribute);
    this.serverError.set(null);
    this.model.set(
      attribute
        ? { name: { ar: attribute.name ?? '', en: attribute.nameEn ?? '' }, groupId: String(attribute.groupId) }
        : { name: { ar: '', en: '' }, groupId: '' },
    );
  }

  protected onCancel(): void {
    this.cancelled.emit();
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      const body = { name: m.name.ar, nameEn: m.name.en || null, groupId: Number(m.groupId) };
      try {
        const current = this.editing();
        if (current) {
          await firstValueFrom(this.service.update(current.id, body));
          this.toast.success(this.translate.instant('attributes.updated_ok'));
        } else {
          await firstValueFrom(this.service.create(body));
          this.toast.success(this.translate.instant('attributes.created_ok'));
        }
        this.saved.emit();
      } catch {
        this.serverError.set(this.translate.instant('attributes.save_failed'));
      }
      return undefined;
    });
  }
}
