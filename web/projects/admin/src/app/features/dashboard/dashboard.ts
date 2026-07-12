import { DatePipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { LanguageService, MoneyPipe } from 'core';
import { AdminDashboardService } from 'data-access';
import type { ChartData, ChartOptions } from 'chart.js';
import { BaseChartDirective } from 'ng2-charts';
import { Icon } from 'ui';
import { orderStatusBadge } from '../../shared/order-status';
import { PageHeader } from '../../shared/page-header';

/** Chart palette (kept in sync with the admin brand tokens). */
const PALETTE = ['#1f3a5f', '#2e7d52', '#c8a24c', '#b0492c', '#3b7a8c', '#7d5ba6', '#d9a441', '#5c6b7a'];
const C = {
  green: '#2e7d52',
  greenSoft: 'rgba(46, 125, 82, 0.16)',
  navy: '#1f3a5f',
  navySoft: 'rgba(31, 58, 95, 0.75)',
  danger: '#b0492c',
  warning: '#d9a441',
  success: '#2e7d52',
};

/**
 * Admin landing page: a decision-maker overview backed by a single aggregate
 * endpoint (`/api/admin/dashboard/stats`). Headline KPIs, a revenue/orders
 * trend, the order-status funnel, payment & channel mix, current stock health,
 * best sellers, and two work queues (low stock + orders needing action).
 */
@Component({
  selector: 'app-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MoneyPipe,
    DecimalPipe,
    DatePipe,
    RouterLink,
    Icon,
    TranslatePipe,
    PageHeader,
    BaseChartDirective,
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly dashboard = inject(AdminDashboardService);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService);

  protected readonly periods = [7, 30, 90] as const;
  protected readonly days = signal(30);

  protected readonly stats = this.dashboard.statsResource(() => this.days());
  protected readonly kpis = computed(() => this.stats.value()?.kpis);

  protected readonly badge = orderStatusBadge;

  /** Re-translate chart labels when the language flips. */
  private t(key: string): string {
    this.language.lang();
    return this.translate.instant(key);
  }

  /**
   * Human label for a payment-method id (`CoD`, `Stripe`, `(none)`, …).
   * Falls back to the raw id when no `payment_methods.*` key exists for it.
   */
  private paymentLabel(name: string): string {
    const key = 'payment_methods.' + (name === '(none)' ? 'none' : name);
    const label = this.t(key);
    return label === key ? name : label;
  }

  // ----- Revenue & orders trend (combo: revenue line + orders bars) -----
  protected readonly trendData = computed<ChartData<'bar'>>(() => {
    const points = this.stats.value()?.revenueTrend ?? [];
    return {
      labels: points.map((p) => p.date.slice(5)),
      datasets: [
        {
          type: 'line',
          label: this.t('dashboard.series_revenue'),
          data: points.map((p) => p.revenue),
          yAxisID: 'y',
          borderColor: C.green,
          backgroundColor: C.greenSoft,
          fill: true,
          tension: 0.35,
          pointRadius: 2,
          order: 0,
        } as never,
        {
          type: 'bar',
          label: this.t('dashboard.series_orders'),
          data: points.map((p) => p.orders),
          yAxisID: 'y1',
          backgroundColor: C.navySoft,
          borderRadius: 4,
          maxBarThickness: 22,
          order: 1,
        } as never,
      ],
    };
  });

  protected readonly trendOptions: ChartOptions<'bar'> = {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index', intersect: false },
    plugins: { legend: { position: 'bottom' } },
    scales: {
      y: { position: 'left', beginAtZero: true, title: { display: false } },
      y1: {
        position: 'right',
        beginAtZero: true,
        grid: { drawOnChartArea: false },
        ticks: { precision: 0 },
      },
    },
  };

  // ----- Order-status funnel (horizontal bars) -----
  protected readonly statusData = computed<ChartData<'bar'>>(() => {
    const slices = this.stats.value()?.statusFunnel ?? [];
    return {
      labels: slices.map((s) => this.t('orders.status_' + s.status)),
      datasets: [
        {
          label: this.t('dashboard.series_orders'),
          data: slices.map((s) => s.count),
          backgroundColor: slices.map((_, i) => PALETTE[i % PALETTE.length]),
          borderRadius: 4,
        },
      ],
    };
  });

  // ----- Best sellers (horizontal bars by units) -----
  protected readonly topProductsData = computed<ChartData<'bar'>>(() => {
    const top = this.stats.value()?.topProducts ?? [];
    return {
      labels: top.map((p) => (p.name.length > 28 ? p.name.slice(0, 27) + '…' : p.name)),
      datasets: [
        {
          label: this.t('dashboard.series_units'),
          data: top.map((p) => p.units),
          backgroundColor: C.green,
          borderRadius: 4,
        },
      ],
    };
  });

  protected readonly horizontalBarOptions: ChartOptions<'bar'> = {
    indexAxis: 'y',
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: { x: { beginAtZero: true, ticks: { precision: 0 } } },
  };

  // ----- Payment mix (doughnut) -----
  protected readonly paymentData = computed<ChartData<'doughnut'>>(() => {
    const mix = this.stats.value()?.paymentMix ?? [];
    return {
      labels: mix.map((m) => this.paymentLabel(m.name)),
      datasets: [
        { data: mix.map((m) => m.count), backgroundColor: mix.map((_, i) => PALETTE[i % PALETTE.length]) },
      ],
    };
  });

  // ----- Channel mix (doughnut) -----
  protected readonly channelData = computed<ChartData<'doughnut'>>(() => {
    const ch = this.stats.value()?.channelMix;
    return {
      labels: [this.t('dashboard.channel_account'), this.t('dashboard.channel_guest')],
      datasets: [
        {
          data: [ch?.account ?? 0, ch?.guest ?? 0],
          backgroundColor: [C.navy, C.warning],
        },
      ],
    };
  });

  // ----- Stock health (doughnut) -----
  protected readonly stockData = computed<ChartData<'doughnut'>>(() => {
    const s = this.stats.value()?.stockHealth;
    return {
      labels: [
        this.t('dashboard.stock_healthy'),
        this.t('dashboard.stock_low'),
        this.t('dashboard.stock_out'),
      ],
      datasets: [
        {
          data: [s?.healthy ?? 0, s?.low ?? 0, s?.outOfStock ?? 0],
          backgroundColor: [C.success, C.warning, C.danger],
        },
      ],
    };
  });

  protected readonly doughnutOptions: ChartOptions<'doughnut'> = {
    responsive: true,
    maintainAspectRatio: false,
    cutout: '58%',
    plugins: { legend: { position: 'bottom' } },
  };
}
