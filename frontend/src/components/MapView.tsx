import { useState, useCallback, useRef, useEffect } from "react";
import { MapContainer, TileLayer, Marker, Popup, useMapEvents, useMap } from "react-leaflet";
import MarkerClusterGroup from "react-leaflet-cluster";
import { Link } from "react-router-dom";
import "leaflet/dist/leaflet.css";
import L from "leaflet";
import icon from "leaflet/dist/images/marker-icon.png?url";
import iconRetina from "leaflet/dist/images/marker-icon-2x.png?url";
import iconShadow from "leaflet/dist/images/marker-shadow.png?url";
import { useStationsBbox } from "@/hooks/useStationsBbox";
import { Button } from "@/components/ui/button";

delete (L.Icon.Default.prototype as any)._getIconUrl;
L.Icon.Default.mergeOptions({
  iconUrl: icon,
  iconRetinaUrl: iconRetina,
  shadowUrl: iconShadow,
});

const userIcon = new L.Icon({
  iconUrl: icon,
  iconRetinaUrl: iconRetina,
  shadowUrl: iconShadow,
  iconSize: [20, 33],
  className: "hue-rotate-180",
});

interface BoundingBox {
  south: number;
  west: number;
  north: number;
  east: number;
}

function BoundsWatcher({ onBoundsChange }: { onBoundsChange: (bbox: BoundingBox) => void }) {
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const map = useMapEvents({
    moveend: () => {
      if (timeoutRef.current) clearTimeout(timeoutRef.current);
      timeoutRef.current = setTimeout(() => {
        const b = map.getBounds();
        onBoundsChange({
          south: b.getSouth(),
          west: b.getWest(),
          north: b.getNorth(),
          east: b.getEast(),
        });
      }, 150);
    },
  });

  useEffect(() => {
    const b = map.getBounds();
    onBoundsChange({
      south: b.getSouth(),
      west: b.getWest(),
      north: b.getNorth(),
      east: b.getEast(),
    });
  }, [map, onBoundsChange]);

  return null;
}

function LocateButton({ onLocate }: { onLocate: (lat: number, lng: number) => void }) {
  const map = useMap();

  const handleClick = () => {
    if (!navigator.geolocation) return;
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        const { latitude, longitude } = pos.coords;
        onLocate(latitude, longitude);
        map.setView([latitude, longitude], 13);
      },
      () => {
        // permission denied or error — silently keep the current view
      }
    );
  };

  return (
    <Button
      onClick={handleClick}
      className="absolute z-1000 top-3 right-3"
      size="sm"
    >
      Locate me
    </Button>
  );
}

export default function MapView() {
  const [bbox, setBbox] = useState<BoundingBox | null>(null);
  const [userPos, setUserPos] = useState<[number, number] | null>(null);
  const { data } = useStationsBbox(bbox);

  const handleBoundsChange = useCallback((newBbox: BoundingBox) => setBbox(newBbox), []);
  const handleLocate = useCallback((lat: number, lng: number) => setUserPos([lat, lng]), []);

  return (
    <div className="relative">
      <MapContainer
        center={[34.0, 9.0]}
        zoom={7}
        style={{ height: "calc(100vh - 65px)", width: "100%" }}
      >
        <TileLayer
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        />

        <BoundsWatcher onBoundsChange={handleBoundsChange} />
        <LocateButton onLocate={handleLocate} />

        {userPos && (
          <Marker position={userPos} icon={userIcon}>
            <Popup>You are here</Popup>
          </Marker>
        )}

        <MarkerClusterGroup>
          {data?.map((station) => (
            <Marker key={station.id} position={[station.lat, station.lng]}>
              <Popup>
                <div className="space-y-1">
                  <strong>{station.name}</strong>
                  <p className="text-sm">
                    {station.avgRating !== null ? `★ ${station.avgRating.toFixed(1)}` : "No reviews"}
                  </p>
                  <Link to={`/stations/${station.id}`} className="text-sm underline">
                    View details →
                  </Link>
                </div>
              </Popup>
            </Marker>
          ))}
        </MarkerClusterGroup>
      </MapContainer>
    </div>
  );
}