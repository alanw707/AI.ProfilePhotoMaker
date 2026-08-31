# Run after manually choosing Gallery > Refine in the existing authenticated browser:
# BU_CDP_URL=http://172.18.208.1:9223 browser-use < scripts/verify-production-studio.browser.py
# Emits only aggregate counts and booleans. Never prints identifiers, URLs,
# cookies, headers, image sources, or image payloads.
import json


def inspect():
    return js("""(() => {
      const visible = element => {
        const rect = element.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0;
      };
      const text = document.body.innerText;
      const progress = text.match(/(\\d+) of (\\d+) generated/i);
      const remaining = text.match(/(\\d+) remaining/i);
      const images = Array.from(document.images).filter(visible);
      return {
        studioRoute: location.pathname === '/app/enhance',
        refineRoutePresent: new URLSearchParams(location.search).has('refineImageId'),
        studioSourceRequestCount: performance.getEntriesByType('resource').filter(entry => entry.name.includes('/studio-source')).length,
        visibleImageCount: images.length,
        loadedImageCount: images.filter(image => image.complete && image.naturalWidth > 0).length,
        failedImageCount: images.filter(image => image.complete && image.naturalWidth === 0).length,
        generated: progress ? Number(progress[1]) : null,
        total: progress ? Number(progress[2]) : null,
        remaining: remaining ? Number(remaining[1]) : null,
        countsReconcile: !!progress && !!remaining && Number(progress[1]) + Number(remaining[1]) === Number(progress[2]),
        horizontalOverflow: document.documentElement.scrollWidth > window.innerWidth,
        alertVisible: Array.from(document.querySelectorAll('[role=alert]')).some(visible)
      };
    })()""")


desktop = inspect()
assert desktop['studioRoute'] and desktop['refineRoutePresent']
assert desktop['studioSourceRequestCount'] == 1
assert desktop['loadedImageCount'] > 0 and desktop['failedImageCount'] == 0
assert desktop['countsReconcile'] and not desktop['horizontalOverflow'] and not desktop['alertVisible']

cdp('Emulation.setDeviceMetricsOverride', width=390, height=844, deviceScaleFactor=1, mobile=True)
try:
    mobile = inspect()
    mobile['viewportWidth'] = js("window.innerWidth")
finally:
    cdp('Emulation.clearDeviceMetricsOverride')

assert mobile['viewportWidth'] in (390, 391)
assert mobile['studioSourceRequestCount'] == 1
assert mobile['countsReconcile'] and not mobile['horizontalOverflow'] and not mobile['alertVisible']
print(json.dumps({'desktop': desktop, 'mobile': mobile}, sort_keys=True))
