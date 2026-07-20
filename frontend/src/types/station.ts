export interface StationListItem {
  id: string;
  name: string;
  lat: number;
  lng: number;
  status: string;
  avgRating: number | null;
}

export interface Connector {
  type: string;
  powerKw: number;
  count: number;
}

export interface StationDetail {
  id: string;
  name: string;
  address: string | null;
  lat: number;
  lng: number;
  status: string;
  operatorName: string | null;
  connectors: Connector[];
  avgRating: number | null;
  reviewCount: number;
}

export interface PagedResult<T> {
  data: T[];
  page: number;
  size: number;
  total: number;
  totalPages: number;
}