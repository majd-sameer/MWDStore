import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastHost } from 'ui';

/**
 * Root shell. Deliberately thin: it only hosts the router outlet and the global
 * toast host. The authenticated chrome (sidebar / topbar) lives in
 * `AdminLayout`, which wraps the guarded feature routes, so the login and
 * forbidden screens render without it.
 */
@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, ToastHost],
  templateUrl: './app.html',
})
export class App {}
