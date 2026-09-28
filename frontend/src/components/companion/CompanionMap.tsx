import { useEffect } from "react";
import { MapContainer, TileLayer, CircleMarker, Polyline, Tooltip, useMap } from "react-leaflet";
import "leaflet/dist/leaflet.css";
import L from "leaflet";
import { pickPlaceName } from "@/lib/placeName";
import { useLocale } from "@/lib/format";
import type { CompanionPlace, WalkingRoute } from "@/types/companion";
import type { StationDetail } from "@/types/station";
import { CATEGORIES } from "./meta";

interface CompanionMapProps {
  station: StationDetail;
  places: CompanionPlace[];
  selected: CompanionPlace | undefined;
  route: WalkingRoute | undefined;
  onSelect: (placeId: string) => void;
}

export default function CompanionMap({ station, places, selected, route, onSelect }: CompanionMapProps) {
  const { lang, isRtl } = useLocale();
  const textDir = isRtl ? "rtl" : "ltr";
  // The selected place stays on the map even if a filter hides it from the list
  const shown = selected && !places.some((p) => p.id === selected.id) ? [...places, selected] : places;

  return (
    // Maps stay left-to-right in every language; tooltip text follows the page direction
    <div dir="ltr" className="overflow-hidden rounded-md border">
      <MapContainer center={[station.lat, station.lng]} zoom={16} style={{ height: "250px", width: "100%" }}>
        <TileLayer
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        />
        <FitView station={station} places={places} selected={selected} route={route} />
        {route && route.points.length > 1 && (
          <Polyline
            positions={route.points}
            pathOptions={{ color: "#0e9d9f", weight: 5, opacity: 0.85, dashArray: "1 8", lineCap: "round" }}
            interactive={false}
          />
        )}
        {shown.map((place) => {
          const isSelected = place.id === selected?.id;
          return (
            <CircleMarker
              key={place.id}
              center={[place.lat, place.lng]}
              radius={isSelected ? 9 : 5}
              pathOptions={{
                color: isSelected ? "#0f172a" : "#ffffff",
                weight: isSelected ? 3 : 1.5,
                fillColor: CATEGORIES[place.category].color,
                fillOpacity: 0.95,
              }}
              eventHandlers={{ click: () => onSelect(place.id) }}
            >
              <Tooltip><span dir={textDir}>{pickPlaceName(place, lang)}</span></Tooltip>
            </CircleMarker>
          );
        })}
        <CircleMarker
          center={[station.lat, station.lng]}
          radius={8}
          pathOptions={{ color: "#ffffff", weight: 2, fillColor: "#0e9d9f", fillOpacity: 1 }}
        >
          <Tooltip><span dir="auto">{station.name}</span></Tooltip>
        </CircleMarker>
      </MapContainer>
    </div>
  );
}

/** Fits the walking route when there is one, else the station + selected place, else every visible place. */
function FitView({ station, places, selected, route }: Omit<CompanionMapProps, "onSelect">) {
  const map = useMap();

  useEffect(() => {
    const bounds = L.latLngBounds([[station.lat, station.lng]]);
    if (route && route.points.length > 1) route.points.forEach((p) => bounds.extend(p));
    else if (selected) bounds.extend([selected.lat, selected.lng]);
    else places.forEach((p) => bounds.extend([p.lat, p.lng]));
    map.fitBounds(bounds, { padding: [28, 28], maxZoom: 17 });
  }, [map, station.lat, station.lng, places, selected, route]);

  return null;
}
