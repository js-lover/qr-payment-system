const { getDefaultConfig } = require('expo/metro-config');

const config = getDefaultConfig(__dirname);

// @babel/runtime 7.x helpers reference each other with explicit .js extensions
// (e.g. slicedToArray.js → require("./iterableToArrayLimit.js")).
// Metro 0.83 package-exports resolver only finds entries WITHOUT .js in the
// exports field, so resolution fails. Custom resolveRequest strips the .js
// extension and retries — context.resolveRequest is Metro's built-in resolver
// (not this function), so there is no infinite recursion.
config.resolver.resolveRequest = (context, moduleName, platform) => {
  if (
    moduleName.endsWith('.js') &&
    (moduleName.startsWith('./') || moduleName.startsWith('../'))
  ) {
    try {
      return context.resolveRequest(context, moduleName.slice(0, -3), platform);
    } catch {
      // fall through to original name below
    }
  }
  return context.resolveRequest(context, moduleName, platform);
};

module.exports = config;
