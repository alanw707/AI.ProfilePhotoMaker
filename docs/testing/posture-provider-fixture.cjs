// Local Docker fixture only. Captures image/prompt boundaries, never credentials or headers.
const http = require('node:http');
const fs = require('node:fs');
const crypto = require('node:crypto');
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');
const source = Buffer.from(process.env.FIXTURE_SOURCE_PNG, 'base64');
const response = Buffer.from(process.env.FIXTURE_RESPONSE_PNG, 'base64');
let count = 0;
fs.mkdirSync('/evidence/posture-chain', { recursive: true });
fs.writeFileSync('/evidence/posture-chain/count.json', JSON.stringify({ requests: count }));
http.createServer(async (req, res) => {
  if (req.method !== 'POST' || req.url !== '/v1/images/edits') {
    res.writeHead(404); res.end(); return;
  }
  try {
    const chunks = [];
    let size = 0;
    for await (const chunk of req) {
      size += chunk.length;
      if (size > 20 * 1024 * 1024) throw Error('Fixture request too large');
      chunks.push(chunk);
    }
    // Parse this fixture's flat multipart shape without decoding binary image bytes.
    // Node's native FormData parser rejected the actual .NET multipart request.
    const boundary = /boundary="?([^";]+)"?/.exec(req.headers['content-type'] || '')?.[1];
    if (!boundary) throw Error('Missing multipart boundary');
    const body = Buffer.concat(chunks);
    const separator = Buffer.from('\r\n--' + boundary);
    const fields = new Map();
    let cursor = body.indexOf(Buffer.from('--' + boundary)) + boundary.length + 2;
    while (body.subarray(cursor, cursor + 2).toString() !== '--') {
      const headerEnd = body.indexOf(Buffer.from('\r\n\r\n'), cursor);
      if (headerEnd < 0) throw Error('Malformed multipart headers');
      const header = body.subarray(cursor, headerEnd).toString('utf8');
      const name = /(?:^|;)\s*name=(?:"([^"]+)"|([^;\r\n]+))/m.exec(header);
      const end = body.indexOf(separator, headerEnd + 4);
      if (!name || end < 0) throw Error('Malformed multipart field');
      fields.set(name[1] || name[2], body.subarray(headerEnd + 4, end));
      cursor = end + separator.length;
    }
    const input = fields.get('image');
    if (!input?.length) throw Error('Missing image');
    const prompt = fields.get('prompt')?.toString('utf8') || '';
    const posture = prompt.includes('Gently straighten shoulder posture.');
    const output = posture ? response : source;
    count++;
    const metadata = { requests: count, posture, model: fields.get('model')?.toString('utf8'),
      prompt, inputHash: hash(input), outputHash: hash(output) };
    fs.writeFileSync('/evidence/posture-chain/input.png', input);
    fs.writeFileSync('/evidence/posture-chain/output.png', output);
    fs.writeFileSync('/evidence/posture-chain/capture.json', JSON.stringify(metadata));
    fs.writeFileSync('/evidence/posture-chain/count.json', JSON.stringify({ requests: count }));
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ data: [{ b64_json: output.toString('base64') }] }));
  } catch (error) {
    fs.writeFileSync('/evidence/posture-chain/fixture-error.txt', String(error));
    res.writeHead(500, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ error: 'Local fixture capture failed' }));
  }
}).listen(5055, '0.0.0.0');
