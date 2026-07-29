import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { Review, SaveReviewInput, SaveReviewOutcome } from "@/types/review";

export const useReviews = (stationId: string | undefined) =>
  useQuery({
    queryKey: ["reviews", stationId],
    queryFn: async () => (await api.get<Review[]>(`/api/v1/stations/${stationId}/reviews`)).data,
    enabled: !!stationId,
  });

export const useSaveReview = (stationId: string | undefined) => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: SaveReviewInput): Promise<SaveReviewOutcome> => {
      const response = await api.post(`/api/v1/stations/${stationId}/reviews`, input);
      return response.status === 201 ? "created" : "updated";
    },
    onSuccess: () => {
      // The list, the station's average/count, and the ratings shown in the station list and on the map
      queryClient.invalidateQueries({ queryKey: ["reviews", stationId] });
      queryClient.invalidateQueries({ queryKey: ["station", stationId] });
      queryClient.invalidateQueries({ queryKey: ["stations"] });
      queryClient.invalidateQueries({ queryKey: ["stations-bbox"] });
    },
  });
};
