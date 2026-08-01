import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

/** Same shape as StationCard: icon, name on up to two lines, status and rating. */
export default function StationCardSkeleton() {
  return (
    <Card className="gap-3 p-4">
      <div className="flex items-start gap-3">
        <Skeleton className="size-10 shrink-0 rounded-lg" />
        <div className="flex-1 space-y-2 pt-1">
          <Skeleton className="h-4 w-4/5" />
          <Skeleton className="h-4 w-1/2" />
        </div>
      </div>
      <div className="flex items-center justify-between">
        <Skeleton className="h-6 w-20 rounded-full" />
        <Skeleton className="h-4 w-12" />
      </div>
    </Card>
  );
}
