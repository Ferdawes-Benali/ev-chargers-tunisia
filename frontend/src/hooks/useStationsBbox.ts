import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { StationListItem } from "@/types/station";

interface BoundingBox {
  south: number;
  west: number;
  north: number;
  east: number;
}

export const useStationsBbox = (bbox: BoundingBox | null) =>
  useQuery({
    queryKey: ["stations-bbox", bbox],
    queryFn: async () =>
      (await api.get<StationListItem[]>("/api/v1/stations/bbox", { params: bbox })).data,
    enabled: !!bbox,
  });