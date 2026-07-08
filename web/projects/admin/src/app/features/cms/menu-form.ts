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
  AdminCmsService,
  type AdminMenuItemDto,
} from 'data-access';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { Button, ToastService } from 'ui';
import { PageHeader } from '../../shared/page-header';
import { MultiLangInput, type MultiLangValue } from '../../shared/multi-lang-input';

/**
 * Create / edit a navigation menu on its own page. The menus API has no
 * single-fetch endpoint, so edit mode seeds from the list resource (the list
 * DTO carries the menu's items). Items are managed inline once the menu exists;
 * creating a new menu lands you on its edit page so you can add them.
 */
@Component({
  selector: 'app-admin-menu-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, RouterLink, TranslatePipe, PageHeader, MultiLangInput],
  templateUrl: './menu-form.html',
})
export class AdminMenuForm {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly service = inject(AdminCmsService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  private readonly idParam = toSignal(this.route.paramMap, {
    initialValue: this.route.snapshot.paramMap,
  });
  protected readonly isNew = computed(() => this.idParam().get('id') === 'new');
  private readonly menuId = computed(() => Number(this.idParam().get('id')));

  protected readonly list = this.service.menusResource();
  protected readonly existing = computed(
    () => this.list.value()?.find((m) => m.id === this.menuId()) ?? null,
  );

  protected readonly name = signal<MultiLangValue>({ ar: '', en: '' });
  protected readonly isPublished = signal(true);
  protected readonly saving = signal(false);

  /** Bilingual label for the "add item" row (link/order stay native inputs). */
  protected readonly newItemName = signal<MultiLangValue>({ ar: '', en: '' });

  private seeded = false;

  constructor() {
    effect(() => {
      if (this.isNew() || this.seeded) {
        return;
      }
      const m = this.existing();
      if (!m) {
        return;
      }
      this.seeded = true;
      this.name.set({ ar: m.name ?? '', en: m.nameEn ?? '' });
      this.isPublished.set(m.isPublished);
    });
  }

  protected save(): void {
    const value = this.name();
    const name = value.ar.trim();
    if (!name) {
      this.toast.error(this.translate.instant('common.name_required'));
      return;
    }
    const body = { name, nameEn: value.en || null, isPublished: this.isPublished() };
    this.saving.set(true);
    if (this.isNew()) {
      this.service.createMenu(body).subscribe({
        next: (menu) => {
          this.toast.success(this.translate.instant('menus.created_ok'));
          this.saving.set(false);
          void this.router.navigate(['/menus', menu.id]);
        },
        error: () => {
          this.toast.error(this.translate.instant('menus.create_failed'));
          this.saving.set(false);
        },
      });
    } else {
      this.service.updateMenu(this.menuId(), body).subscribe({
        next: () => {
          this.toast.success(this.translate.instant('menus.updated_ok'));
          this.saving.set(false);
          void this.router.navigate(['/menus']);
        },
        error: () => {
          this.toast.error(this.translate.instant('menus.update_failed'));
          this.saving.set(false);
        },
      });
    }
  }

  protected addItem(
    menuId: number,
    link: HTMLInputElement,
    order: HTMLInputElement,
  ): void {
    const value = this.newItemName();
    const label = value.ar.trim();
    if (!label) {
      return;
    }
    this.service
      .addMenuItem(menuId, {
        name: label,
        nameEn: value.en || null,
        customLink: link.value.trim() || null,
        displayOrder: Number(order.value) || 0,
      })
      .subscribe({
        next: () => {
          this.newItemName.set({ ar: '', en: '' });
          link.value = '';
          order.value = '0';
          this.list.reload();
        },
        error: () => this.toast.error(this.translate.instant('menus.item_add_failed')),
      });
  }

  protected updateItem(
    menuId: number,
    item: AdminMenuItemDto,
    patch: Partial<{ name: MultiLangValue; customLink: string; displayOrder: number }>,
  ): void {
    this.service
      .updateMenuItem(menuId, item.id, {
        name: patch.name?.ar ?? item.name ?? '',
        nameEn: patch.name ? patch.name.en || null : item.nameEn ?? null,
        customLink: patch.customLink ?? item.customLink,
        parentId: item.parentId,
        displayOrder: patch.displayOrder ?? item.displayOrder,
      })
      .subscribe({
        next: () => this.list.reload(),
        error: () => this.toast.error(this.translate.instant('menus.item_update_failed')),
      });
  }

  protected removeItem(menuId: number, item: AdminMenuItemDto): void {
    this.service.deleteMenuItem(menuId, item.id).subscribe({
      next: () => this.list.reload(),
      error: () => this.toast.error(this.translate.instant('menus.item_delete_failed')),
    });
  }
}
