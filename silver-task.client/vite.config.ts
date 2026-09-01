import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import plugin from '@vitejs/plugin-react';
import fs from 'fs';
import path from 'path';
import child_process from 'child_process';
import { env } from 'process';

// Phase 51 — the repo-root VERSION file is the single authoritative version (Silver-Task.Server.csproj
// reads the same file via MSBuild). package.json's own version field can't be computed from it directly
// (npm requires a static value), so this fails fast on drift instead.
const clientRoot = fileURLToPath(new URL('.', import.meta.url));
const authoritativeVersion = fs.readFileSync(path.join(clientRoot, '..', 'VERSION'), 'utf-8').trim();
const packageJson = JSON.parse(fs.readFileSync(path.join(clientRoot, 'package.json'), 'utf-8'));
if (packageJson.version !== authoritativeVersion) {
    throw new Error(
        `Version mismatch: VERSION file says "${authoritativeVersion}" but silver-task.client/package.json says "${packageJson.version}". Update package.json's "version" field to match.`
    );
}

const baseFolder =
    env.APPDATA !== undefined && env.APPDATA !== ''
        ? `${env.APPDATA}/ASP.NET/https`
        : `${env.HOME}/.aspnet/https`;

const certificateName = "silver-task.client";
const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

if (!fs.existsSync(baseFolder)) {
    fs.mkdirSync(baseFolder, { recursive: true });
}

if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
    if (0 !== child_process.spawnSync('dotnet', [
        'dev-certs',
        'https',
        '--export-path',
        certFilePath,
        '--format',
        'Pem',
        '--no-password',
    ], { stdio: 'inherit', }).status) {
        throw new Error("Could not create certificate.");
    }
}

const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
    env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'https://localhost:7001';

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin()],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    server: {
        proxy: {
            '^/api': {
                target,
                secure: false
            },
            // SignalR (Phase 36) — ws: true so the proxy upgrades the negotiate connection to a
            // real WebSocket instead of treating /hubs/* as a plain HTTP passthrough.
            '^/hubs': {
                target,
                secure: false,
                ws: true
            }
        },
        port: parseInt(env.DEV_SERVER_PORT || '42665'),
        https: {
            key: fs.readFileSync(keyFilePath),
            cert: fs.readFileSync(certFilePath),
        }
    }
})
