/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        primary: {
          50:  '#e6f7fa',
          100: '#cceff5',
          200: '#99dfeb',
          300: '#66cfe1',
          400: '#33bfd7',
          500: '#0dafc8',  // main teal
          600: '#0d8fa5',  // darker teal (CTA)
          700: '#0a6f82',
          800: '#074f5e',
          900: '#042f3b',
        },
        navy: {
          800: '#0d1b2a',
          700: '#142436',
          600: '#1e3a4a',
          500: '#2a4f65',
        },
        surface: '#f5f7fa',
        border: '#eef0f4',
      },
      fontFamily: {
        sans: ['DM Sans', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'monospace'],
      },
      borderRadius: { xl: '14px', '2xl': '20px' },
      boxShadow: {
        card: '0 1px 3px rgba(0,0,0,0.06), 0 1px 2px rgba(0,0,0,0.04)',
        modal: '0 20px 60px rgba(0,0,0,0.15)',
      }
    }
  },
  plugins: []
}
