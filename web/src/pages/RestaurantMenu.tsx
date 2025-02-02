import { useParams } from 'react-router-dom';
import { api } from '../api';
import { useCart } from '../cart';
import { CartPanel } from '../components/CartPanel';
import { useAsync } from '../hooks';
import { formatMoney } from '../pricing';
import type { DayOfWeek } from '../types';

const dayOrder: DayOfWeek[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

export function RestaurantMenu() {
  const { id = '' } = useParams();
  const cart = useCart();
  const details = useAsync(() => api.getRestaurant(id), [id]);
  const menu = useAsync(() => api.getMenu(id), [id]);

  if (details.error || menu.error) {
    return <p className="error">Could not load this restaurant: {(details.error ?? menu.error)?.message}</p>;
  }
  if (!details.data || !menu.data) return <p className="muted">Loading menu…</p>;

  const restaurant = details.data.summary;
  const hours = [...details.data.openingHours].sort(
    (a, b) => dayOrder.indexOf(a.day) - dayOrder.indexOf(b.day) || a.opens.localeCompare(b.opens),
  );

  return (
    <div className="layout-with-cart">
      <section>
        <header className="restaurant-header">
          <h1>{restaurant.name}</h1>
          <p className="muted">
            {restaurant.cuisine} · {restaurant.address}
          </p>
          {!restaurant.isOpen && (
            <p className="warning">Currently closed — you can browse the menu but ordering is disabled.</p>
          )}
          <details>
            <summary>Opening hours ({details.data.timeZoneId})</summary>
            <ul className="hours">
              {hours.map((p) => (
                <li key={`${p.day}-${p.opens}`}>
                  <span>{p.day}</span>
                  <span>
                    {p.opens === p.closes ? 'Open 24 hours' : `${p.opens.slice(0, 5)} – ${p.closes.slice(0, 5)}`}
                  </span>
                </li>
              ))}
            </ul>
          </details>
        </header>

        {menu.data.categories.map((category) => (
          <section key={category.name} className="menu-category">
            <h2>{category.name}</h2>
            <ul className="menu-items">
              {category.items.map((item) => (
                <li key={item.id} className="card menu-item">
                  <div>
                    <h3>
                      {item.name}
                      {item.isVegetarian && <span className="tag veg" title="Vegetarian">V</span>}
                      {item.isSpicy && <span className="tag spicy" title="Spicy">🌶</span>}
                    </h3>
                    <p className="muted">{item.description}</p>
                  </div>
                  <div className="menu-item-actions">
                    <span className="price">{formatMoney(item.price)}</span>
                    <button
                      type="button"
                      className="button"
                      disabled={!restaurant.isOpen || !item.isAvailable}
                      onClick={() => cart.add(restaurant.id, restaurant.name, item)}
                    >
                      Add
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </section>
      <CartPanel />
    </div>
  );
}
