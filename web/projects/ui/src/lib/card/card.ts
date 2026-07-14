import { ChangeDetectionStrategy, Component, input } from '@angular/core';


@Component({
  selector: 'lib-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'card',
  },
  templateUrl: './card.html',
})
export class Card {
  readonly header = input<string | null>(null);
  readonly title = input<string | null>(null);
  readonly footer = input<string | null>(null);
}
