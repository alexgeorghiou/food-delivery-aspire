import type {
  Menu,
  Order,
  OrderActor,
  OrderStatus,
  PlaceOrderRequest,
  ProblemDetails,
  RestaurantDetails,
  RestaurantSummary,
} from './types';

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: ProblemDetails | null,
  ) {
    super(ApiError.describe(status, problem));
    this.name = 'ApiError';
  }

  private static describe(status: number, problem: ProblemDetails | null): string {
    if (problem?.errors) {
      return Object.values(problem.errors).flat().join(' ');
    }
    return problem?.detail ?? problem?.title ?? `Request failed with status ${status}`;
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', Accept: 'application/json', ...init?.headers },
  });

  if (!response.ok) {
    let problem: ProblemDetails | null = null;
    try {
      problem = (await response.json()) as ProblemDetails;
    } catch {
      // Non-JSON error body; fall back to the status code.
    }
    throw new ApiError(response.status, problem);
  }

  if (response.status === 204 || response.status === 202) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export const api = {
  listRestaurants: (origin?: { lat: number; lon: number }) => {
    const query = origin ? `?lat=${origin.lat}&lon=${origin.lon}` : '';
    return request<RestaurantSummary[]>(`/restaurants${query}`);
  },

  getRestaurant: (id: string) => request<RestaurantDetails>(`/restaurants/${id}`),

  getMenu: (id: string) => request<Menu>(`/restaurants/${id}/menu`),

  placeOrder: (body: PlaceOrderRequest) =>
    request<Order>('/orders', { method: 'POST', body: JSON.stringify(body) }),

  getOrder: (id: string) => request<Order>(`/orders/${id}`),

  changeStatus: (id: string, status: OrderStatus, actor: OrderActor, note?: string) =>
    request<Order>(`/orders/${id}/status`, {
      method: 'POST',
      body: JSON.stringify({ status, actor, note: note ?? null }),
    }),

  cancelOrder: (id: string) => request<Order>(`/orders/${id}/cancel`, { method: 'POST' }),
};
