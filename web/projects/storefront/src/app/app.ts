import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastHost } from 'ui';
import { Header } from './layout/header';
import { Footer } from './layout/footer';
import { NewsAlertToast } from './layout/news-alert-toast';
import { CartDrawer } from './features/cart/cart-drawer';
import { BottomNav } from './layout/bottom-nav';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, ToastHost, Header, Footer, CartDrawer, NewsAlertToast, BottomNav],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {}
