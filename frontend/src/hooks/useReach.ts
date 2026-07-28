import { useQuery, useMutation } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { Vehicle, ReachEstimateRequest, ReachEstimateResult } from "@/types/reach";

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