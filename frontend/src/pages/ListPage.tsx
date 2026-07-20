import { useStations } from "../hooks/useStations";

export default function ListPage() {
  const { data, isLoading, error } = useStations();

  if (isLoading) return <div className="p-4">Loading...</div>;
  if (error) return <div className="p-4">Error loading stations.</div>;
  if (!data || data.data.length === 0) return <div className="p-4">No stations yet.</div>;

  return (
    <div className="p-4">
      <h1 className="text-xl font-bold mb-4">Stations</h1>
      <ul className="space-y-2">
        {data.data.map((s) => (
          <li key={s.id} className="border p-2 rounded">
            {s.name} — {s.status}
          </li>
        ))}
      </ul>
    </div>
  );
}