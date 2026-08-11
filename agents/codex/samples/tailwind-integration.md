TailwindCSS integration notes for Angular v22

1. Install Tailwind and peer deps:

```bash
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init
```

2. Configure `tailwind.config.js` to include Angular component files:

```js
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: { extend: {} },
  plugins: [],
};
```

3. Add Tailwind directives to global styles (e.g., `src/styles.css`):

```css
@tailwind base;
@tailwind components;
@tailwind utilities;
```

4. Ensure your `angular.json` builds include the global styles file.

5. Use Tailwind utility classes in component templates (examples provided in `items-list.component.html`).

Notes:
- Prefer utility classes for layout; keep component CSS minimal.
- For production builds, enable `purge` (content) in `tailwind.config.js` to remove unused styles.
