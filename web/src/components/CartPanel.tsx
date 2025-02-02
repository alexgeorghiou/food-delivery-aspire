import { Link } from 'react-router-dom';
import { useCart } from '../cart';
import { formatMoney, pricingPolicy } from '../pricing';

export function CartPanel({ showCheckout = true }: { showCheckout?: boolean }) {
  const cart = useCart();

  if (cart.lines.length === 0) {
    return (
      <aside className="card cart">
        <h2>Your basket</h2>
        <p className="muted">Add something tasty to get started.</p>
      </aside>
    );
  }

  const { totals } = cart;
  const toFreeDelivery = pricingPolicy.freeDeliveryThreshold - totals.subtotal;

  return (
    <aside className="card cart">
      <h2>Your basket</h2>
      <p className="muted">{cart.restaurantName}</p>
      <ul className="cart-lines">
        {cart.lines.map((line) => (
          <li key={line.menuItemId}>
            <span className="qty">
              <button type="button" aria-label={`Remove one ${line.name}`} onClick={() => cart.decrement(line.menuItemId)}>
                −
              </button>
              {line.quantity}
            </span>
            <span className="name">{line.name}</span>
            <span className="price">{formatMoney(line.unitPrice * line.quantity)}</span>
          </li>
        ))}
      </ul>
      <dl className="totals">
        <dt>Subtotal</dt>
        <dd>{formatMoney(totals.subtotal)}</dd>
        <dt>Delivery</dt>
        <dd>{totals.deliveryFee === 0 ? 'Free' : formatMoney(totals.deliveryFee)}</dd>
        {totals.smallOrderSurcharge > 0 && (
          <>
            <dt>Small order surcharge</dt>
            <dd>{formatMoney(totals.smallOrderSurcharge)}</dd>
          </>
        )}
        <dt>Service fee</dt>
        <dd>{formatMoney(totals.serviceFee)}</dd>
        <dt className="grand">Total</dt>
        <dd className="grand">{formatMoney(totals.total)}</dd>
      </dl>
      {toFreeDelivery > 0 && (
        <p className="hint">Add {formatMoney(toFreeDelivery)} more for free delivery.</p>
      )}
      {showCheckout && (
        <Link className="button primary block" to="/checkout">
          Go to checkout
        </Link>
      )}
    </aside>
  );
}
