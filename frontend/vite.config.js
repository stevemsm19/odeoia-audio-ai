import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

const developmentApiTarget = process.env.VITE_DEV_API_TARGET || "http://localhost:5196";

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      "/api": {
        target: developmentApiTarget,
        changeOrigin: true,
      },
    },
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: "./src/setupTests.ts",
  },
});
