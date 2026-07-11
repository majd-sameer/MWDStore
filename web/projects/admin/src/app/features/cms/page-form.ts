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
import { AdminCmsService, type AdminPageDto, type PageUpsertRequest } from 'data-access';
import { firstValueFrom } from 'rxjs';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, FormField, ToastService } from 'ui';
import { firstError } from '../../shared/field-error';
import { PageHeader } from '../../shared/page-header';

/**
 * `AdminPageDto`/`PageUpsertRequest` don't yet declare the English overlay fields in the shared
 * `data-access` models — extend them locally (structural typing lets `AdminCmsService`'s existing
 * methods accept the wider request/response shape without changes there).
 */
interface AdminPageDtoEn extends AdminPageDto {
  nameEn: string | null;
  bodyEn: string | null;
  metaTitleEn: string | null;
  metaKeywordsEn: string | null;
  metaDescriptionEn: string | null;
  hasEnglish: boolean;
}

interface PageUpsertRequestEn extends PageUpsertRequest {
  nameEn?: string | null;
  bodyEn?: string | null;
  metaTitleEn?: string | null;
  metaKeywordsEn?: string | null;
  metaDescriptionEn?: string | null;
}

interface PageModel {
  name: string;
  slug: string;
  body: string;
  metaTitle: string;
  metaKeywords: string;
  metaDescription: string;
  isPublished: boolean;
  nameEn: string;
  bodyEn: string;
  metaTitleEn: string;
  metaKeywordsEn: string;
  metaDescriptionEn: string;
}

function emptyModel(): PageModel {
  return {
    name: '',
    slug: '',
    body: '',
    metaTitle: '',
    metaKeywords: '',
    metaDescription: '',
    isPublished: true,
    nameEn: '',
    bodyEn: '',
    metaTitleEn: '',
    metaKeywordsEn: '',
    metaDescriptionEn: '',
  };
}

/**
 * Create / edit a CMS page on its own page (mirrors the product form). The pages
 * API has no single-fetch endpoint, but the list DTO carries the full body +
 * meta, so edit mode seeds from the list resource. Saving returns to the list.
 * The Arabic fields are the base entity columns; the English fields are the
 * `LocalizedContentProperty` overlay, written in the same create/update call.
 */
@Component({
  selector: 'app-admin-page-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Control, FormField, Button, RouterLink, TranslatePipe, PageHeader],
  template: `
    <nav class="mb-3" aria-label="breadcrumb">
      <a routerLink="/pages" class="text-decoration-none">← {{ 'pages.title' | translate }}</a>
    </nav>
    <app-page-header [title]="(isNew() ? 'pages.new_title' : 'pages.edit_title') | translate" />

    @if (!isNew() && list.isLoading()) {
      <div class="text-center py-5">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
        </div>
      </div>
    } @else if (!isNew() && list.error()) {
      <div class="alert alert-danger">{{ 'pages.load_one_failed' | translate }}</div>
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
                    <lib-form-field [label]="'common.slug' | translate" controlId="pg-slug" [hint]="'common.slug_hint' | translate">
                      <input id="pg-slug" type="text" class="form-control" [formField]="f.slug" />
                    </lib-form-field>
                  </div>
                </div>

                <div class="row">
                  <div class="col-md-6">
                    <h2 class="h6 text-body-secondary text-uppercase mb-3">
                      {{ 'pages.base_lang' | translate }}
                    </h2>
                    <lib-form-field [label]="'common.name' | translate" controlId="pg-name" [required]="true" [error]="err(f.name())">
                      <input id="pg-name" type="text" class="form-control" dir="rtl"
                        [class.is-invalid]="!!err(f.name())" [formField]="f.name" />
                    </lib-form-field>
                    <lib-form-field [label]="'pages.body_html' | translate" controlId="pg-body">
                      <textarea id="pg-body" rows="10" class="form-control font-monospace" dir="rtl"
                        [formField]="f.body"></textarea>
                    </lib-form-field>
                    <lib-form-field [label]="'pages.meta_title' | translate" controlId="pg-mtitle">
                      <input id="pg-mtitle" type="text" class="form-control" dir="rtl" [formField]="f.metaTitle" />
                    </lib-form-field>
                    <lib-form-field [label]="'pages.meta_keywords' | translate" controlId="pg-mkey">
                      <input id="pg-mkey" type="text" class="form-control" dir="rtl" [formField]="f.metaKeywords" />
                    </lib-form-field>
                    <lib-form-field [label]="'pages.meta_description' | translate" controlId="pg-mdesc">
                      <textarea id="pg-mdesc" rows="2" class="form-control" dir="rtl" [formField]="f.metaDescription"></textarea>
                    </lib-form-field>
                  </div>

                  <div class="col-md-6">
                    <h2 class="h6 text-body-secondary text-uppercase mb-3">
                      {{ 'pages.english' | translate }}
                    </h2>
                    <lib-form-field [label]="'common.name' | translate" controlId="pg-name-en">
                      <input id="pg-name-en" type="text" class="form-control" dir="ltr" [formField]="f.nameEn" />
                    </lib-form-field>
                    <lib-form-field [label]="'pages.body_html' | translate" controlId="pg-body-en">
                      <textarea id="pg-body-en" rows="10" class="form-control font-monospace" dir="ltr"
                        [formField]="f.bodyEn"></textarea>
                    </lib-form-field>
                    <lib-form-field [label]="'pages.meta_title' | translate" controlId="pg-mtitle-en">
                      <input id="pg-mtitle-en" type="text" class="form-control" dir="ltr" [formField]="f.metaTitleEn" />
                    </lib-form-field>
                    <lib-form-field [label]="'pages.meta_keywords' | translate" controlId="pg-mkey-en">
                      <input id="pg-mkey-en" type="text" class="form-control" dir="ltr" [formField]="f.metaKeywordsEn" />
                    </lib-form-field>
                    <lib-form-field [label]="'pages.meta_description' | translate" controlId="pg-mdesc-en">
                      <textarea id="pg-mdesc-en" rows="2" class="form-control" dir="ltr" [formField]="f.metaDescriptionEn"></textarea>
                    </lib-form-field>
                  </div>
                </div>

                <div class="form-check form-switch mb-3">
                  <input id="pg-pub" type="checkbox" class="form-check-input" [formField]="f.isPublished" />
                  <label for="pg-pub" class="form-check-label">{{ 'common.published' | translate }}</label>
                </div>

                <div class="form-actions">
                  <button libButton variant="primary" [disabled]="f().submitting()">
                    {{ (f().submitting() ? 'common.saving' : isNew() ? 'pages.create' : 'common.save_changes') | translate }}
                  </button>
                  <a routerLink="/pages" class="btn btn-outline-secondary">{{ 'common.cancel' | translate }}</a>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    }
  `,
})
export class AdminPageForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminCmsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly pageId = computed(() => Number(this.idParam().get('id')));

  protected readonly list = this.service.pagesResource();
  private readonly existing = computed(
    () => (this.list.value() as AdminPageDtoEn[] | undefined)?.find((p) => p.id === this.pageId()) ?? null,
  );

  protected readonly model = signal<PageModel>(emptyModel());
  protected readonly f = form(this.model, (path) => {
    required(path.name, { message: 'Name is required' });
  });
  protected readonly err = firstError;
  protected readonly serverError = signal<string | null>(null);

  private seeded = false;

  constructor() {
    effect(() => {
      if (this.isNew() || this.seeded) {
        return;
      }
      const p = this.existing();
      if (!p) {
        return;
      }
      this.seeded = true;
      this.model.set({
        name: p.name ?? '',
        slug: p.slug ?? '',
        body: p.body ?? '',
        metaTitle: p.metaTitle ?? '',
        metaKeywords: p.metaKeywords ?? '',
        metaDescription: p.metaDescription ?? '',
        isPublished: p.isPublished,
        nameEn: p.nameEn ?? '',
        bodyEn: p.bodyEn ?? '',
        metaTitleEn: p.metaTitleEn ?? '',
        metaKeywordsEn: p.metaKeywordsEn ?? '',
        metaDescriptionEn: p.metaDescriptionEn ?? '',
      });
    });
  }

  protected onSubmit(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      this.serverError.set(null);
      const m = this.model();
      const body: PageUpsertRequestEn = {
        name: m.name,
        slug: m.slug || null,
        body: m.body || null,
        metaTitle: m.metaTitle || null,
        metaKeywords: m.metaKeywords || null,
        metaDescription: m.metaDescription || null,
        isPublished: m.isPublished,
        nameEn: m.nameEn || null,
        bodyEn: m.bodyEn || null,
        metaTitleEn: m.metaTitleEn || null,
        metaKeywordsEn: m.metaKeywordsEn || null,
        metaDescriptionEn: m.metaDescriptionEn || null,
      };
      try {
        if (this.isNew()) {
          await firstValueFrom(this.service.createPage(body));
          this.toast.success(this.translate.instant('pages.created_ok'));
        } else {
          await firstValueFrom(this.service.updatePage(this.pageId(), body));
          this.toast.success(this.translate.instant('pages.updated_ok'));
        }
        await this.router.navigate(['/pages']);
      } catch {
        this.serverError.set(this.translate.instant('pages.save_failed'));
      }
      return undefined;
    });
  }
}
