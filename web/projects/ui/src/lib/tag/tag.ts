import { ChangeDetectionStrategy, Component, input } from '@angular/core';


@Component({
  selector: 'lib-tag',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'ui-tag', '[attr.data-tone]': 'tone()' },
  templateUrl: './tag.html',
})
export class Tag {
  readonly tone = input<'muted' | 'accent' | 'indigo' | 'success' | 'danger'>(
    'muted',
  );
}
