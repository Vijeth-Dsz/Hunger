const target = process.env.HUNGER_API_URL;

if (!target) {
  throw new Error('HUNGER_API_URL must be configured before starting the frontend.');
}

module.exports = {
  '/api': {
    target,
    secure: false,
    changeOrigin: true,
  },
};
