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
  template: `
    <h1 class="page-title">{{ 'account.title' | translate }}</h1>

    <div class="account">
      <section class="profile-col">
        <div class="card-surface">
          <h2 class="block-title">{{ 'account.profile' | translate }}</h2>

          @if (profile.isLoading()) {
            <div class="spinner-border text-primary" role="status">
              <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
            </div>
          } @else if (profile.value(); as account) {
            <p class="signed-in">
              {{ 'account.signed_in_as' | translate }} <strong>{{ account.email }}</strong>
            </p>

            <form (submit)="onSave($event)" novalidate>
              <lib-form-field [label]="'account.full_name' | translate" controlId="fullName">
                <input id="fullName" class="form-control" dir="auto" [formField]="f.fullName" />
              </lib-form-field>
              <lib-form-field [label]="'account.phone' | translate" controlId="phone">
                <input id="phone" class="form-control" dir="auto" [formField]="f.phoneNumber" />
              </lib-form-field>
              <button libButton class="mt-3" variant="dark" [disabled]="f().submitting()">
                {{ (f().submitting() ? 'account.saving' : 'account.save') | translate }}
              </button>
            </form>
          } @else {
            <div class="alert alert-warning mb-0">{{ 'account.profile_error' | translate }}</div>
          }
        </div>

        <div class="card-surface mt-3">
          <a class="quick-link" routerLink="/account/wishlist">
            ♡ {{ 'wishlist.title' | translate }}
          </a>
        </div>

        <button libButton variant="danger" [outline]="true" class="logout-btn mt-3"
          type="button" (click)="onLogout()">
          ⏻ {{ 'account.logout' | translate }}
        </button>

        @if (recentlyViewed().length) {
          <div class="card-surface mt-3">
            <h2 class="block-title">{{ 'recently.title' | translate }}</h2>
            <div class="rv-list">
              @for (rv of recentlyViewed(); track rv.productId) {
                <a class="rv-item" [routerLink]="['/products', rv.productId]">
                  <lib-tile [src]="rv.thumbnailUrl" [seed]="rv.name ?? rv.productId"
                    [alt]="rv.name" ratio="1x1" />
                  <span class="rv-name">{{ rv.name }}</span>
                </a>
              }
            </div>
          </div>
        }
      </section>

      <section class="orders-col">
        <div class="orders-head">
          <h2 class="block-title mb-0">{{ 'account.recent_orders' | translate }}</h2>
          <a class="orders-all" routerLink="/account/orders">
            {{ 'account.view_all_orders' | translate }} <lib-icon name="arrowEnd" [size]="15" />
          </a>
        </div>

        @if (orders.isLoading()) {
          <div class="spinner-border text-primary" role="status">
            <span class="visually-hidden">{{ 'common.loading' | translate }}</span>
          </div>
        } @else if (orders.error()) {
          <div class="alert alert-danger">{{ 'account.orders_error' | translate }}</div>
        } @else if (orders.value(); as list) {
          @if (list.length) {
            <div class="order-list">
              @for (order of list.slice(0, 4); track order.id) {
                <app-order-card [order]="order" />
              }
            </div>
          } @else {
            <div class="empty">
              <p class="text-body-secondary">{{ 'account.no_orders' | translate }}</p>
              <a libButton variant="dark" routerLink="/shop">{{ 'account.start_shopping' | translate }}</a>
            </div>
          }
        }
      </section>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
    .page-title {
      font-weight: 700;
      font-size: clamp(2rem, 4vw, 2.75rem);
      letter-spacing: -0.02em;
      margin-block: 1rem 2rem;
    }
    .account {
      display: grid;
      grid-template-columns: 340px 1fr;
      gap: 2.5rem;
      align-items: start;
    }
    @media (max-width: 900px) {
      .account {
        grid-template-columns: 1fr;
      }
    }
    .card-surface {
      background: var(--surface);
      border: 1px solid var(--line);
      border-radius: var(--r-lg);
      padding: 1.5rem;
      overflow: hidden;
    }
    .block-title {
      font-size: 1.1rem;
      font-weight: 700;
      margin-block-end: 1rem;
    }
    .signed-in {
      color: var(--ink-2);
      margin-block-end: 1.25rem;
    }
    /* Theme the profile form to match the rest of the storefront (not raw Bootstrap). */
    .profile-col form .form-control {
      border: 1.5px solid var(--line);
      border-radius: var(--r-sm);
      padding: 0.7rem 0.9rem;
      background: var(--surface);
      color: var(--ink);
    }
    .profile-col form .form-control:focus {
      border-color: var(--navy);
      box-shadow: none;
    }
    .profile-col form button {
      width: 100%;
      margin-block-start: 0.25rem;
    }
    .orders-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-block-end: 1.25rem;
    }
    .orders-all {
      display: inline-flex;
      align-items: center;
      gap: 0.3rem;
      color: var(--ink-2);
      font-weight: 600;
      text-decoration: none;
    }
    .orders-all:hover {
      color: var(--accent);
    }
    .order-list {
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }
    .empty {
      text-align: center;
      padding-block: 2.5rem;
    }
    .quick-link {
      display: block;
      padding-block: 0.4rem;
      font-weight: 600;
      text-decoration: none;
      color: var(--ink, inherit);
    }
    .quick-link:hover {
      color: var(--accent);
    }
    .logout-btn {
      width: 100%;
    }
    .rv-list {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 0.6rem;
    }
    .rv-item {
      text-decoration: none;
      color: var(--ink, inherit);
    }
    .rv-name {
      display: block;
      font-size: 0.75rem;
      margin-block-start: 0.25rem;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  `,
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
