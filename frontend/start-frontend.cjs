const { spawn } = require('node:child_process');
const { readFileSync } = require('node:fs');
const path = require('node:path');

if (!process.env.HUNGER_API_URL) {
  const launchSettingsPath = path.join(__dirname, '..', 'backend', 'Properties', 'launchSettings.json');
  const launchSettings = JSON.parse(readFileSync(launchSettingsPath, 'utf8'));
  const apiUrl = Object.values(launchSettings.profiles).find((profile) => profile.applicationUrl)?.applicationUrl;

  if (!apiUrl) {
    throw new Error('Set HUNGER_API_URL or configure an API URL in backend launch settings.');
  }

  process.env.HUNGER_API_URL = apiUrl.split(';')[0];
}

const angularCli = path.join(__dirname, 'node_modules', '@angular', 'cli', 'bin', 'ng.js');
const server = spawn(process.execPath, [angularCli, 'serve', '--host', '127.0.0.1'], {
  cwd: __dirname,
  stdio: 'inherit',
});

server.on('error', (error) => {
  console.error('Could not start the Angular development server:', error);
  process.exitCode = 1;
});

server.on('exit', (code) => {
  process.exitCode = code ?? 1;
});

for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => server.kill(signal));
}
