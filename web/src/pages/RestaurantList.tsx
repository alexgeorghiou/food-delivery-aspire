import { Link } from 'react-router-dom';
import { api } from '../api';
import { useAsync } from '../hooks';
import { useDeliveryLocation } from '../location';

function formatNextOpening(iso: string | null): string {
  if (!iso) return 'Closed';
  const date = new Date(iso);
  return `Opens ${date.toLocaleDateString(undefined, { weekday: 'short' })} ${date.toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
  })}`;
}

export function RestaurantList() {
  const { location } = useDeliveryLocation();
  const { data, error, loading } = useAsync(
    () => api.listRestaurants({ lat: location.latitude, lon: location.longitude }),
    [location.latitude, location.longitude],
  );

  if (loading && !data) return <p className="muted">Loading restaurants…</p>;
  if (error) return <p className="error">Could not load restaurants: {error.message}</p>;

  return (
    <section>
      <h1>Restaurants near {location.label}</h1>
      <ul className="grid">
        {data?.map((r) => (
          <li key={r.id} className={`card restaurant ${r.isOpen ? '' : 'closed'}`}>
            <Link to={`/restaurants/${r.id}`}>
              <div className="card-header">
                <h2>{r.name}</h2>
                <span className={`badge ${r.isOpen ? 'open' : 'shut'}`}>
                  {r.isOpen ? 'Open' : formatNextOpening(r.nextOpeningAt)}
                </span>
              </div>
              <p className="muted">
                {r.cuisine} · {r.distanceKm?.toFixed(1)} km · ~{r.averagePrepMinutes} min prep
              </p>
              <p>{r.description}</p>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
