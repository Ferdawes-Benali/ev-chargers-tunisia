import { cn } from "@/lib/utils";
import type { CompanionPlace } from "@/types/companion";
import { openStatusText } from "./meta";

/** Green when open, muted when closed, nothing when unknown. */
export default function OpenStatusText({ place, className }: { place: CompanionPlace; className?: string }) {
  const text = openStatusText(place);
  if (!text) return null;
  return (
    <span
      className={cn(
        "text-xs",
        place.openStatus === "open" ? "text-emerald-700 dark:text-emerald-400" : "text-muted-foreground",
        className,
      )}
    >
      {text}
    </span>
  );
}
