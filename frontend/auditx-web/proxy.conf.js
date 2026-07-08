// Dev-server API proxy. The target resolves in this order:
//   1. AUDITX_API_URL environment variable, if set;
//   2. a gitignored `proxy.target.local` file (one line, e.g. http://localhost:8085) — the
//      local-machine escape hatch, mirroring docker-compose.override.yml, for boxes where the
//      API is not on the standard port (e.g. 8080 is already taken by another project);
//   3. the committed default http://localhost:8080 (matches docker-compose.yml + the runbook).
const fs = require('fs');
const path = require('path');

function resolveTarget() {
  if (process.env.AUDITX_API_URL) {
    return process.env.AUDITX_API_URL;
  }
  const localFile = path.join(__dirname, 'proxy.target.local');
  if (fs.existsSync(localFile)) {
    const value = fs.readFileSync(localFile, 'utf8').trim();
    if (value) {
      return value;
    }
  }
  return 'http://localhost:8080';
}

const target = resolveTarget();
// eslint-disable-next-line no-console
console.log(`[proxy] /api -> ${target}`);

module.exports = {
  '/api': {
    target,
    secure: false,
    changeOrigin: true,
    logLevel: 'debug',
  },
};
