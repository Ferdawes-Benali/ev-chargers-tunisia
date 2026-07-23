import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { PagedResult, StationListItem } from "@/types/station";

export const useStations = (
  page = 1,
  size = 20,
  connectorType?: string | null,
  minPowerKw?: number | null
) =>
  useQuery({
    queryKey: ["stations", page, size, connectorType, minPowerKw],
    queryFn: async () =>
      (
        await api.get<PagedResult<StationListItem>>("/api/v1/stations", {
          params: { page, size, connectorType: connectorType ?? undefined, minPowerKw: minPowerKw ?? undefined },
        })
      ).data,
  });