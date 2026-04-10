const http = require('http');
const data = JSON.stringify({ applicationId: 1, providerId: 3, sourceFolder: 'fah', doneFolder: 'fah', errorFolder: 'fah', retryFolder: '', otherAgencyFolder: '' });
const options = {
  hostname: 'localhost',
  port: 5000,
  path: '/api/providers/3/folders',
  method: 'PUT',
  headers: {
    'Content-Type': 'application/json',
    'Content-Length': Buffer.byteLength(data),
  },
};
const req = http.request(options, (res) => {
  console.log('status', res.statusCode);
  console.log('headers', res.headers);
  let body = '';
  res.on('data', (chunk) => (body += chunk));
  res.on('end', () => console.log('body', body));
});
req.on('error', (e) => console.error('error', e));
req.write(data);
req.end();
