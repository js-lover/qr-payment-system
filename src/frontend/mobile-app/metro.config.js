const path = require('path');
const fs = require('fs');
const { getDefaultConfig } = require('expo/metro-config');

const config = getDefaultConfig(__dirname);

// @babel/runtime 7.x helpers reference each other with explicit .js extensions
// (e.g. require("./iterableToArrayLimit.js")) but Metro 0.83 package-exports
// resolver can't match the .js-suffixed subpath in the exports field.
// Solution: for relative .js imports, compute the absolute path ourselves
// and return it directly if the file exists — skipping Metro's resolver.
config.resolver.resolveRequest = (context, moduleName, platform) => {
  if (
    moduleName.endsWith('.js') &&
    (moduleName.startsWith('./') || moduleName.startsWith('../'))
  ) {
    const originDir = path.dirname(context.originModulePath);
    const absolutePath = path.resolve(originDir, moduleName);
    if (fs.existsSync(absolutePath)) {
      return { type: 'sourceFile', filePath: absolutePath };
    }
  }
  return context.resolveRequest(context, moduleName, platform);
};

module.exports = config;
