import { useTranslation } from "react-i18next";
import { BadgeCheck, CircleX, Clock } from "lucide-react";
import { cn } from "@/lib/utils";

const STYLES: Record<string, { className: string; icon: typeof BadgeCheck | null }> = {
  Verified: { className: "border-success/30 bg-success/10 text-success-ink", icon: BadgeCheck },
  Pending: { className: "border-warning/35 bg-warning/10 text-warning-ink", icon: Clock },
  Rejected: { className: "border-danger/30 bg-danger/10 text-danger-ink", icon: CircleX },
};
const FALLBACK = { className: "border-border bg-muted text-muted-foreground", icon: null };

/** A charger's review status: Verified → success, Pending → warning, Rejected → danger. */
export default function StatusBadge({ status, className }: { status: string; className?: string }) {
  const { t } = useTranslation();
  const { className: tone, icon: Icon } = STYLES[status] ?? FALLBACK;

  return (
    <span
      className={cn(
        "inline-flex h-6 shrink-0 items-center gap-1 rounded-full border px-2.5 text-xs font-medium whitespace-nowrap",
        tone,
        className,
      )}
    >
      {Icon && <Icon aria-hidden="true" className="size-3.5" />}
      {t(`common.status.${status}`, { defaultValue: status })}
    </span>
  );
}
