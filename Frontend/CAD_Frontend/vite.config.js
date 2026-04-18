import { defineConfig, loadEnv } from 'vite'   
import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'

export default defineConfig(({ mode }) => {     
  const env = loadEnv(mode, process.cwd(), '')  

  return {
    plugins: [
      react(),
      babel({ presets: [reactCompilerPreset()] })
    ],
    server: {
      proxy: {
        '/api': {
          target: env.VITE_API_URL,           
          changeOrigin: true,
          secure: false,
        },
      },
    },
  }
})