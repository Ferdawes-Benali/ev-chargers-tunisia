import { useId, useState } from "react";
import { Star } from "lucide-react";
import { cn } from "@/lib/utils";

const STARS = [1, 2, 3, 4, 5] as const;

const starLabel = (n: number) => `${n} ${n === 1 ? "star" : "stars"}`;

/** Read-only stars. Rounds to the nearest whole star; the exact value is in the label. */
export function StarRatingDisplay({ rating, className }: { rating: number; className?: string }) {
  const filled = Math.round(rating);
  return (
    <span role="img" aria-label={`${rating} out of 5 stars`} className={cn("inline-flex gap-0.5", className)}>
      {STARS.map((n) => (
        <Star
          key={n}
          aria-hidden="true"
          className={cn("size-4", n <= filled ? "fill-amber-400 text-amber-400" : "text-muted-foreground/40")}
        />
      ))}
    </span>
  );
}

interface StarRatingInputProps {
  value: number; // 0 = nothing chosen yet
  onChange: (value: number) => void;
  labelledBy: string;
}

/**
 * 1–5 star picker built on native radio buttons, so arrow keys, Tab and screen readers
 * behave like any radio group. The inputs are visually hidden; their label shows the star.
 */
export function StarRatingInput({ value, onChange, labelledBy }: StarRatingInputProps) {
  const name = useId();
  const [hovered, setHovered] = useState(0);
  const shown = hovered || value;

  return (
    <div
      role="radiogroup"
      aria-labelledby={labelledBy}
      className="flex gap-1"
      onMouseLeave={() => setHovered(0)}
    >
      {STARS.map((n) => (
        <label
          key={n}
          onMouseEnter={() => setHovered(n)}
          className="cursor-pointer rounded-md p-0.5 has-focus-visible:ring-3 has-focus-visible:ring-ring/50"
        >
          <input
            type="radio"
            name={name}
            value={n}
            checked={value === n}
            onChange={() => onChange(n)}
            aria-label={starLabel(n)}
            className="sr-only"
          />
          <Star
            aria-hidden="true"
            className={cn(
              "size-7 transition-colors",
              n <= shown ? "fill-amber-400 text-amber-400" : "text-muted-foreground/50",
            )}
          />
        </label>
      ))}
    </div>
  );
}
