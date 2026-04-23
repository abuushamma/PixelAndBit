/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./PixelAndBit.Web/**/*.cshtml",
    "./PixelAndBit.Web/**/*.razor",
    "./PixelAndBit.Web/wwwroot/js/**/*.js",
  ],
  corePlugins: {
    preflight: false,
  },
  theme: {
    extend: {
      fontFamily: {
        sans: ["Inter", "Cairo", "system-ui", "sans-serif"],
      },
      boxShadow: {
        nav: "0 8px 40px rgba(0,0,0,0.38)",
        "nav-scrolled": "0 12px 48px rgba(0,0,0,0.45)",
      },
      backgroundImage: {
        "hero-mesh":
          "radial-gradient(ellipse 80% 55% at 100% 0%, rgba(168,85,247,0.16), transparent 50%), radial-gradient(ellipse 70% 50% at 0% 0%, rgba(59,130,246,0.12), transparent 48%), radial-gradient(ellipse 60% 45% at 50% 100%, rgba(34,211,238,0.08), transparent 55%), linear-gradient(to bottom, #050816, #03050f)",
        "hero-grid":
          "linear-gradient(rgba(255,255,255,0.06) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.06) 1px, transparent 1px)",
      },
    },
  },
  plugins: [],
};
