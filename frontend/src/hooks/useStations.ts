import { useQuery } from "@tanstack/react-query";
import { api } from "../lib/api";
import type { PagedResult, StationListItem } from "../types/station";

export const useStations = (page = 1, size = 20) =>
  useQuery({
    queryKey: ["stations", page, size],
    queryFn: async () =>
      (await api.get<PagedResult<StationListItem>>("/api/v1/stations", { params: { page, size } })).data,
  });