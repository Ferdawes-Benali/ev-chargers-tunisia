import { useState } from "react";
import { useTranslation } from "react-i18next";
import { MapContainer, Marker, useMapEvents } from "react-leaflet";
import { MousePointerClick } from "lucide-react";
import MapTiles from "@/components/MapTiles";
import "leaflet/dist/leaflet.css";
import L from "leaflet";
import icon from "leaflet/dist/images/marker-icon.png?url";
import iconRetina from "leaflet/dist/images/marker-icon-2x.png?url";
import iconShadow from "leaflet/dist/images/marker-shadow.png?url";

delete (L.Icon.Default.prototype as any)._getIconUrl;
L.Icon.Default.mergeOptions({
  iconUrl: icon,
  iconRetinaUrl: iconRetina,
  shadowUrl: iconShadow,
});

interface LocationPickerProps {
  onPick: (lat: number, lng: number) => void;
}

function ClickHandler({ onPick, setMarker }: LocationPickerProps & { setMarker: (pos: [number, number]) => void }) {
  useMapEvents({
    click: (e) => {
      const { lat, lng } = e.latlng;
      setMarker([lat, lng]);
      onPick(lat, lng);
    },
  });
  return null;
}

export default function LocationPicker({ onPick }: LocationPickerProps) {
  const { t } = useTranslation();
  const [marker, setMarker] = useState<[number, number] | null>(null);

  return (
    <div className="isolate overflow-hidden rounded-xl border bg-card">
      {/* Maps stay left-to-right in every language */}
      <div dir="ltr">
        <MapContainer
          center={[34.0, 9.0]}
          zoom={6}
          style={{ height: "280px", width: "100%" }}
        >
          <MapTiles />
          <ClickHandler onPick={onPick} setMarker={setMarker} />
          {marker && (
            <Marker
              position={marker}
              alt={t("submit.markerAlt")}
              draggable
              eventHandlers={{
                dragend: (e) => {
                  const pos = e.target.getLatLng();
                  setMarker([pos.lat, pos.lng]);
                  onPick(pos.lat, pos.lng);
                },
              }}
            />
          )}
        </MapContainer>
      </div>
      <p className="flex items-start gap-2 border-t bg-muted/50 px-3 py-2.5 text-xs text-muted-foreground">
        <MousePointerClick aria-hidden="true" className="mt-px size-4 shrink-0" />
        {t("submit.locationHint")}
      </p>
    </div>
  );
}