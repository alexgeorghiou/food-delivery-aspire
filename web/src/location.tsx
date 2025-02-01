import { createContext, useContext, useState, type ReactNode } from 'react';

export interface DeliveryLocation {
  label: string;
  address: string;
  latitude: number;
  longitude: number;
}

// Demo drop-off points around central Amsterdam (matching the seeded restaurants).
export const deliveryPresets: DeliveryLocation[] = [
  { label: 'Dam Square', address: 'Dam 1, Amsterdam', latitude: 52.3731, longitude: 4.8926 },
  { label: 'Vondelpark', address: 'Vondelpark 3, Amsterdam', latitude: 52.3584, longitude: 4.8686 },
  { label: 'Oosterpark', address: 'Oosterpark 9, Amsterdam', latitude: 52.3605, longitude: 4.9205 },
  { label: 'Westerpark', address: 'Haarlemmerweg 4, Amsterdam', latitude: 52.3867, longitude: 4.8754 },
];

interface LocationContextValue {
  location: DeliveryLocation;
  setLocation: (location: DeliveryLocation) => void;
}

const LocationContext = createContext<LocationContextValue | null>(null);

export function DeliveryLocationProvider({ children }: { children: ReactNode }) {
  const [location, setLocation] = useState<DeliveryLocation>(deliveryPresets[0]);
  return <LocationContext.Provider value={{ location, setLocation }}>{children}</LocationContext.Provider>;
}

export function useDeliveryLocation(): LocationContextValue {
  const value = useContext(LocationContext);
  if (!value) throw new Error('useDeliveryLocation must be used inside <DeliveryLocationProvider>');
  return value;
}
