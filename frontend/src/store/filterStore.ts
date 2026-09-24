import { create } from "zustand";

interface FilterState {
  connectorType: string | null;
  minPowerKw: number | null;
  setConnectorType: (type: string | null) => void;
  setMinPowerKw: (power: number | null) => void;
}

export const useFilterStore = create<FilterState>((set) => ({
  connectorType: null,
  minPowerKw: null,
  setConnectorType: (connectorType) => set({ connectorType }),
  setMinPowerKw: (minPowerKw) => set({ minPowerKw }),
}));