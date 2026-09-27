import { useParams } from "react-router-dom";
import { useStation } from "@/hooks/useStation";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { useAuth } from "@/hooks/useAuth";
import { useProfile } from "@/hooks/useProfile";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";

export default function DetailPage() {
  const { id } = useParams();
  const { data: station, isLoading, error } = useStation(id);
  const { isLoggedIn } = useAuth();
  const { data: profile } = useProfile(isLoggedIn);
  const queryClient = useQueryClient();

  const verifyMutation = useMutation({
    mutationFn: async () => api.post(`/api/v1/stations/${id}/verify`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["station", id] });
    },
});
  if (isLoading) {
    return (
      <div className="p-4 space-y-3">
        <Skeleton className="h-8 w-1/2" />
        <Skeleton className="h-4 w-1/3" />
        <Skeleton className="h-24 w-full" />
      </div>
    );
  }

  if (error || !station) {
    return <div className="p-4 text-center text-muted-foreground">Station not found.</div>;
  }

  const directionsUrl = `https://www.google.com/maps/search/?api=1&query=${station.lat},${station.lng}`;

  return (
    <div className="p-4 space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">{station.name}</h1>
        <Badge variant={station.status === "Verified" ? "default" : "secondary"}>
          {station.status}
        </Badge>
      </div>

      {profile?.isAdmin && station.status === "Pending" && (
        <Button size="sm" onClick={() => verifyMutation.mutate()} disabled={verifyMutation.isPending}>
          {verifyMutation.isPending ? "Verifying..." : "Verify Station (Admin)"}
        </Button>
      )}

      {station.address && <p className="text-muted-foreground">{station.address}</p>}
      {station.operatorName && <p className="text-sm">Operator: {station.operatorName}</p>}

      <div>
        <h2 className="font-semibold mb-2">Connectors</h2>
        {station.connectors.length === 0 ? (
          <p className="text-sm text-muted-foreground">No connector info yet.</p>
        ) : (
          <ul className="space-y-1">
            {station.connectors.map((c, i) => (
              <li key={i} className="text-sm">
                {c.type} — {c.powerKw}kW × {c.count}
              </li>
            ))}
          </ul>
        )}
      </div>

      <div>
        <h2 className="font-semibold mb-2">
          Reviews {station.avgRating !== null && `(★ ${station.avgRating.toFixed(1)}, ${station.reviewCount})`}
        </h2>
        {station.reviewCount === 0 && (
          <p className="text-sm text-muted-foreground">No reviews yet.</p>
        )}
      </div>

      <a
        href={directionsUrl}
        target="_blank"
        rel="noopener noreferrer"
        className="inline-block underline text-sm"
      >
        Get directions →
      </a>
    </div>
  );
}