import { useState, useCallback, useRef, useEffect, useMemo } from "react";
import { MapContainer, TileLayer, Marker, Popup, CircleMarker, Polyline, useMapEvents, useMap } from "react-leaflet";
import MarkerClusterGroup from "react-leaflet-cluster";
import { Link } from "react-router-dom";
import "leaflet/dist/leaflet.css";
import L from "leaflet";
import axios from "axios";
import { Zap } from "lucide-react";
import icon from "leaflet/dist/images/marker-icon.png?url";
import iconRetina from "leaflet/dist/images/marker-icon-2x.png?url";
import iconShadow from "leaflet/dist/images/marker-shadow.png?url";
import { useStationsBbox } from "@/hooks/useStationsBbox";
import { useReachEstimate, useTripPlan } from "@/hooks/useReach";
import ReachPanel, { type ReachInputs } from "@/components/ReachPanel";
import { Button } from "@/components/ui/button";
import { BATTERY_COLORS, batteryColor } from "@/lib/battery";
import type { PlaceSuggestion, ReachEstimateResult, TripPlanResult } from "@/types/reach";

delete (L.Icon.Default.prototype as any)._getIconUrl;
L.Icon.Default.mergeOptions({ iconUrl: icon, iconRetinaUrl: iconRetina, shadowUrl: iconShadow });

const baseIconOptions = {
  iconUrl: icon, iconRetinaUrl: iconRetina, shadowUrl: iconShadow,
  iconSize: [25, 41] as [number, number], iconAnchor: [12, 41] as [number, number], popupAnchor: [1, -34] as [number, number],
};
const defaultIcon = new L.Icon.Default();
const userIcon = new L.Icon({ ...baseIconOptions, className: "hue-rotate-180" });
const reachableIcon = new L.Icon({ ...baseIconOptions, className: "hue-rotate-[250deg]" }); // blue → green
const destinationIcon = new L.Icon({ ...baseIconOptions, className: "hue-rotate-[90deg]" });
const lowBatteryIcon = L.divIcon({
  className: "",
  html: `<div style="display:grid;place-items:center;width:28px;height:28px;border-radius:50%;background:${BATTERY_COLORS.critical};border:3px solid #fff;box-shadow:0 1px 4px rgba(15,23,42,.45);color:#fff;font:700 15px/1 system-ui,sans-serif">!</div>`,
  iconSize: [28, 28],
  iconAnchor: [14, 14],
  popupAnchor: [0, -14],
});

const DEFAULT_ORIGIN: [number, number] = [36.8065, 10.1815]; // Tunis

interface BoundingBox { south: number; west: number; north: number; east: number; }

interface RouteSegment { positions: [number, number][]; color: string; dashed: boolean; }

/** Splits the route into runs of the same color; everything after the low battery point is dashed red. */
function buildRouteSegments(trip: TripPlanResult): RouteSegment[] {
  const points = trip.routePoints;
  const battery = trip.batteryAtPoints;

  let lowIndex = points.length;
  if (trip.lowBatteryPoint) {
    // The backend inserts this exact point into the route; nearest match survives float rounding
    const [lat, lng] = trip.lowBatteryPoint;
    let best = Infinity;
    points.forEach(([pLat, pLng], i) => {
      const d = (pLat - lat) ** 2 + (pLng - lng) ** 2;
      if (d < best) { best = d; lowIndex = i; }
    });
  }

  const segments: RouteSegment[] = [];
  for (let i = 0; i < points.length - 1; i++) {
    const dashed = i >= lowIndex;
    const color = dashed ? BATTERY_COLORS.critical : batteryColor(battery[i]);
    const last = segments.at(-1);
    if (last && last.color === color && last.dashed === dashed) last.positions.push(points[i + 1]);
    else segments.push({ positions: [points[i], points[i + 1]], color, dashed });
  }
  return segments;
}

function describeError(error: unknown): string {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 502) return "We couldn't get a road route to this place right now. Try again in a moment, or pick a nearby town.";
    if (error.response?.status === 404) return "This car is no longer available. Choose another car.";
  }
  return "Couldn't check your range. Check your connection and try again.";
}

function BoundsWatcher({ onBoundsChange }: { onBoundsChange: (bbox: BoundingBox) => void }) {
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const toBbox = (b: L.LatLngBounds) => ({ south: b.getSouth(), west: b.getWest(), north: b.getNorth(), east: b.getEast() });

  const map = useMapEvents({
    moveend: () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
      timeoutRef.current = setTimeout(() => onBoundsChange(toBbox(map.getBounds())), 150);
    },
  });

  useEffect(() => { onBoundsChange(toBbox(map.getBounds())); }, [map, onBoundsChange]);
  return null;
}

function LocateButton({ onLocate }: { onLocate: (lat: number, lng: number) => void }) {
  const map = useMap();
  const handleClick = () => {
    if (!navigator.geolocation) return;
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        onLocate(pos.coords.latitude, pos.coords.longitude);
        map.setView([pos.coords.latitude, pos.coords.longitude], 13);
      },
      () => { /* permission denied — keep current view */ }
    );
  };
  return (
    <Button onClick={handleClick} className="absolute z-[1000] top-3 right-3" size="sm">Locate me</Button>
  );
}

export default function MapView() {
  const mapRef = useRef<L.Map | null>(null);
  const [bbox, setBbox] = useState<BoundingBox | null>(null);
  const [userPos, setUserPos] = useState<[number, number] | null>(null);
  const { data: stations } = useStationsBbox(bbox);

  // Reach estimator state
  const [showReach, setShowReach] = useState(false);
  const [destination, setDestination] = useState<PlaceSuggestion | null>(null);
  const [reachOrigin, setReachOrigin] = useState<[number, number] | null>(null);
  const triedGeolocation = useRef(false);
  const reach = useReachEstimate();
  const trip = useTripPlan();
  // Last successful results: kept while a recalculation runs so the map doesn't flicker
  const [reachResult, setReachResult] = useState<ReachEstimateResult | undefined>();
  const [tripResult, setTripResult] = useState<TripPlanResult | undefined>();

  const handleBoundsChange = useCallback((b: BoundingBox) => setBbox(b), []);
  const handleLocate = useCallback((lat: number, lng: number) => setUserPos([lat, lng]), []);

  const openPanel = () => {
    setShowReach(true);
    // Try once for a precise start; if denied, the map center is used
    if (!userPos && !triedGeolocation.current && navigator.geolocation) {
      triedGeolocation.current = true;
      navigator.geolocation.getCurrentPosition(
        (pos) => setUserPos([pos.coords.latitude, pos.coords.longitude]),
        () => { /* denied or unavailable — keep map center */ },
        { timeout: 10_000, maximumAge: 5 * 60_000 }
      );
    }
  };

  const handleEstimate = (inputs: ReachInputs, { fresh }: { fresh: boolean }) => {
    // Origin = your location if known. Otherwise the map center when you press the button;
    // automatic updates keep the same start, since fitting the map moves its center.
    const center = mapRef.current?.getCenter();
    const centerPos: [number, number] | null = center ? [center.lat, center.lng] : null;
    const origin = userPos ?? (fresh ? null : reachOrigin) ?? centerPos ?? DEFAULT_ORIGIN;
    setReachOrigin(origin);

    // Per-call callbacks only fire for the latest call, so a slow older response can't win.
    // On error the old result is dropped: it would no longer match the inputs shown.
    if (destination) {
      trip.mutate(
        { ...inputs, originLat: origin[0], originLng: origin[1], destLat: destination.lat, destLng: destination.lng },
        { onSuccess: setTripResult, onError: () => setTripResult(undefined) }
      );
    } else {
      reach.mutate(
        { ...inputs, originLat: origin[0], originLng: origin[1] },
        { onSuccess: setReachResult, onError: () => setReachResult(undefined) }
      );
    }
  };

  const handleDestinationChange = (place: PlaceSuggestion | null) => {
    setDestination(place);
    trip.reset();
    setTripResult(undefined); // the old route belongs to another place
  };

  const handleClose = () => {
    setShowReach(false);
    reach.reset();
    trip.reset();
    setReachResult(undefined);
    setTripResult(undefined);
    setDestination(null);
    setReachOrigin(null);
  };

  // Each mode shows only its own result
  const active = destination ? trip : reach;
  const result = destination ? tripResult : reachResult;
  const reachableIds = useMemo(() => new Set(result?.reachableStationIds ?? []), [result]);
  const routeSegments = useMemo(() => (tripResult ? buildRouteSegments(tripResult) : []), [tripResult]);

  // No destination: frame the area you can reach
  useEffect(() => {
    if (destination || !reachResult || !reachOrigin || !mapRef.current) return;
    const radiusMeters = Math.max(reachResult.rangeKm * 0.75 * 1000, 5000);
    mapRef.current.fitBounds(L.latLng(reachOrigin).toBounds(radiusMeters * 2), { padding: [20, 20] });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- refit on a new result, not when a destination is picked
  }, [reachResult, reachOrigin]);

  // Destination: frame the route, only when the route itself changes (not on every battery change)
  const fittedRoute = useRef<string | null>(null);
  useEffect(() => {
    if (!tripResult || !mapRef.current || tripResult.routePoints.length < 2) return;
    const key = `${tripResult.routePoints[0]}|${tripResult.routePoints.at(-1)}`;
    if (fittedRoute.current === key) return;
    fittedRoute.current = key;
    mapRef.current.fitBounds(L.latLngBounds(tripResult.routePoints), { padding: [40, 40] });
  }, [tripResult]);
  useEffect(() => { if (!destination) fittedRoute.current = null; }, [destination]);

  return (
    <div className="relative">
      <MapContainer ref={mapRef} center={[34.0, 9.0]} zoom={7} style={{ height: "calc(100vh - 65px)", width: "100%" }}>
        <TileLayer
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        />

        <BoundsWatcher onBoundsChange={handleBoundsChange} />
        <LocateButton onLocate={handleLocate} />

        {/* Road route colored by battery level, on a white casing for contrast with the map */}
        {destination && tripResult && (
          <>
            <Polyline
              positions={tripResult.routePoints}
              pathOptions={{ color: "#ffffff", weight: 10, opacity: 0.9, lineCap: "round", lineJoin: "round" }}
              interactive={false}
            />
            {routeSegments.map((s, i) => (
              <Polyline
                key={`${i}-${s.color}-${s.dashed}`}
                positions={s.positions}
                pathOptions={{ color: s.color, weight: 6, lineCap: "round", lineJoin: "round", dashArray: s.dashed ? "2 10" : undefined }}
                interactive={false}
              />
            ))}
            {tripResult.lowBatteryPoint && (
              <Marker position={tripResult.lowBatteryPoint} icon={lowBatteryIcon} zIndexOffset={1000}>
                <Popup>Battery reaches your reserve here</Popup>
              </Marker>
            )}
          </>
        )}

        {result && reachOrigin && (
          <CircleMarker
            center={reachOrigin}
            radius={8}
            pathOptions={{ color: "#1d4ed8", fillColor: "#1d4ed8", fillOpacity: 1 }}
          >
            <Popup>Start point</Popup>
          </CircleMarker>
        )}

        {userPos && (
          <Marker position={userPos} icon={userIcon}><Popup>You are here</Popup></Marker>
        )}
        {destination && (
          <Marker position={[destination.lat, destination.lng]} icon={destinationIcon}>
            <Popup>{destination.label}</Popup>
          </Marker>
        )}

        <MarkerClusterGroup>
          {stations?.map((station) => {
            const inReach = reachableIds.has(station.id);
            return (
              <Marker
                key={station.id}
                position={[station.lat, station.lng]}
                icon={inReach ? reachableIcon : defaultIcon}
                // After a check, chargers out of reach fade so the green ones stand out
                opacity={result && !inReach ? 0.4 : 1}
              >
                <Popup>
                  <div className="space-y-1">
                    <strong>{station.name}</strong>
                    <p className="text-sm">
                      {station.avgRating !== null ? `★ ${station.avgRating.toFixed(1)}` : "No reviews"}
                    </p>
                    {inReach && <p className="text-sm text-green-700">✓ Within your reach</p>}
                    <Link to={`/stations/${station.id}`} className="text-sm underline">View details →</Link>
                  </div>
                </Popup>
              </Marker>
            );
          })}
        </MarkerClusterGroup>
      </MapContainer>

      {/* Outside MapContainer so clicks on the panel don't reach the map */}
      {!showReach && (
        <Button className="absolute z-[1000] top-14 right-3 gap-1.5" size="sm" variant="secondary" onClick={openPanel}>
          <Zap className="size-4" aria-hidden />
          Check my range
        </Button>
      )}
      {showReach && (
        <ReachPanel
          onEstimate={handleEstimate}
          reachResult={destination ? undefined : reachResult}
          tripResult={destination ? tripResult : undefined}
          isPending={active.isPending}
          error={active.isError ? describeError(active.error) : null}
          originLabel={userPos ? "Your location" : "Map center"}
          originKey={userPos ? userPos.join(",") : "map-center"}
          destination={destination}
          onDestinationChange={handleDestinationChange}
          searchFocus={userPos ? { lat: userPos[0], lng: userPos[1] } : reachOrigin ? { lat: reachOrigin[0], lng: reachOrigin[1] } : null}
          onClose={handleClose}
        />
      )}
    </div>
  );
}
