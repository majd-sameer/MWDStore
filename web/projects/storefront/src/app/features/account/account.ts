import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  signal,
} from '@angular/core';
import { form, FormField as Control, submit } from '@angular/forms/signals';
import { RouterLink } from '@angular/router';
import { AuthService } from 'core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import {
  AccountService,
  OrderService,
  StorefrontFeaturesService,
  type RecentlyViewedDto,
} from 'data-access';
import { firstValueFrom } from 'rxjs';
import { Button, FormField, Icon, Tile, ToastService } from 'ui';
import { OrderCard } from './order-card';

interface ProfileModel {
  fullName: string;
  phoneNumber: string;
}

/**
 * Account hub: profile card (Signal Forms) beside recent orders rendered as
 * OrderCards with a tracking timeline. Copy keyed; layout logical.
 */
@Component({
  selector: 'app-account',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Control, FormField, TranslatePipe, Button, Icon, OrderCard, Tile],
  templateUrl: './account.html',
  styleUrl: './account.scss',
})
export class Account {
  private readonly account = inject(AccountService);
  private readonly auth = inject(AuthService);
  private readonly orderService = inject(OrderService);
  private readonly features = inject(StorefrontFeaturesService);
  private readonly toast = inject(ToastService);
  private readonly translate = inject(TranslateService);

  protected readonly profile = this.account.profileResource();
  protected readonly orders = this.orderService.ordersResource();
  protected readonly recentlyViewed = signal<RecentlyViewedDto[]>([]);

  protected readonly model = signal<ProfileModel>({ fullName: '', phoneNumber: '' });
  protected readonly f = form(this.model);

  private seeded = false;

  constructor() {
    effect(() => {
      const account = this.profile.value();
      if (account && !this.seeded) {
        this.seeded = true;
        this.model.set({
          fullName: account.fullName ?? '',
          phoneNumber: account.phoneNumber ?? '',
        });
      }
    });

    this.features.recentlyViewed(4).subscribe({
      next: (items) => this.recentlyViewed.set(items),
      error: () => this.recentlyViewed.set([]),
    });
  }

  protected onSave(event: Event): void {
    event.preventDefault();
    void submit(this.f, async () => {
      try {
        await firstValueFrom(this.account.updateProfile(this.model()));
        this.toast.success(this.translate.instant('account.saved'));
      } catch {
        this.toast.error(this.translate.instant('account.save_error'));
      }
      return undefined;
    });
  }

  protected onLogout(): void {
    this.auth.logout();
  }
}
