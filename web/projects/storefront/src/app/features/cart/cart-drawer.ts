import { MoneyPipe } from 'core';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { Button, Icon, Tile } from 'ui';
import { CartStore } from '../../core/cart.store';
import { CartDrawerService } from '../../core/cart-drawer.service';

/**
 * Slide-in bag drawer rendered in the app shell and opened from the header.
 * Anchored to the inline-end side so it mirrors correctly in RTL. Reads the
 * shared CartStore; copy is keyed.
 */
@Component({
  selector: 'app-cart-drawer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MoneyPipe, TranslatePipe, Button, Icon, Tile],
  templateUrl: './cart-drawer.html',
  styleUrl: './cart-drawer.scss',
})
export class CartDrawer {
  protected readonly store = inject(CartStore);
  protected readonly drawer = inject(CartDrawerService);

  protected readonly count = computed(() => this.store.count());
}
