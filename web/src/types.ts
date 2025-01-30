export type DayOfWeek =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday';

export interface RestaurantSummary {
  id: string;
  name: string;
  cuisine: string;
  address: string;
  description: string;
  latitude: number;
  longitude: number;
  isOpen: boolean;
  nextOpeningAt: string | null;
  averagePrepMinutes: number;
  distanceKm: number | null;
}

export interface OpeningPeriod {
  day: DayOfWeek;
  opens: string;
  closes: string;
}

export interface RestaurantDetails {
  summary: RestaurantSummary;
  timeZoneId: string;
  openingHours: OpeningPeriod[];
}

export interface MenuItem {
  id: string;
  name: string;
  description: string;
  price: number;
  isAvailable: boolean;
  isVegetarian: boolean;
  isSpicy: boolean;
}

export interface MenuCategory {
  name: string;
  items: MenuItem[];
}

export interface Menu {
  restaurantId: string;
  currency: string;
  categories: MenuCategory[];
}

export type OrderStatus =
  | 'Placed'
  | 'Accepted'
  | 'Preparing'
  | 'ReadyForPickup'
  | 'CourierAssigned'
  | 'PickedUp'
  | 'Delivered'
  | 'Cancelled'
  | 'Rejected';

export type OrderActor = 'Customer' | 'Restaurant' | 'Courier' | 'System';

export interface OrderTotals {
  subtotal: number;
  deliveryFee: number;
  smallOrderSurcharge: number;
  serviceFee: number;
  total: number;
}

export interface OrderLine {
  menuItemId: string;
  name: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface OrderStatusChange {
  status: OrderStatus;
  actor: OrderActor;
  at: string;
  note: string | null;
}

export interface Order {
  id: string;
  restaurantId: string;
  restaurantName: string;
  customerName: string;
  deliveryAddress: string;
  status: OrderStatus;
  lines: OrderLine[];
  totals: OrderTotals;
  courier: { id: string; name: string } | null;
  history: OrderStatusChange[];
  nextStatuses: OrderStatus[];
  placedAt: string;
  updatedAt: string;
  version: number;
}

export interface PlaceOrderRequest {
  restaurantId: string;
  customerName: string;
  deliveryAddress: string;
  deliveryLatitude: number;
  deliveryLongitude: number;
  lines: { menuItemId: string; quantity: number }[];
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}
