// Client-side preview of the fee rules in FoodDelivery.Domain.Pricing.PricingPolicy.
// The Orders API recomputes everything from the Restaurants API quote; this is display only.

export const pricingPolicy = {
  deliveryFee: 2.49,
  freeDeliveryThreshold: 30,
  minimumOrderSubtotal: 10,
  serviceFeeRate: 0.05,
  serviceFeeCap: 3,
} as const;

const round = (value: number) => Math.round((value + Number.EPSILON) * 100) / 100;

export interface CartLineInput {
  unitPrice: number;
  quantity: number;
}

export function previewTotals(lines: CartLineInput[]) {
  if (lines.length === 0) {
    return { subtotal: 0, deliveryFee: 0, smallOrderSurcharge: 0, serviceFee: 0, total: 0 };
  }

  const subtotal = round(lines.reduce((sum, l) => sum + round(l.unitPrice * l.quantity), 0));
  const deliveryFee = subtotal >= pricingPolicy.freeDeliveryThreshold ? 0 : pricingPolicy.deliveryFee;
  const smallOrderSurcharge =
    subtotal < pricingPolicy.minimumOrderSubtotal ? round(pricingPolicy.minimumOrderSubtotal - subtotal) : 0;
  const serviceFee = round(Math.min(subtotal * pricingPolicy.serviceFeeRate, pricingPolicy.serviceFeeCap));
  const total = round(subtotal + deliveryFee + smallOrderSurcharge + serviceFee);

  return { subtotal, deliveryFee, smallOrderSurcharge, serviceFee, total };
}

const formatter = new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' });

export const formatMoney = (amount: number) => formatter.format(amount);
