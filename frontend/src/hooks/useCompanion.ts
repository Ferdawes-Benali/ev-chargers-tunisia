import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { CompanionResult, WalkingRoute } from "@/types/companion";

/** Places to walk to while charging. Nearby shops rarely change: cache for an hour (unless still being prepared). */
export const useCompanion = (stationId: string | undefined, enabled = true) =>
  useQuery({
    queryKey: ["companion", stationId],
    queryFn: async () => (await api.get<CompanionResult>(`/api/v1/stations/${stationId}/companion`)).data,
    enabled: enabled && !!stationId,
    staleTime: (query) => (query.state.data?.status === "preparing" ? 0 : 60 * 60 * 1000),
  });

/** Walking route from the station to one place; fetched only once a place is selected. */
export const useWalkingRoute = (stationId: string | undefined, placeId: string | null) =>
  useQuery({
    queryKey: ["companion-route", stationId, placeId],
    queryFn: async () =>
      (await api.get<WalkingRoute>(`/api/v1/stations/${stationId}/companion/route`, { params: { placeId } })).data,
    enabled: !!stationId && !!placeId,
    staleTime: 24 * 60 * 60 * 1000,
  });
