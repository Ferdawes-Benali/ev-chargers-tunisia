import { useId, type ReactNode } from "react";
import type { LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";

interface SectionCardProps {
  title: ReactNode;
  /** Id of the heading, for aria-labelledby; generated when omitted. */
  titleId?: string;
  icon?: LucideIcon;
  description?: ReactNode;
  /** Shown at the end of the header row. */
  action?: ReactNode;
  className?: string;
  children: ReactNode;
}

/** A titled card section: a <section> labelled by its <h2>. */
export default function SectionCard({ title, titleId, icon: Icon, description, action, className, children }: SectionCardProps) {
  const generatedId = useId();
  const headingId = titleId ?? generatedId;

  return (
    <section
      aria-labelledby={headingId}
      className={cn("rounded-xl border bg-card text-card-foreground shadow-sm", className)}
    >
      <div className="flex items-start gap-3 px-4 pt-4 sm:px-5 sm:pt-5">
        {Icon && (
          <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/12 text-success-ink">
            <Icon aria-hidden="true" className="size-5" />
          </span>
        )}
        <div className="min-w-0 flex-1 space-y-1">
          <h2 id={headingId} className={cn("text-base font-semibold", Icon ? "leading-9" : "leading-snug")}>{title}</h2>
          {description && <div className="text-sm text-muted-foreground">{description}</div>}
        </div>
        {action && <div className="shrink-0">{action}</div>}
      </div>
      <div className="space-y-4 p-4 sm:p-5">{children}</div>
    </section>
  );
}
