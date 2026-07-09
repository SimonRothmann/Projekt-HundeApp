import { defineConfig } from "vitest/config";
import path from "node:path";

export default defineConfig({
  resolve: {
    alias: {
      "@": path.resolve(__dirname, "src"),
    },
  },
  test: {
    // Reine Logik-Tests (Geometrie, Kalman, IndexedDB-Wrapper) - kein DOM
    // nötig. IndexedDB kommt aus fake-indexeddb (per Import im Test).
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
