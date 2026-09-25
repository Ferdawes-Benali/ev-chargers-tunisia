import { Outlet, Link } from "react-router-dom";

export default function Layout() {
  return (
    <div className="min-h-screen flex flex-col">
      <header className="border-b px-4 py-3 flex gap-4 items-center">
        <span className="font-bold">EV Chargers Tunisia</span>
        <nav className="flex gap-3 text-sm">
          <Link to="/">Map</Link>
          <Link to="/stations">List</Link>
          <Link to="/submit">Submit</Link>
          <Link to="/profile">Profile</Link>
        </nav>
      </header>
      <main className="flex-1">
        <Outlet />
      </main>
    </div>
  );
}