import {
  booleanAttribute,
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core';

export type ButtonVariant =
  | 'primary'
  | 'secondary'
  | 'success'
  | 'danger'
  | 'warning'
  | 'info'
  | 'light'
  | 'dark'
  | 'link';

export type ButtonSize = 'sm' | 'lg';


@Component({
  selector: 'button[libButton], a[libButton]',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './button.html',
  host: {
    '[class]': 'classes()',
  },
})
export class Button {
  readonly variant = input<ButtonVariant>('primary');
  readonly size = input<ButtonSize | null>(null);
  readonly outline = input(false, { transform: booleanAttribute });
  readonly block = input(false, { transform: booleanAttribute });

  protected readonly classes = computed(() => {
    const variant = this.variant();
    const tokens = ['btn', `btn-${this.outline() ? 'outline-' : ''}${variant}`];

    const size = this.size();
    if (size) {
      tokens.push(`btn-${size}`);
    }
    if (this.block()) {
      tokens.push('w-100');
    }
    return tokens.join(' ');
  });
}
