import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgbToast } from '@ng-bootstrap/ng-bootstrap';
import { ToastService } from './toast.service';

/**
 * Renders every toast held in {@link ToastService} using ng-bootstrap's
 * `NgbToast`. Drop a single instance near the app root:
 *
 * @example
 * <lib-toast-host />
 */
@Component({
  selector: 'lib-toast-host',
  imports: [NgbToast],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'toast-container mystore-toast-container position-fixed top-0 end-0 p-3',
    'aria-live': 'polite',
    'aria-atomic': 'true',
  },
  template: `
    @for (toast of toastService.toasts(); track toast.id) {
      <ngb-toast
        [class]="toast.classname ?? ''"
        [header]="toast.header ?? ''"
        [autohide]="toast.autohide"
        [delay]="toast.delay"
        (hidden)="toastService.remove(toast.id)"
      >
        {{ toast.text }}
      </ngb-toast>
    }
  `,
})
export class ToastHost {
  protected readonly toastService = inject(ToastService);
}
