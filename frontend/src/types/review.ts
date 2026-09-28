export interface Review {
  id: string;
  rating: number;
  comment: string | null;
  createdAt: string; // ISO 8601, UTC
  authorName: string;
  userId: string | null;
}

export interface SaveReviewInput {
  rating: number;
  comment: string | null;
}

/** POST answers 201 for a new review, 200 when the user's existing review was replaced. */
export type SaveReviewOutcome = "created" | "updated";
