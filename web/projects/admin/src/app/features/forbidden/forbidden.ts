import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from 'core';

/** Shown when an authenticated user lacks the `Admin` role (roleGuard target). */
@Component({
  selector: 'app-forbidden',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, TranslatePipe],
  templateUrl: './forbidden.html',
})
export class Forbidden {
  private readonly auth = inject(AuthService);

  protected signOut(): void {
    this.auth.logout();
  }
}
