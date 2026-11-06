import http from 'node:http';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { resolve, extname, sep } from 'node:path';

const root = fileURLToPath(new URL('.', import.meta.url));
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.json': 'application/json' };
const server = http.createServer(async (req, res) => {
  try {
    // Dev-only export sink (Unity port): POST /__export/<name>.json from this machine only,
    // written to web-proto/.export/. See tools/export-unity.js.
    if (req.method === 'POST' && req.url.startsWith('/__export/')) {
      const local = ['127.0.0.1', '::1', '::ffff:127.0.0.1'].includes(req.socket.remoteAddress);
      const name = req.url.slice('/__export/'.length);
      if (!local || !/^[a-z0-9_-]+\.json$/i.test(name)) { res.writeHead(403); return res.end('Forbidden'); }
      const chunks = []; for await (const c of req) chunks.push(c);
      const dir = resolve(root, '.export'); await mkdir(dir, { recursive: true });
      await writeFile(resolve(dir, name), Buffer.concat(chunks));
      res.writeHead(200); return res.end('ok');
    }
    if (!['GET', 'HEAD'].includes(req.method)) {
      res.writeHead(405, { Allow: 'GET, HEAD' }); return res.end();
    }
    const pathname = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
    const path = resolve(root, '.' + (pathname === '/' ? '/index.html' : pathname));
    if (!path.startsWith(resolve(root) + sep)) {
      res.writeHead(403); return res.end('Forbidden');
    }
    const data = await readFile(path);
    res.writeHead(200, { 'Content-Type': (types[extname(path)] || 'application/octet-stream') + '; charset=utf-8', 'Cache-Control': 'no-cache' });
    res.end(req.method === 'HEAD' ? undefined : data);
  } catch (error) {
    res.writeHead(error.code === 'ENOENT' ? 404 : 400);
    res.end(error.code === 'ENOENT' ? 'Not found' : 'Bad request');
  }
});
server.listen(5173, '0.0.0.0', () => console.log('Motion lab: http://localhost:5173'));
server.on('error', error => { console.error(error.message); process.exitCode = 1; });
