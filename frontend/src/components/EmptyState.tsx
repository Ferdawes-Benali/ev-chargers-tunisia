import type { ReactNode } from "react";
import type { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  text?: string;
  /** A button or link, e.g. to clear filters. */
  action?: ReactNode;
  tone?: "neutral" | "danger";
  className?: string;
}

/** Centered placeholder for empty lists and load errors. */
export default function EmptyState({ icon: Icon, title, text, action, tone = "neutral", className }: EmptyStateProps) {
  return (
    <div
      className={cn(
        "flex flex-col items-center gap-3 rounded-xl border border-dashed bg-card/60 px-6 py-10 text-center",
        className,
      )}
    >
      <span
        className={cn(
          "grid size-12 place-items-center rounded-full",
          tone === "danger" ? "bg-danger/10 text-danger-ink" : "bg-muted text-muted-foreground",
        )}
      >
        <Icon aria-hidden="true" className="size-6" />
      </span>
      <div className="max-w-sm space-y-1">
        <p className="font-medium">{title}</p>
        {text && <p className="text-sm text-muted-foreground">{text}</p>}
      </div>
      {action}
    </div>
  );
}
