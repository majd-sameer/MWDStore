import { Injectable, signal } from '@angular/core';

export interface Toast {
  readonly id: number;
  readonly text: string;
  readonly header?: string;
  /** Extra classes for the toast, e.g. `'bg-success text-light'`. */
  readonly classname?: string;
  readonly autohide: boolean;
  readonly delay: number;
}

export type ToastOptions = Partial<Omit<Toast, 'id' | 'text'>>;

/**
 * App-wide toast store. Components/services push toasts here; a single
 * `<lib-toast-host />` renders them. State is a signal so it works under
 * zoneless change detection.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private sequence = 0;
  readonly toasts = signal<readonly Toast[]>([]);

  show(text: string, options: ToastOptions = {}): number {
    const id = ++this.sequence;
    const toast: Toast = {
      autohide: true,
      delay: 5000,
      ...options,
      id,
      text,
    };
    this.toasts.update((list) => [...list, toast]);
    return id;
  }

  success(text: string, header?: string): number {
    return this.show(text, { header, classname: 'bg-success text-light' });
  }

  error(text: string, header?: string): number {
    return this.show(text, {
      header,
      classname: 'bg-danger text-light',
      autohide: false,
    });
  }

  remove(id: number): void {
    this.toasts.update((list) => list.filter((toast) => toast.id !== id));
  }

  clear(): void {
    this.toasts.set([]);
  }
}
