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
  AdminOperationsService,
  AdminProductAttributesService,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';

/**
 * Create / edit a product template (a named attribute set) on its own page. The
 * templates API has no single-fetch endpoint, so edit mode seeds from the list
 * resource (the list DTO carries the assigned attributes). Saving returns to the list.
 */
@Component({
  selector: 'app-admin-product-template-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, RouterLink, TranslatePipe, PageHeader],
  templateUrl: './product-template-form.html',
})
export class AdminProductTemplateForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminOperationsService);
  private readonly attributesService = inject(AdminProductAttributesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly templateId = computed(() => Number(this.idParam().get('id')));

  protected readonly list = this.service.templatesResource();
  protected readonly attributes = this.attributesService.listResource();
  private readonly existing = computed(
    () => this.list.value()?.find((t) => t.id === this.templateId()) ?? null,
  );

  protected readonly name = signal('');
  protected readonly selectedIds = signal<number[]>([]);
  protected readonly saving = signal(false);

  private seeded = false;

  constructor() {
    effect(() => {
      if (this.isNew() || this.seeded) {
        return;
      }
      const t = this.existing();
      if (!t) {
        return;
      }
      this.seeded = true;
      this.name.set(t.name ?? '');
      this.selectedIds.set(t.attributes.map((a) => a.id));
    });
  }

  protected toggle(id: number): void {
    this.selectedIds.update((ids) =>
      ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id],
    );
  }

  protected save(): void {
    const name = this.name().trim();
    if (!name) {
      this.toast.error(this.translate.instant('common.name_required'));
      return;
    }
    this.saving.set(true);
    const body = { name, attributeIds: this.selectedIds() };
    const request = this.isNew()
      ? this.service.createTemplate(body)
      : this.service.updateTemplate(this.templateId(), body);
    request.subscribe({
      next: () => {
        this.toast.success(
          this.translate.instant(this.isNew() ? 'templates.created_ok' : 'templates.updated_ok'),
        );
        this.saving.set(false);
        void this.router.navigate(['/product-templates']);
      },
      error: () => {
        this.toast.error(this.translate.instant('templates.save_failed'));
        this.saving.set(false);
      },
    });
  }
}
