import { useId, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import axios from "axios";
import { LogIn, MessageSquareText } from "lucide-react";
import { useAuth } from "@/hooks/useAuth";
import { useProfile } from "@/hooks/useProfile";
import { useReviews, useSaveReview } from "@/hooks/useReviews";
import { relativeTime } from "@/lib/relativeTime";
import { useLocale } from "@/lib/format";
import { cn } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { StarRatingDisplay, StarRatingInput } from "@/components/StarRating";
import SectionCard from "@/components/SectionCard";
import type { Review, SaveReviewOutcome } from "@/types/review";
import type { StationDetail } from "@/types/station";

const MAX_COMMENT = 1000;

/** A translation key, so the notice follows a language change. */
type Notice = { kind: "success" | "error"; key: string };

const SUCCESS_KEY: Record<SaveReviewOutcome, string> = {
  created: "reviews.created",
  updated: "reviews.updated",
};

export default function ReviewsSection({ station }: { station: StationDetail }) {
  const { t } = useTranslation();
  const { number } = useLocale();
  const { isLoggedIn, loading: authLoading } = useAuth();
  const { data: profile } = useProfile(isLoggedIn);
  const { data: reviews, isLoading, isError } = useReviews(station.id);
  // Lives here, not in the form: the form remounts once a first review exists
  const [notice, setNotice] = useState<Notice | null>(null);

  const myReview = profile ? reviews?.find((r) => r.userId === profile.id) : undefined;
  const sorted = [...(reviews ?? [])].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt));

  return (
    <SectionCard
      title={t("reviews.title")}
      titleId="reviews-heading"
      icon={MessageSquareText}
      description={
        station.avgRating !== null && station.reviewCount > 0 && (
          <p className="flex flex-wrap items-center gap-2">
            <StarRatingDisplay rating={station.avgRating} />
            <span className="tabular-nums">
              <span className="font-semibold text-foreground">{number(station.avgRating, { minimumFractionDigits: 1, maximumFractionDigits: 1 })}</span>
              <span>
                {" "}· {t("reviews.count", { count: station.reviewCount })}
              </span>
            </span>
          </p>
        )
      }
    >

      {!authLoading &&
        (isLoggedIn ? (
          <ReviewForm
            // Remount when the user's own review shows up, so the form is prefilled with it
            key={myReview?.id ?? "new"}
            stationId={station.id}
            existing={myReview}
            onSaved={(outcome) => setNotice({ kind: "success", key: SUCCESS_KEY[outcome] })}
            onError={(key) => setNotice({ kind: "error", key })}
            onEdit={() => setNotice(null)}
          />
        ) : (
          <p className="text-sm">
            <Link to="/login" className={buttonVariants({ variant: "outline", className: "gap-2" })}>
              <LogIn aria-hidden="true" className="rtl:-scale-x-100" />
              {t("reviews.loginToReview")}
            </Link>
          </p>
        ))}

      {notice && (
        <p
          role={notice.kind === "error" ? "alert" : "status"}
          className={cn(
            "rounded-lg border p-3 text-sm",
            notice.kind === "error" ? "border-danger/40 bg-danger/10 text-destructive" : "border-success/40 bg-success/10 text-success-ink",
          )}
        >
          {t(notice.key)}
        </p>
      )}

      {isLoading ? (
        <div className="space-y-2" aria-busy="true" aria-label={t("common.loading")}>
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
      ) : isError ? (
        <p className="text-sm text-muted-foreground">{t("reviews.loadError")}</p>
      ) : sorted.length === 0 ? (
        <p className="text-sm text-muted-foreground">{t("reviews.empty")}</p>
      ) : (
        <ul className="divide-y overflow-hidden rounded-xl ring-1 ring-border">
          {sorted.map((review) => (
            <ReviewItem key={review.id} review={review} isMine={review.id === myReview?.id} />
          ))}
        </ul>
      )}
    </SectionCard>
  );
}

function ReviewItem({ review, isMine }: { review: Review; isMine: boolean }) {
  const { t } = useTranslation();
  const { locale } = useLocale();
  return (
    <li className={cn("flex gap-3 p-4", isMine && "bg-primary/5")}>
      {/* Initial of the author, decorative */}
      <span aria-hidden="true" className="grid size-9 shrink-0 place-items-center rounded-full bg-muted text-sm font-semibold text-muted-foreground uppercase">
        {review.authorName.trim().charAt(0)}
      </span>
      <div className="min-w-0 flex-1 space-y-1.5">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
          <span className="text-sm font-semibold">{review.authorName}</span>
          {isMine && <Badge className="border-primary/40 bg-primary/12 text-success-ink">{t("reviews.you")}</Badge>}
          <time
            dateTime={review.createdAt}
            title={new Date(review.createdAt).toLocaleString(locale)}
            className="ms-auto text-xs text-muted-foreground"
          >
            {relativeTime(review.createdAt, locale)}
          </time>
        </div>
        <StarRatingDisplay rating={review.rating} />
        {review.comment && <p className="text-sm whitespace-pre-line wrap-break-word">{review.comment}</p>}
      </div>
    </li>
  );
}

interface ReviewFormProps {
  stationId: string;
  existing: Review | undefined;
  onSaved: (outcome: SaveReviewOutcome) => void;
  /** Receives a translation key. */
  onError: (key: string) => void;
  onEdit: () => void;
}

function ReviewForm({ stationId, existing, onSaved, onError, onEdit }: ReviewFormProps) {
  const { t } = useTranslation();
  const [rating, setRating] = useState(existing?.rating ?? 0);
  const [comment, setComment] = useState(existing?.comment ?? "");
  const save = useSaveReview(stationId);
  const ratingLabelId = useId();
  const commentId = useId();
  const counterId = useId();

  const submit = (e: FormEvent) => {
    e.preventDefault();
    if (rating === 0) return;
    const trimmed = comment.trim();
    save.mutate(
      { rating, comment: trimmed === "" ? null : trimmed },
      {
        onSuccess: onSaved,
        onError: (err) =>
          onError(
            axios.isAxiosError(err) && err.response?.status === 401 ? "errors.loginAgain" : "reviews.saveError",
          ),
      },
    );
  };

  return (
    <Card className="bg-muted/40 shadow-none">
      <CardHeader>
        <CardTitle className="font-semibold">{existing ? t("reviews.edit") : t("reviews.write")}</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="space-y-3">
          <div className="space-y-1">
            <p id={ratingLabelId} className="text-sm font-medium">{t("reviews.yourRating")}</p>
            <StarRatingInput
              value={rating}
              onChange={(value) => {
                setRating(value);
                onEdit();
              }}
              labelledBy={ratingLabelId}
            />
          </div>

          <div className="space-y-1">
            <label htmlFor={commentId} className="text-sm font-medium">
              {t("reviews.comment")} <span className="font-normal text-muted-foreground">{t("reviews.optional")}</span>
            </label>
            <textarea
              id={commentId}
              value={comment}
              onChange={(e) => {
                setComment(e.target.value);
                onEdit();
              }}
              maxLength={MAX_COMMENT}
              rows={4}
              aria-describedby={counterId}
              placeholder={t("reviews.placeholder")}
              className="w-full min-w-0 rounded-lg border border-input bg-card px-3 py-2 text-base transition-colors outline-none placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 md:text-sm"
            />
            <p id={counterId} className="text-end text-xs text-muted-foreground tabular-nums">
              {comment.length}/{MAX_COMMENT}
            </p>
          </div>

          <div className="flex items-center gap-3">
            <Button type="submit" disabled={rating === 0 || save.isPending}>
              {save.isPending ? t("reviews.saving") : existing ? t("reviews.update") : t("reviews.submit")}
            </Button>
            {rating === 0 && <span className="text-xs text-muted-foreground">{t("reviews.chooseRating")}</span>}
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
