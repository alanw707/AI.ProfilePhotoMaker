import { defineConfig } from 'vite';

export default defineConfig({
  server: {
    port: 4200,
    proxy: {
      // Proxy all /api requests to the backend
      '/api': {
        target: 'http://localhost:5035',
        changeOrigin: true,
        secure: false,
        // Log proxy requests for debugging
        configure: (proxy, options) => {
          proxy.on('proxyReq', (proxyReq, req, res) => {
            console.log('Proxying:', req.method, req.url, '->', options.target + req.url);
          });
        }
      }
    }
  }
});