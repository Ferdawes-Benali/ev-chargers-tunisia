export interface Vehicle {
  id: string;
  name: string;
  batteryKwh: number;
  socReservePercent: number;
}

export interface ReachEstimateRequest {
  vehicleId: string;
  batteryPercent: number;
  originLat: number;
  originLng: number;
}

export interface ReachEstimateResult {
  rangeKm: number;
  reachableStationIds: string[];
}

export interface PlaceSuggestion {
  label: string;
  lat: number;
  lng: number;
}

export interface TripPlanRequest {
  vehicleId: string;
  batteryPercent: number;
  originLat: number;
  originLng: number;
  destLat: number;
  destLng: number;
}

export interface TripPlanResult {
  distanceKm: number;
  durationMinutes: number;
  rangeKm: number;
  batteryOnArrival: number;
  reachable: boolean;
  shortfallKm: number;
  /** Share of the route on motorways, 0..1 */
  motorwayShare: number;
  temperatureC: number | null;
  /** Road route as [lat, lng] */
  routePoints: [number, number][];
  /** Battery % at each route point, same length as routePoints */
  batteryAtPoints: number[];
  /** [lat, lng] where the battery reaches the reserve, or null */
  lowBatteryPoint: [number, number] | null;
  /** Chargers near the route reached before the battery hits the reserve */
  reachableStationIds: string[];
  /** Where to charge when the destination is out of reach */
  recommendedStop: RecommendedStop | null;
  /** All chargers near the route ahead of the driver, reachable or not */
  chargersAlongRouteCount: number;
}

export interface RecommendedStop {
  stationId: string;
  name: string;
  distanceAlongKm: number;
  /** Battery % when reaching the stop */
  batteryOnArrivalPercent: number;
  lat: number;
  lng: number;
}
