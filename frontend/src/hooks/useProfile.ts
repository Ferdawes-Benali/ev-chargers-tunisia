import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { StationListItem } from "@/types/station";

interface UserProfile {
  id: string;
  displayName: string | null;
  avatarUrl: string | null;
  isAdmin: boolean;
  favoriteStationIds: string[];
  /** "fr" | "ar" | "en": emails use it; applied after login when the browser has no saved choice. */
  preferredLanguage: string;
}

export const useProfile = (enabled: boolean) =>
  useQuery({
    queryKey: ["me"],
    queryFn: async () => (await api.get<UserProfile>("/api/v1/me")).data,
    enabled,
  });

/** The user's favorite chargers, in the order they were saved. */
export const useFavorites = (enabled: boolean) =>
  useQuery({
    queryKey: ["favorites"],
    queryFn: async () => (await api.get<StationListItem[]>("/api/v1/me/favorites")).data,
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
      // The heart state (profile) and the favorites list on the Profile page
      queryClient.invalidateQueries({ queryKey: ["me"] });
      queryClient.invalidateQueries({ queryKey: ["favorites"] });
    },
  });
};
