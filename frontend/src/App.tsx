import { BrowserRouter, Routes, Route } from "react-router-dom";
import Layout from "./components/Layout";
import MapPage from "./pages/MapPage";
import ListPage from "./pages/ListPage";
import DetailPage from "./pages/DetailPage";
import SubmitPage from "./pages/SubmitPage";
import ProfilePage from "./pages/ProfilePage";
import LoginPage from "./pages/LoginPage";
function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/" element={<MapPage />} />
          <Route path="/stations" element={<ListPage />} />
          <Route path="/stations/:id" element={<DetailPage />} />
          <Route path="/submit" element={<SubmitPage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="/login" element={<LoginPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}

export default App;