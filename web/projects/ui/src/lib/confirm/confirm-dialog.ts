import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';

/**
 * The modal body rendered by {@link ConfirmService}. Not exported for direct
 * use — open it through the service so every confirmation looks the same.
 */
@Component({
  selector: 'lib-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="modal-header border-0 pb-0">
      <h5 class="modal-title d-flex align-items-center gap-2">
        <span
          class="confirm-icon"
          [class.confirm-icon-danger]="destructive"
          aria-hidden="true"
        >
          <i class="bi" [class]="destructive ? 'bi-trash3' : 'bi-question-lg'"></i>
        </span>
        {{ title }}
      </h5>
      <button
        type="button"
        class="btn-close"
        [attr.aria-label]="cancelText"
        (click)="modal.dismiss()"
      ></button>
    </div>
    <div class="modal-body pt-2 text-body-secondary">{{ message }}</div>
    <div class="modal-footer border-0 pt-0">
      <button type="button" class="btn btn-outline-secondary" (click)="modal.dismiss()">
        {{ cancelText }}
      </button>
      <button
        type="button"
        class="btn"
        [class.btn-danger]="destructive"
        [class.btn-primary]="!destructive"
        (click)="modal.close(true)"
      >
        {{ okText }}
      </button>
    </div>
  `,
  styles: `
    .confirm-icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      inline-size: 2rem;
      block-size: 2rem;
      border-radius: 50%;
      background: var(--bs-primary-bg-subtle, #e7f1ff);
      color: var(--bs-primary, #0d6efd);
      font-size: 1rem;
      flex-shrink: 0;
    }
    .confirm-icon-danger {
      background: var(--bs-danger-bg-subtle, #f8d7da);
      color: var(--bs-danger, #dc3545);
    }
  `,
})
export class ConfirmDialog {
  protected readonly modal = inject(NgbActiveModal);

  title = '';
  message = '';
  okText = 'OK';
  cancelText = 'Cancel';
  destructive = false;
}
