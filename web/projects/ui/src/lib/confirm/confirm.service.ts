import { inject, Injectable } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ConfirmDialog } from './confirm-dialog';

export interface ConfirmOptions {
  /** Dialog headline. Pass an already-translated string. */
  title: string;
  /** Body text. Pass an already-translated string. */
  message: string;
  /** Confirm button label (already translated). */
  okText: string;
  /** Cancel button label (already translated). */
  cancelText: string;
  /** `true` renders the confirm button as danger (deletes and other destructive acts). */
  destructive?: boolean;
}

/**
 * In-app replacement for `window.confirm()` — opens a styled ng-bootstrap
 * modal and resolves `true` only when the user explicitly confirms.
 *
 * @example
 * const ok = await this.confirmService.confirm({
 *   title: this.translate.instant('common.confirm_title'),
 *   message: this.translate.instant('products.confirm_delete', { name }),
 *   okText: this.translate.instant('common.delete'),
 *   cancelText: this.translate.instant('common.cancel'),
 *   destructive: true,
 * });
 * if (!ok) return;
 */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly modal = inject(NgbModal);

  async confirm(options: ConfirmOptions): Promise<boolean> {
    const ref = this.modal.open(ConfirmDialog, { centered: true, backdrop: 'static' });
    const dialog = ref.componentInstance as ConfirmDialog;
    dialog.title = options.title;
    dialog.message = options.message;
    dialog.okText = options.okText;
    dialog.cancelText = options.cancelText;
    dialog.destructive = options.destructive ?? false;

    try {
      return (await ref.result) === true;
    } catch {
      return false; // dismissed (backdrop/escape/close button)
    }
  }
}
