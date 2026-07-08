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
import { AdminProductOptionsService } from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';
import { MultiLangInput, type MultiLangValue } from '../../shared/multi-lang-input';

/**
 * Create / edit a product option (Color, Size, …) on its own page. The options
 * API has no single-fetch endpoint, so edit mode seeds from the list resource.
 */
@Component({
  selector: 'app-admin-product-option-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader, MultiLangInput],
  templateUrl: './product-option-form.html',
})
export class AdminProductOptionForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminProductOptionsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly optionId = computed(() => Number(this.idParam().get('id')));

  protected readonly list = this.service.listResource();
  private readonly existing = computed(
    () => this.list.value()?.find((o) => o.id === this.optionId()) ?? null,
  );

  protected readonly model = signal<{ name: MultiLangValue }>({ name: { ar: '', en: '' } });
  protected readonly f = form(this.model, (path) => {
    required(path.name.ar, { message: 'Name is required' });
  });
  protected readonly err = firstError;
  protected readonly serverError = signal<string | null>(null);

  private seeded = false;

  constructor() {
    effect(() => {
      if (this.isNew() || this.seeded) {
        return;
      }
      const o = this.existing();
      if (!o) {
        return;
      }
      this.seeded = true;
      this.model.set({ name: { ar: o.name ?? '', en: o.nameEn ?? '' } });
    });
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
          await firstValueFrom(this.service.update(this.optionId(), body));
          this.toast.success(this.translate.instant('options.updated_ok'));
        }
        await this.router.navigate(['/product-options']);
      } catch {
        this.serverError.set(this.translate.instant('options.save_failed'));
      }
      return undefined;
    });
  }
}
