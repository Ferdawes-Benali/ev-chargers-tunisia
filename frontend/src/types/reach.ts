export interface Vehicle {
  id: string;
  name: string;
  batteryKwh: number;
  socReservePercent: number;
}

export type DrivingCondition = "city" | "highway" | "cold";

export interface ReachEstimateRequest {
  vehicleId: string;
  batteryPercent: number;
  drivingCondition: DrivingCondition;
  originLat: number;
  originLng: number;
  destLat?: number | null;
  destLng?: number | null;
}

export interface ReachEstimateResult {
  rangeKm: number;
  batteryPercentOnArrival: number | null;
  destinationReachable: boolean | null;
  isochroneGeoJson: string | null;
  reachableStationIds: string[];
}