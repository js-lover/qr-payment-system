const { getDefaultConfig } = require('expo/metro-config');

const config = getDefaultConfig(__dirname);

// Metro 0.83 enables package.json `exports` field resolution by default.
// @babel/runtime exports field maps helpers WITHOUT .js extension, but
// the helper files internally require each other WITH .js (e.g.
// slicedToArray.js → require("./iterableToArrayLimit.js")). Metro's
// exports resolver can't match the .js-suffixed path → bundle fails.
config.resolver.unstable_enablePackageExports = false;

module.exports = config;
