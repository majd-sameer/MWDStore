/**
 * Maps the API's numeric order-status code (Store.Application OrderStatus) to a
 * position on the storefront's four-step tracking timeline. We branch on the
 * stable code — never on the (localized) status name.
 *
 *   New(1) / OnHold(10) / PendingPayment(20) / PaymentReceived(30) /
 *   PaymentFailed(35) / Invoiced(40)            → Placed
 *   Shipping(50)                                 → Processing
 *   Shipped(60)                                  → Shipped
 *   Complete(70)                                 → Delivered
 *   Canceled(80) / Refunded(90) / Closed(100)    → cancelled (off-timeline)
 */
export type TrackStage = 'placed' | 'processing' | 'shipped' | 'delivered';

export const TRACK_STAGES: readonly TrackStage[] = [
  'placed',
  'processing',
  'shipped',
  'delivered',
];

export function isCancelled(code: number): boolean {
  return code >= 80;
}

/** Index into {@link TRACK_STAGES} of the furthest-reached stage. */
export function stageIndex(code: number): number {
  if (code >= 70 && code < 80) {
    return 3;
  }
  if (code === 60) {
    return 2;
  }
  if (code === 50) {
    return 1;
  }
  return 0;
}

/** i18n keys for each numeric order-status code (the translate pipe localizes them). */
export const STATUS_LABEL_KEYS: Record<number, string> = {
  1: 'tracking.events.new',
  10: 'tracking.events.on_hold',
  20: 'tracking.events.pending_payment',
  30: 'tracking.events.payment_received',
  35: 'tracking.events.payment_failed',
  40: 'tracking.events.invoiced',
  50: 'tracking.events.shipping',
  60: 'tracking.events.shipped',
  70: 'tracking.events.complete',
  80: 'tracking.events.canceled',
  90: 'tracking.events.refunded',
  100: 'tracking.events.closed',
};

/**
 * Label for a status code: the i18n key when known (the translate pipe localizes it),
 * otherwise the raw backend name (passing it through translate returns it unchanged).
 */
export function statusLabel(code: number, fallback: string | null): string {
  return STATUS_LABEL_KEYS[code] ?? fallback ?? '';
}
