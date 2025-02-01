import { createContext, useContext, useEffect, useMemo, useReducer, type ReactNode } from 'react';
import type { MenuItem } from './types';
import { previewTotals } from './pricing';

export interface CartLine {
  menuItemId: string;
  name: string;
  unitPrice: number;
  quantity: number;
}

interface CartState {
  restaurantId: string | null;
  restaurantName: string | null;
  lines: CartLine[];
}

type CartAction =
  | { type: 'add'; restaurantId: string; restaurantName: string; item: MenuItem }
  | { type: 'decrement'; menuItemId: string }
  | { type: 'remove'; menuItemId: string }
  | { type: 'clear' };

const MAX_QUANTITY = 50;
const STORAGE_KEY = 'food-delivery.cart';
const emptyCart: CartState = { restaurantId: null, restaurantName: null, lines: [] };

function reducer(state: CartState, action: CartAction): CartState {
  switch (action.type) {
    case 'add': {
      // A cart belongs to one restaurant; switching restaurant starts a new cart.
      const base = state.restaurantId === action.restaurantId ? state : emptyCart;
      const existing = base.lines.find((l) => l.menuItemId === action.item.id);
      const lines = existing
        ? base.lines.map((l) =>
            l.menuItemId === action.item.id ? { ...l, quantity: Math.min(l.quantity + 1, MAX_QUANTITY) } : l,
          )
        : [...base.lines, { menuItemId: action.item.id, name: action.item.name, unitPrice: action.item.price, quantity: 1 }];
      return { restaurantId: action.restaurantId, restaurantName: action.restaurantName, lines };
    }
    case 'decrement': {
      const lines = state.lines
        .map((l) => (l.menuItemId === action.menuItemId ? { ...l, quantity: l.quantity - 1 } : l))
        .filter((l) => l.quantity > 0);
      return lines.length === 0 ? emptyCart : { ...state, lines };
    }
    case 'remove': {
      const lines = state.lines.filter((l) => l.menuItemId !== action.menuItemId);
      return lines.length === 0 ? emptyCart : { ...state, lines };
    }
    case 'clear':
      return emptyCart;
  }
}

function load(): CartState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as CartState) : emptyCart;
  } catch {
    return emptyCart;
  }
}

interface CartContextValue extends CartState {
  itemCount: number;
  totals: ReturnType<typeof previewTotals>;
  add: (restaurantId: string, restaurantName: string, item: MenuItem) => void;
  decrement: (menuItemId: string) => void;
  remove: (menuItemId: string) => void;
  clear: () => void;
}

const CartContext = createContext<CartContextValue | null>(null);

export function CartProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(reducer, undefined, load);

  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    } catch {
      // Storage may be unavailable (private mode); the cart still works for this session.
    }
  }, [state]);

  const value = useMemo<CartContextValue>(
    () => ({
      ...state,
      itemCount: state.lines.reduce((n, l) => n + l.quantity, 0),
      totals: previewTotals(state.lines),
      add: (restaurantId, restaurantName, item) => dispatch({ type: 'add', restaurantId, restaurantName, item }),
      decrement: (menuItemId) => dispatch({ type: 'decrement', menuItemId }),
      remove: (menuItemId) => dispatch({ type: 'remove', menuItemId }),
      clear: () => dispatch({ type: 'clear' }),
    }),
    [state],
  );

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart(): CartContextValue {
  const context = useContext(CartContext);
  if (!context) {
    throw new Error('useCart must be used inside <CartProvider>');
  }
  return context;
}
