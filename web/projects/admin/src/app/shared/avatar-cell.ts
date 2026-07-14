import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';


@Component({
  selector: 'app-avatar-cell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="avatar-cell-media" [style.--avatar-hue]="hue()">
      @if (imageUrl() && !failed()) {
        <img class="avatar-cell-img" [src]="imageUrl()" alt="" loading="lazy" (error)="failed.set(true)" />
      } @else {
        <span class="avatar-cell-initials">{{ initials() }}</span>
      }
    </span>
    <span class="avatar-cell-text">
      <span class="avatar-cell-name">{{ name() || '—' }}</span>
      @if (sublabel()) {
        <span class="avatar-cell-sub">{{ sublabel() }}</span>
      }
    </span>
  `,
  host: { class: 'avatar-cell' },
})
export class AvatarCell {
  readonly name = input<string | null>('');
  readonly imageUrl = input<string | null>(null);
  readonly sublabel = input<string | null>(null);

  protected readonly failed = signal(false);

  protected readonly initials = computed(() => {
    const parts = (this.name() ?? '').trim().split(/\s+/).filter(Boolean);
    if (!parts.length) {
      return '—';
    }
    const first = parts[0][0] ?? '';
    const last = parts.length > 1 ? (parts[parts.length - 1][0] ?? '') : '';
    return (first + last).toUpperCase();
  });

  protected readonly hue = computed(() => {
    const name = this.name() ?? '';
    let hash = 0;
    for (let i = 0; i < name.length; i++) {
      hash = (hash * 31 + name.charCodeAt(i)) % 360;
    }
    return hash;
  });
}
