import { useId, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import axios from "axios";
import { useAuth } from "@/hooks/useAuth";
import { useProfile } from "@/hooks/useProfile";
import { useReviews, useSaveReview } from "@/hooks/useReviews";
import { relativeTime } from "@/lib/relativeTime";
import { cn } from "@/lib/utils";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { StarRatingDisplay, StarRatingInput } from "@/components/StarRating";
import type { Review, SaveReviewOutcome } from "@/types/review";
import type { StationDetail } from "@/types/station";

const MAX_COMMENT = 1000;

type Notice = { kind: "success" | "error"; text: string };

const SUCCESS_TEXT: Record<SaveReviewOutcome, string> = {
  created: "Thanks! Your review was published.",
  updated: "Your review was updated.",
};

export default function ReviewsSection({ station }: { station: StationDetail }) {
  const { isLoggedIn, loading: authLoading } = useAuth();
  const { data: profile } = useProfile(isLoggedIn);
  const { data: reviews, isLoading, isError } = useReviews(station.id);
  // Lives here, not in the form: the form remounts once a first review exists
  const [notice, setNotice] = useState<Notice | null>(null);

  const myReview = profile ? reviews?.find((r) => r.userId === profile.id) : undefined;
  const sorted = [...(reviews ?? [])].sort((a, b) => Date.parse(b.createdAt) - Date.parse(a.createdAt));

  return (
    <section aria-labelledby="reviews-heading" className="space-y-3">
      <div>
        <h2 id="reviews-heading" className="font-semibold">Reviews</h2>
        {station.avgRating !== null && station.reviewCount > 0 && (
          <p className="mt-1 flex items-center gap-2 text-sm">
            <StarRatingDisplay rating={station.avgRating} />
            <span>
              <span className="font-medium">{station.avgRating.toFixed(1)}</span>
              <span className="text-muted-foreground">
                {" "}· {station.reviewCount} {station.reviewCount === 1 ? "review" : "reviews"}
              </span>
            </span>
          </p>
        )}
      </div>

      {!authLoading &&
        (isLoggedIn ? (
          <ReviewForm
            // Remount when the user's own review shows up, so the form is prefilled with it
            key={myReview?.id ?? "new"}
            stationId={station.id}
            existing={myReview}
            onSaved={(outcome) => setNotice({ kind: "success", text: SUCCESS_TEXT[outcome] })}
            onError={(text) => setNotice({ kind: "error", text })}
            onEdit={() => setNotice(null)}
          />
        ) : (
          <p className="text-sm">
            <Link to="/login" className="underline underline-offset-4">
              Log in to write a review
            </Link>
          </p>
        ))}

      {notice && (
        <p
          role={notice.kind === "error" ? "alert" : "status"}
          className={cn("text-sm", notice.kind === "error" ? "text-destructive" : "text-emerald-700 dark:text-emerald-400")}
        >
          {notice.text}
        </p>
      )}

      {isLoading ? (
        <div className="space-y-2">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
      ) : isError ? (
        <p className="text-sm text-muted-foreground">Couldn't load reviews.</p>
      ) : sorted.length === 0 ? (
        <p className="text-sm text-muted-foreground">No reviews yet — be the first to share your experience.</p>
      ) : (
        <ul className="divide-y rounded-xl ring-1 ring-foreground/10">
          {sorted.map((review) => (
            <ReviewItem key={review.id} review={review} isMine={review.id === myReview?.id} />
          ))}
        </ul>
      )}
    </section>
  );
}

function ReviewItem({ review, isMine }: { review: Review; isMine: boolean }) {
  return (
    <li className="space-y-1 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-medium">{review.authorName}</span>
        {isMine && <Badge variant="secondary">You</Badge>}
        <StarRatingDisplay rating={review.rating} />
        <time
          dateTime={review.createdAt}
          title={new Date(review.createdAt).toLocaleString("en")}
          className="ml-auto text-xs text-muted-foreground"
        >
          {relativeTime(review.createdAt)}
        </time>
      </div>
      {review.comment && <p className="text-sm whitespace-pre-line break-words">{review.comment}</p>}
    </li>
  );
}

interface ReviewFormProps {
  stationId: string;
  existing: Review | undefined;
  onSaved: (outcome: SaveReviewOutcome) => void;
  onError: (text: string) => void;
  onEdit: () => void;
}

function ReviewForm({ stationId, existing, onSaved, onError, onEdit }: ReviewFormProps) {
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
            axios.isAxiosError(err) && err.response?.status === 401
              ? "Please log in again."
              : "Couldn't save your review. Please try again.",
          ),
      },
    );
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>{existing ? "Edit your review" : "Write a review"}</CardTitle>
      </CardHeader>
      <CardContent>
        <form onSubmit={submit} className="space-y-3">
          <div className="space-y-1">
            <p id={ratingLabelId} className="text-sm font-medium">Your rating</p>
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
              Comment <span className="font-normal text-muted-foreground">(optional)</span>
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
              placeholder="How was charging here? Waiting time, working connectors, access…"
              className="w-full min-w-0 rounded-lg border border-input bg-transparent px-2.5 py-2 text-base transition-colors outline-none placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 md:text-sm dark:bg-input/30"
            />
            <p id={counterId} className="text-right text-xs text-muted-foreground">
              {comment.length}/{MAX_COMMENT}
            </p>
          </div>

          <div className="flex items-center gap-3">
            <Button type="submit" disabled={rating === 0 || save.isPending}>
              {save.isPending ? "Saving…" : existing ? "Update review" : "Submit review"}
            </Button>
            {rating === 0 && <span className="text-xs text-muted-foreground">Choose a rating to continue.</span>}
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
