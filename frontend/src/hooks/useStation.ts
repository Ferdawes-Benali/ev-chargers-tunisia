import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { StationDetail } from "@/types/station";

export const useStation = (id: string | undefined) =>
  useQuery({
    queryKey: ["station", id],
    queryFn: async () => (await api.get<StationDetail>(`/api/v1/stations/${id}`)).data,
    enabled: !!id,
  });