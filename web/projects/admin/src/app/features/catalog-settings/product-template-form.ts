import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  output,
  signal,
} from '@angular/core';
import {
  AdminOperationsService,
  AdminProductAttributesService,
  type AdminProductTemplateDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, ToastService } from 'ui';

/**
 * Create / edit a product template (a named attribute set) inside an offcanvas
 * panel. Opened by the template list via `NgbOffcanvas.open(...)`, which calls
 * {@link init} with the row being edited (or `null` to create) — the list DTO
 * carries the assigned attributes, so the panel seeds itself with no extra
 * fetch. Reports back through {@link saved} / {@link cancelled}.
 */
@Component({
  selector: 'app-admin-product-template-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, TranslatePipe],
  templateUrl: './product-template-form.html',
})
export class AdminProductTemplateForm {
  private readonly service = inject(AdminOperationsService);
  private readonly attributesService = inject(AdminProductAttributesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  /** Emitted after a successful create/update — the host closes + reloads. */
  readonly saved = output<void>();
  /** Emitted when the user cancels or closes the panel without saving. */
  readonly cancelled = output<void>();

  protected readonly attributes = this.attributesService.listResource();

  /** The template being edited, or `null` while creating. */
  private readonly editing = signal<AdminProductTemplateDto | null>(null);
  protected readonly isNew = computed(() => this.editing() === null);

  protected readonly name = signal('');
  protected readonly selectedIds = signal<number[]>([]);
  protected readonly saving = signal(false);

  /** Seed the panel for creating (`null`) or editing the given template. */
  init(template: AdminProductTemplateDto | null): void {
    this.editing.set(template);
    this.name.set(template?.name ?? '');
    this.selectedIds.set(template?.attributes.map((a) => a.id) ?? []);
  }

  protected toggle(id: number): void {
    this.selectedIds.update((ids) =>
      ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id],
    );
  }

  protected onCancel(): void {
    this.cancelled.emit();
  }

  protected save(): void {
    const name = this.name().trim();
    if (!name) {
      this.toast.error(this.translate.instant('common.name_required'));
      return;
    }
    this.saving.set(true);
    const body = { name, attributeIds: this.selectedIds() };
    const current = this.editing();
    const request = current
      ? this.service.updateTemplate(current.id, body)
      : this.service.createTemplate(body);
    request.subscribe({
      next: () => {
        this.toast.success(
          this.translate.instant(this.isNew() ? 'templates.created_ok' : 'templates.updated_ok'),
        );
        this.saving.set(false);
        this.saved.emit();
      },
      error: () => {
        this.toast.error(this.translate.instant('templates.save_failed'));
        this.saving.set(false);
      },
    });
  }
}
