
export const ORDER_STATUS = {
  New: 1,
  OnHold: 10,
  PendingPayment: 20,
  PaymentReceived: 30,
  PaymentFailed: 35,
  Invoiced: 40,
  Shipping: 50,
  Shipped: 60,
  Complete: 70,
  Canceled: 80,
  Refunded: 90,
  Closed: 100,
} as const;

export interface OrderStatusOption {
  value: number;
  label: string;
}

export const ORDER_STATUS_OPTIONS: readonly OrderStatusOption[] = [
  { value: ORDER_STATUS.New, label: 'New' },
  { value: ORDER_STATUS.OnHold, label: 'On hold' },
  { value: ORDER_STATUS.PendingPayment, label: 'Pending payment' },
  { value: ORDER_STATUS.PaymentReceived, label: 'Payment received' },
  { value: ORDER_STATUS.PaymentFailed, label: 'Payment failed' },
  { value: ORDER_STATUS.Invoiced, label: 'Invoiced' },
  { value: ORDER_STATUS.Shipping, label: 'Shipping' },
  { value: ORDER_STATUS.Shipped, label: 'Shipped' },
  { value: ORDER_STATUS.Complete, label: 'Complete' },
  { value: ORDER_STATUS.Canceled, label: 'Canceled' },
  { value: ORDER_STATUS.Refunded, label: 'Refunded' },
  { value: ORDER_STATUS.Closed, label: 'Closed' },
];

export function orderStatusTone(status: number): import('./status-pill').StatusTone {
  switch (status) {
    case ORDER_STATUS.Complete:
    case ORDER_STATUS.PaymentReceived:
    case ORDER_STATUS.Shipped:
      return 'success';
    case ORDER_STATUS.Shipping:
    case ORDER_STATUS.Invoiced:
      return 'info';
    case ORDER_STATUS.PaymentFailed:
    case ORDER_STATUS.Canceled:
      return 'danger';
    case ORDER_STATUS.Refunded:
      return 'warning';
    default:
      return 'neutral';
  }
}

export function orderStatusBadge(status: number): string {
  switch (status) {
    case ORDER_STATUS.Complete:
    case ORDER_STATUS.PaymentReceived:
    case ORDER_STATUS.Shipped:
      return 'text-bg-success';
    case ORDER_STATUS.Shipping:
    case ORDER_STATUS.Invoiced:
      return 'text-bg-info';
    case ORDER_STATUS.New:
    case ORDER_STATUS.PendingPayment:
    case ORDER_STATUS.OnHold:
      return 'text-bg-secondary';
    case ORDER_STATUS.PaymentFailed:
    case ORDER_STATUS.Canceled:
      return 'text-bg-danger';
    case ORDER_STATUS.Refunded:
      return 'text-bg-warning';
    default:
      return 'text-bg-light';
  }
}
