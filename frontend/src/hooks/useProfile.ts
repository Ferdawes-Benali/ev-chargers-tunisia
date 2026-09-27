import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";

interface UserProfile {
  id: string;
  displayName: string | null;
  avatarUrl: string | null;
  isAdmin: boolean;
  favoriteStationIds: string[];
}

export const useProfile = (enabled: boolean) =>
  useQuery({
    queryKey: ["me"],
    queryFn: async () => (await api.get<UserProfile>("/api/v1/me")).data,
    enabled,
  });

export const useToggleFavorite = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ stationId, isFavorite }: { stationId: string; isFavorite: boolean }) => {
      if (isFavorite) {
        await api.delete(`/api/v1/me/favorites/${stationId}`);
      } else {
        await api.put(`/api/v1/me/favorites/${stationId}`);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["me"] });
    },
  });
};