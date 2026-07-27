import { useAuth } from "@/hooks/useAuth";
import { useProfile } from "@/hooks/useProfile";
import { Navigate } from "react-router-dom";

export default function ProfilePage() {
  const { isLoggedIn, loading: authLoading, session } = useAuth();
  const { data: profile, isLoading } = useProfile(isLoggedIn);

  if (authLoading) return <div className="p-4">Loading...</div>;
  if (!isLoggedIn) return <Navigate to="/login" replace />;

  return (
    <div className="p-4 space-y-4">
      <h1 className="text-xl font-bold">Profile</h1>
      <p>{session?.user.email}</p>

      {isLoading ? (
        <p className="text-muted-foreground">Loading profile...</p>
      ) : (
        <div>
          <h2 className="font-semibold mb-2">Favorites</h2>
          {profile && profile.favoriteStationIds.length === 0 ? (
            <p className="text-sm text-muted-foreground">No favorites yet.</p>
          ) : (
            <ul className="text-sm space-y-1">
              {profile?.favoriteStationIds.map((id) => (
                <li key={id}>{id}</li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}