import { useState, useCallback, useRef, useEffect, useMemo } from "react";
import { MapContainer, TileLayer, Marker, Popup, Circle, CircleMarker, GeoJSON, useMapEvents, useMap } from "react-leaflet";
import MarkerClusterGroup from "react-leaflet-cluster";
import { Link } from "react-router-dom";
import "leaflet/dist/leaflet.css";
import L from "leaflet";
import { Zap } from "lucide-react";
import type { GeoJsonObject } from "geojson";
import icon from "leaflet/dist/images/marker-icon.png?url";
import iconRetina from "leaflet/dist/images/marker-icon-2x.png?url";
import iconShadow from "leaflet/dist/images/marker-shadow.png?url";
import { useStationsBbox } from "@/hooks/useStationsBbox";
import { useReachEstimate } from "@/hooks/useReach";
import ReachPanel, { type ReachInputs } from "@/components/ReachPanel";
import { Button } from "@/components/ui/button";

delete (L.Icon.Default.prototype as any)._getIconUrl;
L.Icon.Default.mergeOptions({ iconUrl: icon, iconRetinaUrl: iconRetina, shadowUrl: iconShadow });

const baseIconOptions = {
  iconUrl: icon, iconRetinaUrl: iconRetina, shadowUrl: iconShadow,
  iconSize: [25, 41] as [number, number], iconAnchor: [12, 41] as [number, number], popupAnchor: [1, -34] as [number, number],
};
const userIcon = new L.Icon({ ...baseIconOptions, className: "hue-rotate-180" });
const reachableIcon = new L.Icon({ ...baseIconOptions, className: "hue-rotate-[250deg]" }); // blue → green
const destinationIcon = new L.Icon({ ...baseIconOptions, className: "hue-rotate-[90deg]" });

interface BoundingBox { south: number; west: number; north: number; east: number; }

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

/** Captures ONE map click as the destination while "picking" mode is on. */
function DestinationPicker({ active, onPick }: { active: boolean; onPick: (lat: number, lng: number) => void }) {
  useMapEvents({ click: (e) => { if (active) onPick(e.latlng.lat, e.latlng.lng); } });
  return null;
}

export default function MapView() {
  const mapRef = useRef<L.Map | null>(null);
  const [bbox, setBbox] = useState<BoundingBox | null>(null);
  const [userPos, setUserPos] = useState<[number, number] | null>(null);
  const { data: stations } = useStationsBbox(bbox);

  // Reach estimator state
  const [showReach, setShowReach] = useState(false);
  const [destination, setDestination] = useState<[number, number] | null>(null);
  const [pickingDestination, setPickingDestination] = useState(false);
  const [reachOrigin, setReachOrigin] = useState<[number, number] | null>(null);
  const reach = useReachEstimate();

  const handleBoundsChange = useCallback((b: BoundingBox) => setBbox(b), []);
  const handleLocate = useCallback((lat: number, lng: number) => setUserPos([lat, lng]), []);

  const handleEstimate = (inputs: ReachInputs) => {
    // Origin = your location if known, otherwise the current map center
    const center = mapRef.current?.getCenter();
    const origin: [number, number] = userPos ?? (center ? [center.lat, center.lng] : [36.8065, 10.1815]);
    setReachOrigin(origin);
    reach.mutate({
      ...inputs,
      originLat: origin[0], originLng: origin[1],
      destLat: destination?.[0] ?? null, destLng: destination?.[1] ?? null,
    });
  };

  const handleClear = () => {
    reach.reset();
    setDestination(null);
    setReachOrigin(null);
    setPickingDestination(false);
  };

  const result = reach.data;
  const isochrone = useMemo<GeoJsonObject | null>(() => {
    if (!result?.isochroneGeoJson) return null;
    try { return JSON.parse(result.isochroneGeoJson) as GeoJsonObject; } catch { return null; }
  }, [result]);
  const reachableIds = useMemo(() => new Set(result?.reachableStationIds ?? []), [result]);

  useEffect(() => {
    if (!result || !reachOrigin || !mapRef.current) return;
    const radiusMeters = Math.max(result.rangeKm * 0.75 * 1000, 5000);
    mapRef.current.fitBounds(L.latLng(reachOrigin).toBounds(radiusMeters * 2), { padding: [20, 20] });
  }, [result, reachOrigin]);

  return (
    <div className="relative">
      <MapContainer ref={mapRef} center={[34.0, 9.0]} zoom={7} style={{ height: "calc(100vh - 65px)", width: "100%" }}>
        <TileLayer
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        />

        <BoundsWatcher onBoundsChange={handleBoundsChange} />
        <LocateButton onLocate={handleLocate} />
        <DestinationPicker
          active={pickingDestination}
          onPick={(lat, lng) => { setDestination([lat, lng]); setPickingDestination(false); }}
        />

        {/* Rough estimate: dashed circle */}
        {result && reachOrigin && result.rangeKm > 0 && (
          <Circle
            center={reachOrigin}
            radius={result.rangeKm * 0.75 * 1000}
            pathOptions={{ color: "#2563eb", weight: 2, dashArray: "6 6", fillOpacity: 0.05 }}
          />
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

        {/* Real road-based reach: filled polygon. `key` forces a redraw when the data changes. */}
        {isochrone && (
          <GeoJSON
            key={result?.isochroneGeoJson?.length}
            data={isochrone}
            style={{ color: "#16a34a", weight: 2, fillOpacity: 0.2 }}
          />
        )}

        {userPos && (
          <Marker position={userPos} icon={userIcon}><Popup>You are here</Popup></Marker>
        )}
        {destination && (
          <Marker position={destination} icon={destinationIcon}><Popup>Destination</Popup></Marker>
        )}

        <MarkerClusterGroup>
          {stations?.map((station) => (
            <Marker
              key={station.id}
              position={[station.lat, station.lng]}
              icon={reachableIds.has(station.id) ? reachableIcon : new L.Icon.Default()}
            >
              <Popup>
                <div className="space-y-1">
                  <strong>{station.name}</strong>
                  <p className="text-sm">
                    {station.avgRating !== null ? `★ ${station.avgRating.toFixed(1)}` : "No reviews"}
                  </p>
                  {reachableIds.has(station.id) && <p className="text-sm text-green-700">✓ Within your reach</p>}
                  <Link to={`/stations/${station.id}`} className="text-sm underline">View details →</Link>
                </div>
              </Popup>
            </Marker>
          ))}
        </MarkerClusterGroup>
      </MapContainer>

      {/* Outside MapContainer so clicks on the panel don't reach the map */}
      {!showReach && (
        <Button className="absolute z-[1000] top-14 right-3 gap-1.5" size="sm" variant="secondary" onClick={() => setShowReach(true)}>
          <Zap className="size-4" aria-hidden />
          Check my range
        </Button>
      )}
      {showReach && (
        <ReachPanel
          onEstimate={handleEstimate}
          result={result}
          isPending={reach.isPending}
          isError={reach.isError}
          originLabel={userPos ? "Your location" : "Map center"}
          destinationKey={destination ? destination.join(",") : null}
          pickingDestination={pickingDestination}
          onTogglePickDestination={() => setPickingDestination((p) => !p)}
          onClearDestination={() => setDestination(null)}
          onClose={() => { setShowReach(false); handleClear(); }}
        />
      )}
    </div>
  );
}