import { Outlet, Link, useNavigate } from "react-router-dom";
import { useAuth } from "@/hooks/useAuth";
import { supabase } from "@/lib/supabase";
import { Button } from "@/components/ui/button";

export default function Layout() {
  const { session, isLoggedIn } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await supabase.auth.signOut();
    navigate("/");
  };

  return (
    <div className="min-h-screen flex flex-col">
      <header className="border-b px-4 py-3 flex gap-4 items-center justify-between">
        <div className="flex gap-4 items-center">
          <span className="font-bold">EV Chargers Tunisia</span>
          <nav className="flex gap-3 text-sm">
            <Link to="/">Map</Link>
            <Link to="/stations">List</Link>
            <Link to="/submit">Submit</Link>
            <Link to="/profile">Profile</Link>
          </nav>
        </div>
        <div className="flex items-center gap-2">
          {isLoggedIn ? (
            <>
              <span className="text-sm text-muted-foreground">{session?.user.email}</span>
              <Button size="sm" variant="outline" onClick={handleLogout}>
                Log out
              </Button>
            </>
          ) : (
            <Link to="/login">
              <Button size="sm">Log in</Button>
            </Link>
          )}
        </div>
      </header>
      <main className="flex-1">
        <Outlet />
      </main>
    </div>
  );
}