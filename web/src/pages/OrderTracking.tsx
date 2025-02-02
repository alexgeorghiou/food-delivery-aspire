import { useState } from 'react';
import { useParams } from 'react-router-dom';
import { api, ApiError } from '../api';
import { StatusTimeline } from '../components/StatusTimeline';
import { usePolling } from '../hooks';
import { formatMoney } from '../pricing';
import type { Order, OrderActor, OrderStatus } from '../types';

const POLL_INTERVAL_MS = 3000;
const terminal: OrderStatus[] = ['Delivered', 'Cancelled', 'Rejected'];

// Who would press each button in real life. The demo panel lets one person play every role.
const actorFor: Partial<Record<OrderStatus, OrderActor>> = {
  Accepted: 'Restaurant',
  Rejected: 'Restaurant',
  Preparing: 'Restaurant',
  ReadyForPickup: 'Restaurant',
  PickedUp: 'Courier',
  Delivered: 'Courier',
};

export function OrderTracking() {
  const { id = '' } = useParams();
  const [actionError, setActionError] = useState<string | null>(null);
  const { data: order, error, refresh } = usePolling<Order>(
    () => api.getOrder(id),
    POLL_INTERVAL_MS,
    (o) => terminal.includes(o.status),
    [id],
  );

  if (error && !order) return <p className="error">Could not load order: {error.message}</p>;
  if (!order) return <p className="muted">Loading order…</p>;

  const act = async (run: () => Promise<Order>) => {
    setActionError(null);
    try {
      await run();
      refresh();
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : 'Action failed');
    }
  };

  const demoActions = order.nextStatuses.filter((s) => actorFor[s]);
  const canCancel = order.status === 'Placed' || order.status === 'Accepted';

  return (
    <div className="layout-with-cart">
      <section>
        <h1>Order from {order.restaurantName}</h1>
        <p className="muted">
          #{order.id.slice(0, 8)} · for {order.customerName} · {order.deliveryAddress}
        </p>

        <StatusTimeline status={order.status} history={order.history} />

        {order.courier && (
          <p className="card courier">
            🚲 <strong>{order.courier.name}</strong> is your courier.
          </p>
        )}
        {order.status === 'ReadyForPickup' && <p className="muted">Finding the nearest available courier…</p>}

        {!terminal.includes(order.status) && (
          <div className="card demo-panel">
            <h2>Demo controls</h2>
            <p className="muted small">Move the order along as the restaurant or courier would.</p>
            <div className="actions">
              {demoActions.map((status) => (
                <button
                  key={status}
                  type="button"
                  className="button"
                  onClick={() => act(() => api.changeStatus(order.id, status, actorFor[status]!))}
                >
                  {actorFor[status]}: {status.replace(/([a-z])([A-Z])/g, '$1 $2')}
                </button>
              ))}
              {canCancel && (
                <button type="button" className="button danger" onClick={() => act(() => api.cancelOrder(order.id))}>
                  Cancel order
                </button>
              )}
            </div>
            {actionError && <p className="error">{actionError}</p>}
          </div>
        )}
      </section>

      <aside className="card cart">
        <h2>Receipt</h2>
        <ul className="cart-lines">
          {order.lines.map((l) => (
            <li key={l.menuItemId}>
              <span className="qty">{l.quantity}×</span>
              <span className="name">{l.name}</span>
              <span className="price">{formatMoney(l.lineTotal)}</span>
            </li>
          ))}
        </ul>
        <dl className="totals">
          <dt>Subtotal</dt>
          <dd>{formatMoney(order.totals.subtotal)}</dd>
          <dt>Delivery</dt>
          <dd>{order.totals.deliveryFee === 0 ? 'Free' : formatMoney(order.totals.deliveryFee)}</dd>
          {order.totals.smallOrderSurcharge > 0 && (
            <>
              <dt>Small order surcharge</dt>
              <dd>{formatMoney(order.totals.smallOrderSurcharge)}</dd>
            </>
          )}
          <dt>Service fee</dt>
          <dd>{formatMoney(order.totals.serviceFee)}</dd>
          <dt className="grand">Total</dt>
          <dd className="grand">{formatMoney(order.totals.total)}</dd>
        </dl>
      </aside>
    </div>
  );
}
