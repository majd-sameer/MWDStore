import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
  TemplateRef,
  viewChild,
} from '@angular/core';
import {
  form,
  FormField as Control,
  required,
  submit,
} from '@angular/forms/signals';
import { NgbOffcanvas, type NgbOffcanvasRef } from '@ng-bootstrap/ng-bootstrap';
import {
  AdminProductOptionsService,
  type AdminProductOptionListItem,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, Icon, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';
import { MultiLangInput, type MultiLangValue } from '../../shared/multi-lang-input';

/**
 * Product option browser (Color, Size, …): a full-width list. Creating and
 * editing happen in an offcanvas that slides in from the end — the option
 * carries only a bilingual name, so a full form page would be overkill.
 */
@Component({
  selector: 'app-admin-product-options',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    Control,
    FormField,
    Button,
    Icon,
    TranslatePipe,
    PageHeader,
    TableSkeleton,
    MultiLangInput,
  ],
  templateUrl: './product-options.html',
})
export class AdminProductOptions {
  private readonly service = inject(AdminProductOptionsService);
  private readonly offcanvas = inject(NgbOffcanvas);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly list = this.service.listResource();
  protected readonly deletingId = signal<number | null>(null);

  // ----- Create / edit offcanvas -----------------------------------------------
  private readonly panel = viewChild.required<TemplateRef<unknown>>('optionPanel');
  private panelRef: NgbOffcanvasRef | null = null;

  /** The option being edited, or null while creating a new one. */
  protected readonly editing = signal<AdminProductOptionListItem | null>(null);
  protected readonly isNew = signal(true);

  protected readonly model = signal<{ name: MultiLangValue }>({ name: { ar: '', en: '' } });
  protected readonly f = form(this.model, (path) => {
    required(path.name.ar, { message: 'Name is required' });
  });
  protected readonly err = firstError;
  protected readonly serverError = signal<string | null>(null);

  /** Opens the panel to create a new option. */
  protected openNew(): void {
    this.editing.set(null);
    this.isNew.set(true);
    this.model.set({ name: { ar: '', en: '' } });
    this.openPanel();
  }

  /** Opens the panel to edit an existing option. */
  protected openEdit(o: AdminProductOptionListItem): void {
    this.editing.set(o);
    this.isNew.set(false);
    this.model.set({ name: { ar: o.name ?? '', en: o.nameEn ?? '' } });
    this.openPanel();
  }

  private openPanel(): void {
    this.serverError.set(null);
    this.panelRef = this.offcanvas.open(this.panel(), {
      position: 'end',
      ariaLabelledBy: 'option-panel-title',
      // Only Cancel/Save close the panel — ignore backdrop clicks and Esc so an
      // accidental click can't discard an in-progress form.
      backdrop: 'static',
      keyboard: false,
    });
  }

  protected closePanel(): void {
    this.panelRef?.dismiss();
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const body = { name: this.model().name.ar, nameEn: this.model().name.en || null };
      try {
        if (this.isNew()) {
          await firstValueFrom(this.service.create(body));
          this.toast.success(this.translate.instant('options.created_ok'));
        } else {
          await firstValueFrom(this.service.update(this.editing()!.id, body));
          this.toast.success(this.translate.instant('options.updated_ok'));
        }
        this.panelRef?.close();
        this.list.reload();
      } catch {
        this.serverError.set(this.translate.instant('options.save_failed'));
      }
      return undefined;
    });
  }

  protected remove(o: AdminProductOptionListItem): void {
    if (!confirm(this.translate.instant('options.confirm_delete', { name: o.name ?? '#' + o.id }))) {
      return;
    }
    this.deletingId.set(o.id);
    this.service.delete(o.id).subscribe({
      next: () => {
        this.toast.success(this.translate.instant('options.deleted_ok'));
        this.deletingId.set(null);
        this.list.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('options.delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
