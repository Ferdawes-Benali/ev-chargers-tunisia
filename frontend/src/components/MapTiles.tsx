import { TileLayer } from "react-leaflet";

/**
 * Base map tiles for every map: OpenStreetMap in both themes.
 * The dark theme darkens the tiles only, with a CSS filter on .leaflet-tile-pane (see index.css).
 */
export default function MapTiles() {
  return (
    <TileLayer
      url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
    />
  );
}
