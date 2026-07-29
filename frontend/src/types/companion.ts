export type PlaceCategory =
  | "cafe"
  | "restaurant"
  | "mosque"
  | "park"
  | "shopping"
  | "pharmacy"
  | "toilets"
  | "atm";

/** What the driver wants to do; each category belongs to one group. */
export type PlaceGroup = "eat" | "pray" | "essentials" | "relax" | "shop";

export type WalkBand = "≤2 min" | "≤5 min" | "≤10 min" | "≤15 min" | "farther";

export type OpenStatus = "open" | "closed" | "unknown";

export type PlaceLanguage = "fr" | "ar" | "en";

/** Name in each app language, when OpenStreetMap has it. */
export type PlaceNames = Record<PlaceLanguage, string | null>;

export interface CompanionPlace {
  /** Stable OSM id, e.g. "node/715619811". */
  id: string;
  /** Fallback name (OSM "name", then French, then Arabic, else a generic label). */
  name: string;
  names: PlaceNames;
  category: PlaceCategory;
  group: PlaceGroup;
  lat: number;
  lng: number;
  /** Straight-line distance from the station. */
  distanceMeters: number;
  walkMinutes: number;
  band: WalkBand;
  openStatus: OpenStatus;
  /** Local "HH:mm"; null when open around the clock or unknown. */
  closesAt: string | null;
  /** Local "HH:mm" when it reopens within 24 h. */
  opensAt: string | null;
  /** False for extra ATMs/banks, which only the "Essentials" filter shows. */
  inDefaultList: boolean;
}

export interface CompanionPick {
  label: string;
  placeId: string;
  reason: string;
}

export interface CompanionResult {
  chargeMinutes: number | null;
  maxPowerKw: number | null;
  /** Local "HH:mm" when the car should be ready. */
  backBy: string | null;
  picks: CompanionPick[];
  places: CompanionPlace[];
  source: string;
  /** true when OpenStreetMap could not be reached; places is then empty. */
  unavailable: boolean;
}

export interface WalkingRoute {
  distanceMeters: number;
  durationMinutes: number;
  /** [lat, lng] pairs; empty when estimated. */
  points: [number, number][];
  /** true when routing was unavailable: numbers come from straight-line distance. */
  estimated: boolean;
}
