import { useState } from "react";
import { useTranslation } from "react-i18next";
import { MapContainer, TileLayer, Marker, useMapEvents } from "react-leaflet";
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
    <div className="rounded-md overflow-hidden border">
      {/* Maps stay left-to-right in every language */}
      <div dir="ltr">
        <MapContainer
          center={[34.0, 9.0]}
          zoom={6}
          style={{ height: "250px", width: "100%" }}
        >
          <TileLayer
            url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
          />
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
      <p className="text-xs text-muted-foreground p-2">
        {t("submit.locationHint")}
      </p>
    </div>
  );
}