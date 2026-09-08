/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        kraft: {
          50:  '#fdf8f0',
          100: '#faf0de',
          200: '#f4deb0',
          300: '#eac678',
          400: '#e0aa48',
          500: '#d4912a',
          600: '#c07820',
          700: '#9e5f1b',
          800: '#7e4c1d',
          900: '#663e1b',
        },
        postal: {
          navy:  '#1e3a5f',
          red:   '#c62828',
          blue:  '#1565c0',
          wax:   '#8b0000',
          stamp: '#2e7d32',
        },
        parchment: '#fffef5',
        ink: '#2c2417',
      },
      fontFamily: {
        handwritten: ['Caveat', 'cursive'],
        serif: ['"Playfair Display"', 'Georgia', 'serif'],
        mono: ['"Courier Prime"', 'monospace'],
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
      keyframes: {
        waddle: {
          '0%, 100%': { transform: 'rotate(-4deg) translateY(0)' },
          '25%': { transform: 'rotate(4deg) translateY(-4px)' },
          '75%': { transform: 'rotate(-2deg) translateY(-2px)' },
        },
        'letter-float': {
          '0%, 100%': { transform: 'translateY(0) rotate(-1.5deg)' },
          '50%': { transform: 'translateY(-10px) rotate(1.5deg)' },
        },
        'seal-glow': {
          '0%, 100%': { boxShadow: '0 0 6px 0 currentColor' },
          '50%': { boxShadow: '0 0 18px 4px currentColor' },
        },
        'stamp-down': {
          '0%': { transform: 'scale(1.4) rotate(-12deg)', opacity: '0' },
          '60%': { transform: 'scale(0.92) rotate(2deg)', opacity: '1' },
          '80%': { transform: 'scale(1.04) rotate(-1deg)' },
          '100%': { transform: 'scale(1) rotate(0deg)', opacity: '1' },
        },
        typewriter: {
          from: { clipPath: 'inset(0 100% 0 0)' },
          to:   { clipPath: 'inset(0 0% 0 0)' },
        },
        'bounce-in': {
          '0%': { transform: 'scale(0)', opacity: '0' },
          '60%': { transform: 'scale(1.15)' },
          '80%': { transform: 'scale(0.95)' },
          '100%': { transform: 'scale(1)', opacity: '1' },
        },
        'duck-happy': {
          '0%, 100%': { transform: 'rotate(-6deg) scale(1.02)' },
          '50%': { transform: 'rotate(6deg) scale(1.05)' },
        },
      },
      animation: {
        waddle: 'waddle 0.6s ease-in-out infinite',
        'letter-float': 'letter-float 3.2s ease-in-out infinite',
        'seal-glow': 'seal-glow 2s ease-in-out infinite',
        'stamp-down': 'stamp-down 0.45s cubic-bezier(0.22, 1, 0.36, 1) forwards',
        typewriter: 'typewriter 1.2s steps(40) forwards',
        'bounce-in': 'bounce-in 0.5s cubic-bezier(0.22, 1, 0.36, 1) forwards',
        'duck-happy': 'duck-happy 0.4s ease-in-out infinite',
      },
      backgroundImage: {
        'kraft-texture': "url(\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='400' height='400'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.65' numOctaves='3' stitchTiles='stitch'/%3E%3CfeColorMatrix type='saturate' values='0'/%3E%3C/filter%3E%3Crect width='400' height='400' filter='url(%23n)' opacity='0.05'/%3E%3C/svg%3E\")",
      },
    },
  },
  plugins: [],
};
