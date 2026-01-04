const { tryParseUrl } = require('./url-utils');

const appBaseUrl = process.env.TEST_BASE_URL || 'http://localhost:4200';
const parsed = tryParseUrl(appBaseUrl);
const appBaseUrlRoot = (parsed ? parsed.origin : appBaseUrl).replace(/\/+$/, '');

function buildAppUrl(pathname = '/') {
  const normalized = pathname.startsWith('/') ? pathname : `/${pathname}`;
  return `${appBaseUrlRoot}${normalized}`;
}

module.exports = {
  appBaseUrl,
  appBaseUrlRoot,
  buildAppUrl,
};
