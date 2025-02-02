import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api, ApiError } from '../api';
import { useCart } from '../cart';
import { CartPanel } from '../components/CartPanel';
import { deliveryPresets, useDeliveryLocation } from '../location';

export function Checkout() {
  const cart = useCart();
  const navigate = useNavigate();
  const { location, setLocation } = useDeliveryLocation();
  const [customerName, setCustomerName] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!cart.restaurantId || cart.lines.length === 0) {
    return (
      <section>
        <h1>Checkout</h1>
        <p className="muted">
          Your basket is empty. <Link to="/">Browse restaurants</Link>.
        </p>
      </section>
    );
  }

  const restaurantId = cart.restaurantId;

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const order = await api.placeOrder({
        restaurantId,
        customerName: customerName.trim(),
        deliveryAddress: location.address,
        deliveryLatitude: location.latitude,
        deliveryLongitude: location.longitude,
        lines: cart.lines.map((l) => ({ menuItemId: l.menuItemId, quantity: l.quantity })),
      });
      cart.clear();
      navigate(`/orders/${order.id}`);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Something went wrong placing your order.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="layout-with-cart">
      <section>
        <h1>Checkout</h1>
        <form className="card form" onSubmit={submit}>
          <label>
            Your name
            <input
              required
              maxLength={120}
              value={customerName}
              onChange={(e) => setCustomerName(e.target.value)}
              placeholder="Ada Lovelace"
            />
          </label>
          <label>
            Deliver to
            <select
              value={location.label}
              onChange={(e) => setLocation(deliveryPresets.find((p) => p.label === e.target.value) ?? location)}
            >
              {deliveryPresets.map((p) => (
                <option key={p.label} value={p.label}>
                  {p.label} — {p.address}
                </option>
              ))}
            </select>
          </label>
          <p className="muted small">
            Prices and fees are confirmed by the restaurant when the order is placed. You pay the courier on delivery.
          </p>
          {error && <p className="error">{error}</p>}
          <button className="button primary" type="submit" disabled={submitting || customerName.trim() === ''}>
            {submitting ? 'Placing order…' : 'Place order'}
          </button>
        </form>
      </section>
      <CartPanel showCheckout={false} />
    </div>
  );
}
