import type { OrderStatus, OrderStatusChange } from '../types';

const happyPath: { status: OrderStatus; label: string }[] = [
  { status: 'Placed', label: 'Order placed' },
  { status: 'Accepted', label: 'Accepted by restaurant' },
  { status: 'Preparing', label: 'Being prepared' },
  { status: 'ReadyForPickup', label: 'Ready for pickup' },
  { status: 'CourierAssigned', label: 'Courier assigned' },
  { status: 'PickedUp', label: 'On the way' },
  { status: 'Delivered', label: 'Delivered' },
];

export function StatusTimeline({ status, history }: { status: OrderStatus; history: OrderStatusChange[] }) {
  if (status === 'Cancelled' || status === 'Rejected') {
    const last = history[history.length - 1];
    return (
      <div className="card timeline-ended">
        <strong>{status === 'Cancelled' ? 'Order cancelled' : 'Order rejected by the restaurant'}</strong>
        {last?.note && <p className="muted">{last.note}</p>}
      </div>
    );
  }

  const currentIndex = happyPath.findIndex((s) => s.status === status);
  const reachedAt = new Map(history.map((h) => [h.status, h.at]));

  return (
    <ol className="timeline">
      {happyPath.map((step, index) => {
        const state = index < currentIndex ? 'done' : index === currentIndex ? 'current' : 'todo';
        const at = reachedAt.get(step.status);
        return (
          <li key={step.status} className={state}>
            <span className="dot" aria-hidden />
            <span className="label">{step.label}</span>
            {at && (
              <time dateTime={at} className="muted small">
                {new Date(at).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })}
              </time>
            )}
          </li>
        );
      })}
    </ol>
  );
}
