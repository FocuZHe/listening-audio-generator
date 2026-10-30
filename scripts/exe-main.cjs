process.env.NODE_ENV = process.env.NODE_ENV || "production";
global.LISTENING_EMBEDDED_ASSETS = require("../build/embedded-assets.cjs");
require("../server.cjs");
