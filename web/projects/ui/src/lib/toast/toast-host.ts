import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgbToast } from '@ng-bootstrap/ng-bootstrap';
import { ToastService } from './toast.service';


@Component({
  selector: 'lib-toast-host',
  imports: [NgbToast],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'toast-container mystore-toast-container position-fixed bottom-0 end-0 p-3',
    'aria-live': 'polite',
    'aria-atomic': 'true',
  },
  templateUrl: './toast-host.html',
})
export class ToastHost {
  protected readonly toastService = inject(ToastService);
}
