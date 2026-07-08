import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminTaxService } from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, Icon, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { TableSkeleton } from '../../shared/table-skeleton';

/**
 * Tax browser: the tax-classes manager alongside the per-destination rates list.
 * Creating and editing a rate happen on their own page (`/taxes/new`,
 * `/taxes/:id`); tax classes stay here as a lightweight secondary entity.
 */
@Component({
  selector: 'app-admin-taxes',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Button, Icon, TranslatePipe, PageHeader, TableSkeleton],
  templateUrl: './taxes.html',
})
export class AdminTaxes {
  private readonly service = inject(AdminTaxService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly classes = this.service.classesResource();
  protected readonly rates = this.service.ratesResource();
  protected readonly deletingId = signal<number | null>(null);

  // ----- Classes -------------------------------------------------------------

  protected addClass(input: HTMLInputElement): void {
    const name = input.value.trim();
    if (!name) {
      return;
    }
    this.service.createClass({ name }).subscribe({
      next: () => {
        input.value = '';
        this.classes.reload();
      },
      error: () => this.toast.error(this.translate.instant('taxes.class_create_failed')),
    });
  }

  protected renameClass(id: number, name: string): void {
    const trimmed = name.trim();
    if (!trimmed) {
      return;
    }
    this.service.updateClass(id, { name: trimmed }).subscribe({
      next: () => this.toast.success(this.translate.instant('taxes.class_updated')),
      error: () => this.toast.error(this.translate.instant('taxes.class_update_failed')),
    });
  }

  protected removeClass(id: number, name: string | null): void {
    if (!confirm(this.translate.instant('taxes.confirm_delete_class', { name: name ?? '' }))) {
      return;
    }
    this.service.deleteClass(id).subscribe({
      next: () => {
        this.classes.reload();
        this.rates.reload();
      },
      error: () =>
        this.toast.error(this.translate.instant('taxes.class_delete_failed')),
    });
  }

  // ----- Rates ----------------------------------------------------------------

  protected removeRate(id: number): void {
    if (!confirm(this.translate.instant('taxes.confirm_delete_rate'))) {
      return;
    }
    this.deletingId.set(id);
    this.service.deleteRate(id).subscribe({
      next: () => {
        this.deletingId.set(null);
        this.rates.reload();
      },
      error: () => {
        this.toast.error(this.translate.instant('taxes.rate_delete_failed'));
        this.deletingId.set(null);
      },
    });
  }
}
