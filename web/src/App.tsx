import { Link, NavLink, Route, Routes } from 'react-router-dom';
import { useCart } from './cart';
import { deliveryPresets, useDeliveryLocation } from './location';
import { Checkout } from './pages/Checkout';
import { OrderTracking } from './pages/OrderTracking';
import { RestaurantList } from './pages/RestaurantList';
import { RestaurantMenu } from './pages/RestaurantMenu';

function Header() {
  const { itemCount } = useCart();
  const { location, setLocation } = useDeliveryLocation();

  return (
    <header className="app-header">
      <Link to="/" className="brand">
        <img src="/favicon.svg" alt="" width={28} height={28} />
        Food Delivery
      </Link>
      <label className="location-picker">
        <span className="muted small">Deliver to</span>
        <select
          value={location.label}
          onChange={(e) => setLocation(deliveryPresets.find((p) => p.label === e.target.value) ?? location)}
        >
          {deliveryPresets.map((p) => (
            <option key={p.label}>{p.label}</option>
          ))}
        </select>
      </label>
      <NavLink to="/checkout" className="basket-link">
        Basket{itemCount > 0 && <span className="count">{itemCount}</span>}
      </NavLink>
    </header>
  );
}

export function App() {
  return (
    <>
      <Header />
      <main className="container">
        <Routes>
          <Route path="/" element={<RestaurantList />} />
          <Route path="/restaurants/:id" element={<RestaurantMenu />} />
          <Route path="/checkout" element={<Checkout />} />
          <Route path="/orders/:id" element={<OrderTracking />} />
          <Route path="*" element={<p>Page not found. <Link to="/">Back to restaurants</Link></p>} />
        </Routes>
      </main>
    </>
  );
}
