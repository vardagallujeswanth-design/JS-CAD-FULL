const url = 'http://localhost:5000/api/providers/1/folders';
const body = {
  applicationId: 1,
  providerId: 1,
  sourceFolder: 'C:/temp/source',
  doneFolder: 'C:/temp/done',
  errorFolder: 'C:/temp/error',
  retryFolder: 'C:/temp/retry',
  otherAgencyFolder: 'C:/temp/other',
};

async function test() {
  try {
    const response = await fetch(url, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    console.log('STATUS', response.status);
    console.log('OK', response.ok);
    const text = await response.text();
    console.log('BODY', text || '<empty>');
  } catch (err) {
    console.error('ERROR', err);
  }
}

test();
