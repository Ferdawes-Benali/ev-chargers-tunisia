import { TileLayer } from "react-leaflet";
import { useTheme } from "@/lib/theme";

const OSM_ATTRIBUTION = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors';

/** Base map tiles: OpenStreetMap in the light theme, CARTO "dark_all" in the dark theme. */
export default function MapTiles() {
  const { theme } = useTheme();

  // Keyed by theme so the attribution control is replaced along with the tiles
  return theme === "dark" ? (
    <TileLayer
      key="dark"
      url="https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png"
      subdomains="abcd"
      attribution={`${OSM_ATTRIBUTION} &copy; <a href="https://carto.com/attributions">CARTO</a>`}
    />
  ) : (
    <TileLayer key="light" url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" attribution={OSM_ATTRIBUTION} />
  );
}
