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
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/product-templates" class="text-decoration-none">← {{ 'templates.title' | translate }}</a>
    </nav>
    <app-page-header [title]="(isNew() ? 'templates.new_title' : 'templates.edit_title') | translate" />

    @if (!isNew() && list.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (!isNew() && list.error()) {
      <div class="alert alert-danger">{{ 'templates.load_one_failed' | translate }}</div>
    } @else {
      <div class="row g-4">
        <div class="col-lg-7">
          <div class="card border-0 shadow-sm">
            <div class="card-body">
              <label class="form-label" for="tpl-name">{{ 'common.name' | translate }}</label>
              <input id="tpl-name" type="text" class="form-control mb-3" [value]="name()"
                (input)="name.set($any($event.target).value)" />

              <div class="form-label">{{ 'nav.attributes' | translate }}</div>
              <div class="border rounded p-2 mb-3" style="max-height: 18rem; overflow-y: auto">
                @for (a of attributes.value() ?? []; track a.id) {
                  <div class="form-check">
                    <input type="checkbox" class="form-check-input" id="tpl-attr-{{ a.id }}"
                      [checked]="selectedIds().includes(a.id)"
                      (change)="toggle(a.id)" />
                    <label class="form-check-label" for="tpl-attr-{{ a.id }}">
                      {{ a.groupName }} / {{ a.name }}
                    </label>
                  </div>
                } @empty {
                  <span class="text-body-secondary small">{{ 'templates.define_attributes_first' | translate }}</span>
                }
              </div>

              <div class="form-actions">
                <button type="button" libButton variant="primary" [disabled]="saving()" (click)="save()">
                  {{ (saving() ? 'common.saving' : isNew() ? 'templates.create' : 'common.save_changes') | translate }}
                </button>
                <a routerLink="/product-templates" class="btn btn-outline-secondary">{{ 'common.cancel' | translate }}</a>
              </div>
            </div>
          </div>
        </div>
      </div>
    }
  `,
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
