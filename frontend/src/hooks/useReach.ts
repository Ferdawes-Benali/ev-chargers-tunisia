import { useQuery, useMutation } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type {
  Vehicle,
  ReachEstimateRequest,
  ReachEstimateResult,
  PlaceSuggestion,
  TripPlanRequest,
  TripPlanResult,
} from "@/types/reach";

export const useVehicles = () =>
  useQuery({
    queryKey: ["vehicles"],
    queryFn: async () => (await api.get<Vehicle[]>("/api/v1/vehicles")).data,
    staleTime: Infinity, // vehicle specs rarely change — fetch once per session
  });

export const useReachEstimate = () =>
  useMutation({
    mutationFn: async (req: ReachEstimateRequest) =>
      (await api.post<ReachEstimateResult>("/api/v1/reach/estimate", req)).data,
  });

/** Place suggestions in Tunisia. Pass an already debounced query. */
export const usePlaceSearch = (query: string, focus: { lat: number; lng: number } | null) => {
  const q = query.trim();
  // Rounded like the backend cache key, so small moves don't refetch
  const focusKey = focus ? [Math.round(focus.lat * 100) / 100, Math.round(focus.lng * 100) / 100] : null;
  return useQuery({
    queryKey: ["places", q.toLowerCase(), focusKey],
    queryFn: async ({ signal }) =>
      (
        await api.get<PlaceSuggestion[]>("/api/v1/geocode/autocomplete", {
          params: { q, lat: focusKey?.[0], lng: focusKey?.[1] },
          signal,
        })
      ).data,
    enabled: q.length >= 2,
    staleTime: 10 * 60 * 1000,
    placeholderData: (previous) => previous, // keep old suggestions visible while typing
  });
};

export const useTripPlan = () =>
  useMutation({
    mutationFn: async (req: TripPlanRequest) =>
      (await api.post<TripPlanResult>("/api/v1/trips/plan", req)).data,
  });
