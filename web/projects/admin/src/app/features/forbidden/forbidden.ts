import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from 'core';

/** Shown when an authenticated user lacks the `Admin` role (roleGuard target). */
@Component({
  selector: 'app-forbidden',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe],
  template: `
    <div class="min-vh-100 d-flex align-items-center bg-body-tertiary">
      <div class="container text-center">
        <p class="display-1 fw-semibold text-body-secondary mb-0">403</p>
        <h1 class="h4 mb-3">{{ 'forbidden.title' | translate }}</h1>
        <p class="text-body-secondary mb-4">
          {{ 'forbidden.message' | translate }}
        </p>
        <button
          type="button"
          class="btn btn-outline-secondary"
          (click)="signOut()"
        >
          {{ 'forbidden.signin_other' | translate }}
        </button>
        <a routerLink="/" class="btn btn-link">{{ 'forbidden.back_dashboard' | translate }}</a>
      </div>
    </div>
  `,
})
export class Forbidden {
  private readonly auth = inject(AuthService);

  protected signOut(): void {
    this.auth.logout();
  }
}
