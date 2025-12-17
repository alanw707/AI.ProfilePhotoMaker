// @ts-check

function tryParseUrl(value) {
  if (!value) return null;

  try {
    return new URL(value);
  } catch {
    // Allow passing hostnames without a scheme, e.g. "app.aiprofilephotomaker.com".
    try {
      return new URL(`https://${value}`);
    } catch {
      return null;
    }
  }
}

function getHostname(value) {
  const parsed = tryParseUrl(value);
  return parsed?.hostname || '';
}

function getHostAndPort(value) {
  const parsed = tryParseUrl(value);
  if (!parsed) return { hostname: '', port: '' };
  return { hostname: parsed.hostname, port: parsed.port };
}

function isProductionAppBaseUrl(value) {
  // In our Playwright config, "unset" implies production defaults.
  if (!value) return true;
  return getHostname(value) === 'app.aiprofilephotomaker.com';
}

function isStagingAppBaseUrl(value) {
  return getHostname(value) === 'staging-app.aiprofilephotomaker.com';
}

module.exports = {
  tryParseUrl,
  getHostname,
  getHostAndPort,
  isProductionAppBaseUrl,
  isStagingAppBaseUrl,
};

