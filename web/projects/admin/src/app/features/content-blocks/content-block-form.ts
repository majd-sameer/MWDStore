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
import { AdminContentBlocksService, type ContentBlockUpdateRequest } from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';

interface ContentBlockModel {
  title: string;
  text: string;
  imageUrl: string;
  linkUrl: string;
  linkText: string;
  sortOrder: number;
  isPublished: boolean;
  titleEn: string;
  textEn: string;
  linkTextEn: string;
}

function emptyModel(): ContentBlockModel {
  return {
    title: '',
    text: '',
    imageUrl: '',
    linkUrl: '',
    linkText: '',
    sortOrder: 0,
    isPublished: true,
    titleEn: '',
    textEn: '',
    linkTextEn: '',
  };
}

/**
 * Edit one homepage content block (the set is fixed/seeded — no create/delete here). The Arabic
 * fields are the base entity columns; the English fields are the `LocalizedContentProperty`
 * overlay, written in the same update call. Route: `/content-blocks/:id`.
 */
@Component({
  selector: 'app-admin-content-block-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/content-blocks" class="text-decoration-none">
        ← {{ 'content_blocks.title' | translate }}
      </a>
    </nav>
    <app-page-header
      [title]="'content_blocks.edit_title' | translate"
      [subtitle]="existing.value()?.key ?? null"
    />

    @if (existing.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (existing.error()) {
      <div class="alert alert-danger">{{ 'common.error_api' | translate }}</div>
    } @else {
      <div class="row g-4">
        <div class="col-lg-9">
          <div class="card border-0 shadow-sm">
            <div class="card-body">
              @if (serverError(); as message) {
                <div class="alert alert-danger" role="alert">{{ message }}</div>
              }
              <form (submit)="onSubmit($event)" novalidate>
                <div class="row">
                  <div class="col-md-6">
                    <h2 class="h6 text-body-secondary text-uppercase mb-3">
                      {{ 'content_blocks.base_lang' | translate }}
                    </h2>
                    <lib-form-field
                      [label]="'content_blocks.field_title' | translate"
                      controlId="cb-title"
                      [required]="true"
                      [error]="err(f.title())"
                    >
                      <input id="cb-title" type="text" class="form-control" dir="rtl"
                        [class.is-invalid]="!!err(f.title())" [formField]="f.title" />
                    </lib-form-field>
                    <lib-form-field [label]="'content_blocks.field_text' | translate" controlId="cb-text">
                      <textarea id="cb-text" rows="4" class="form-control" dir="rtl" [formField]="f.text"></textarea>
                    </lib-form-field>
                    <lib-form-field
                      [label]="'content_blocks.field_link_text' | translate"
                      controlId="cb-link-text"
                    >
                      <input id="cb-link-text" type="text" class="form-control" dir="rtl" [formField]="f.linkText" />
                    </lib-form-field>
                  </div>

                  <div class="col-md-6">
                    <h2 class="h6 text-body-secondary text-uppercase mb-3">
                      {{ 'content_blocks.english' | translate }}
                    </h2>
                    <lib-form-field [label]="'content_blocks.field_title' | translate" controlId="cb-title-en">
                      <input id="cb-title-en" type="text" class="form-control" dir="ltr" [formField]="f.titleEn" />
                    </lib-form-field>
                    <lib-form-field [label]="'content_blocks.field_text' | translate" controlId="cb-text-en">
                      <textarea id="cb-text-en" rows="4" class="form-control" dir="ltr" [formField]="f.textEn"></textarea>
                    </lib-form-field>
                    <lib-form-field
                      [label]="'content_blocks.field_link_text' | translate"
                      controlId="cb-link-text-en"
                    >
                      <input id="cb-link-text-en" type="text" class="form-control" dir="ltr" [formField]="f.linkTextEn" />
                    </lib-form-field>
                  </div>
                </div>

                <hr class="my-3" />

                <div class="row">
                  <div class="col-md-8">
                    <lib-form-field
                      [label]="'content_blocks.field_image_url' | translate"
                      controlId="cb-image"
                      [hint]="'content_blocks.field_image_hint' | translate"
                    >
                      <input id="cb-image" type="text" class="form-control" [formField]="f.imageUrl" />
                    </lib-form-field>
                  </div>
                  <div class="col-md-4">
                    <lib-form-field [label]="'content_blocks.field_sort_order' | translate" controlId="cb-order">
                      <input id="cb-order" type="number" class="form-control" [formField]="f.sortOrder" />
                    </lib-form-field>
                  </div>
                </div>
                <div class="row">
                  <div class="col-md-8">
                    <lib-form-field [label]="'content_blocks.field_link_url' | translate" controlId="cb-link-url">
                      <input id="cb-link-url" type="text" class="form-control" [formField]="f.linkUrl" />
                    </lib-form-field>
                  </div>
                </div>

                <div class="form-check form-switch mb-3">
                  <input id="cb-pub" type="checkbox" class="form-check-input" [formField]="f.isPublished" />
                  <label for="cb-pub" class="form-check-label">{{ 'common.published' | translate }}</label>
                </div>

                <div class="form-actions">
                  <button libButton variant="primary" [disabled]="f().submitting()">
                    {{ (f().submitting() ? 'common.saving' : 'common.save_changes') | translate }}
                  </button>
                  <a routerLink="/content-blocks" class="btn btn-outline-secondary">
                    {{ 'common.cancel' | translate }}
                  </a>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class AdminContentBlockForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminContentBlocksService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  private readonly blockId = computed(() => Number(this.idParam().get('id')));

  protected readonly existing = this.service.getResource(this.blockId);

  protected readonly model = signal<ContentBlockModel>(emptyModel());
  protected readonly f = form(this.model, (path) => {
    required(path.title, { message: 'Title is required' });
  });
  protected readonly err = firstError;
  protected readonly serverError = signal<string | null>(null);

  private seeded = false;

  constructor() {
    // Seed the form once the block arrives.
    effect(() => {
      if (this.seeded) {
        return;
      }
      const b = this.existing.value();
      if (!b) {
        return;
      }
      this.seeded = true;
      this.model.set({
        title: b.title ?? '',
        text: b.text ?? '',
        imageUrl: b.imageUrl ?? '',
        linkUrl: b.linkUrl ?? '',
        linkText: b.linkText ?? '',
        sortOrder: b.sortOrder,
        isPublished: b.isPublished,
        titleEn: b.titleEn ?? '',
        textEn: b.textEn ?? '',
        linkTextEn: b.linkTextEn ?? '',
      });
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      const body: ContentBlockUpdateRequest = {
        title: m.title || null,
        text: m.text || null,
        imageUrl: m.imageUrl || null,
        linkUrl: m.linkUrl || null,
        linkText: m.linkText || null,
        sortOrder: Number(m.sortOrder),
        isPublished: m.isPublished,
        titleEn: m.titleEn || null,
        textEn: m.textEn || null,
        linkTextEn: m.linkTextEn || null,
      };
      try {
        await firstValueFrom(this.service.update(this.blockId(), body));
        this.toast.success(this.translate.instant('content_blocks.updated_ok'));
        await this.router.navigate(['/content-blocks']);
      } catch {
        this.serverError.set(this.translate.instant('content_blocks.save_failed'));
      }
      return undefined;
    });
  }
}
