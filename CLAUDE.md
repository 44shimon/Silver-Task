# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

This is the standard Visual Studio "React and ASP.NET Core" template (net10.0 + React 19/Vite), currently unmodified beyond scaffolding. It has two projects wired together as a solution:

- **`Silver-Task.Server`** — ASP.NET Core Web API (`net10.0`). Entry point `Program.cs`. Currently exposes a single scaffolded `WeatherForecastController`.
- **`silver-task.client`** — React 19 SPA built with Vite. Entry point `src/main.jsx` → `src/App.jsx`.

The server references the client project (`ProjectReference` in `Silver-Task.Server.csproj`) with `ReferenceOutputAssembly=false`, purely so that running/publishing the server also builds the SPA and copies its output into `wwwroot` as static assets (`app.UseDefaultFiles()` / `app.MapStaticAssets()` / `app.MapFallbackToFile("/index.html")` in `Program.cs`). This makes the server the single deployable unit — in production the API and SPA are served from the same origin.

## Dev workflow: SPA proxy

In development the two projects run as separate processes and requests are proxied together — do not expect a single `dotnet run` to serve the built SPA in dev mode:

- `Silver-Task.Server.csproj` sets `SpaProxyLaunchCommand=npm run dev` and `SpaProxyServerUrl=https://localhost:42665`, and references `Microsoft.AspNetCore.SpaProxy`. When the ASP.NET server starts in Development, it auto-launches the Vite dev server and proxies non-API requests to it.
- `silver-task.client/vite.config.js` proxies specific API paths (currently `^/weatherforecast`) to the ASP.NET backend, resolved from `ASPNETCORE_HTTPS_PORT` / `ASPNETCORE_URLS` env vars (falls back to `https://localhost:7001`). **When adding new API routes/controllers, add matching proxy entries here** or client fetches to those routes will fail in dev.
- The Vite dev server listens on port `42665` (overridable via `DEV_SERVER_PORT`) using an HTTPS dev certificate auto-generated via `dotnet dev-certs https` into `%APPDATA%/ASP.NET/https` (or `~/.aspnet/https`) on first run.
- ASP.NET launch profiles (`Properties/launchSettings.json`): `http` → `http://localhost:5056`; `https` → `https://localhost:7001;http://localhost:5056`.

## Commands

Run these from the repective project directory (or open `Silver-Task.slnx` in Visual Studio, which wires up the SPA proxy automatically on F5).

**Server** (`Silver-Task.Server/`):
```
dotnet run                 # starts the API and (in Development) auto-launches the Vite dev server via SpaProxy
dotnet build
```

**Client** (`silver-task.client/`):
```
npm install
npm run dev                # Vite dev server only, expects the ASP.NET backend running separately for API proxying
npm run build               # production build, output consumed by the server via MapStaticAssets
npm run lint                # oxlint
npm run preview
```

There is no test setup in either project yet (no test projects, no test scripts).

## Linting

Client linting uses **oxlint** (not ESLint), configured in `silver-task.client/.oxlintrc.json` with the `react` and `oxc` plugins. Notably `react/rules-of-hooks` is an error and `react/only-export-components` is a warning.

## Architecture notes

- The `@` import alias in `vite.config.js` resolves to `silver-task.client/src`.
- The client is currently plain JSX (not TypeScript). `README.md` in `silver-task.client/` notes that the TS variant (with type-aware oxlint rules) can be added later by following Vite's `create-vite` TS-react template — this hasn't been done yet.
- `RootNamespace` for the server project is `Silver_Task.Server` (underscore, not hyphen).
