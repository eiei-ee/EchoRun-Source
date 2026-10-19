// DevTools-only resource server. Run after export; never upload this preview configuration.
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const zlib = require('node:zlib');
const os = require('node:os');
const args = process.argv.slice(2);
if (args.length && (args.length !== 2 || args[0] !== '--host'))
  throw new Error('Usage: node Tools/WeChat/local-preview.cjs [--host <local-private-IPv4>]');
const host = args[1] || '127.0.0.1';
const localAddresses = Object.values(os.networkInterfaces()).flat().filter(Boolean)
  .filter(info => info.family === 'IPv4').map(info => info.address);
const privateAddress = /^(10\.|192\.168\.|172\.(1[6-9]|2\d|3[01])\.)/.test(host);
if (host !== '127.0.0.1' && (!privateAddress || !localAddresses.includes(host)))
  throw new Error('Host must be a private IPv4 address assigned to this computer.');
const root = path.resolve(__dirname, '../../Builds/WeixinMiniGameV0-Clean');
const gamePath = path.join(root, 'minigame/game.js');
const config = JSON.parse(fs.readFileSync(path.join(root, 'minigame/project.config.json'), 'utf8'));
if (!/^wx[0-9a-f]{16}$/.test(config.appid)) throw new Error('Configure the existing Mini Game AppID in the exported project first.');
const port = 18765;
let game = fs.readFileSync(gamePath, 'utf8');
const hash = game.match(/DATA_FILE_MD5:\s*'([0-9a-f]+)'/)?.[1];
if (!hash) throw new Error('Missing exported data hash');
const name = `${hash}.webgl.data.unityweb.bin.txt`;
const resource = path.join(root, 'webgl', name);
const size = fs.statSync(resource).size;
const compressed = zlib.gzipSync(fs.readFileSync(resource));
const server = http.createServer((req, res) => {
  if (req.url === '/health' && req.method === 'GET') {
    res.writeHead(200, {'Content-Type': 'text/plain; charset=utf-8', 'Cache-Control': 'no-store'});
    res.end('EchoRun preview resources ready');
    return;
  }
  if (req.url !== `/${name}` || !['GET', 'HEAD'].includes(req.method)) {
    res.writeHead(404); res.end(); return;
  }
  const gzip = /\bgzip\b/.test(req.headers['accept-encoding'] || '');
  console.log(`Resource ${req.method} from ${req.socket.remoteAddress}; gzip=${gzip}`);
  res.on('finish', () => console.log(`Resource response finished (${gzip ? compressed.length : size} bytes)`));
  res.writeHead(200, {'Content-Type': 'application/octet-stream',
    'Content-Length': gzip ? compressed.length : size,
    ...(gzip ? {'Content-Encoding': 'gzip'} : {}),
    'Vary': 'Accept-Encoding', 'Access-Control-Allow-Origin': '*', 'Cache-Control': 'no-cache'});
  if (req.method === 'HEAD') res.end();
  else if (gzip) res.end(compressed);
  else fs.createReadStream(resource).pipe(res);
});
server.on('error', error => { console.error(error.message); process.exitCode = 1; });
server.listen(port, host, () => {
  game = game.replace(/DATA_CDN:\s*(?:'[^']*'|"[^"]*")/, `DATA_CDN: 'http://${host}:${port}'`)
    .replace(/APPID:\s*'[^']*'/, `APPID: '${config.appid}'`);
  fs.writeFileSync(gamePath, game);
  console.log(`Development resource server: http://${host}:${port} (${size} bytes). Keep this process running. ${host === '127.0.0.1' ? 'Computer-only preview.' : 'Same-network phone debugging only.'} Not a release URL.`);
});
