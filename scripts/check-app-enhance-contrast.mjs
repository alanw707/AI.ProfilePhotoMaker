const pairs = [
  ['Production ink / proof paper', '#202529', '#f7f4ed'],
  ['Muted copy / proof paper', '#4d555b', '#f7f4ed'],
  ['White / proof cobalt', '#ffffff', '#2457c5'],
  ['Deep cobalt / proof paper', '#173f99', '#f7f4ed'],
  ['Proof red / paper white', '#9f3028', '#fffdf8'],
  ['Completion green / paper white', '#246b4b', '#fffdf8'],
];

function luminance(hex) {
  const channels = hex
    .slice(1)
    .match(/../g)
    .map(value => parseInt(value, 16) / 255)
    .map(value => (value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4));
  return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
}

let failed = false;
for (const [name, foreground, background] of pairs) {
  const foregroundLuminance = luminance(foreground);
  const backgroundLuminance = luminance(background);
  const ratio =
    (Math.max(foregroundLuminance, backgroundLuminance) + 0.05) /
    (Math.min(foregroundLuminance, backgroundLuminance) + 0.05);
  console.log(`${name}: ${ratio.toFixed(2)}:1`);
  failed ||= ratio < 4.5;
}

if (failed) process.exitCode = 1;
