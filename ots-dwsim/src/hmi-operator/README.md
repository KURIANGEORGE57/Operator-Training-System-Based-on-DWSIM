# DWSIM OTS - Operator HMI

Modern web-based Human-Machine Interface (HMI) for DWSIM Operator Training System.

## Features

✅ **Process Mimic** - SVG-based process visualization
✅ **Real-time Tag Display** - Live process variable monitoring
✅ **Trend Charts** - Historical data visualization with Recharts
✅ **Control Panel** - Session control and tag manipulation
✅ **Material-UI Design** - Professional dark theme interface
✅ **TypeScript** - Type-safe React components
✅ **API Integration** - Complete REST API client for OTS backend

## Tech Stack

- **React 18** - Modern React with hooks
- **TypeScript** - Type safety and better DX
- **Vite** - Fast development and builds
- **Material-UI (MUI)** - Professional UI components
- **Recharts** - Beautiful, responsive charts
- **Axios** - HTTP client for API calls

## Getting Started

### Prerequisites

- Node.js 18+ and npm
- DWSIM OTS Backend running on `http://localhost:5000`

### Installation

```bash
cd ots-dwsim/src/hmi-operator
npm install
```

### Development

```bash
npm run dev
```

Opens on `http://localhost:3000`

### Build for Production

```bash
npm run build
```

Output in `dist/` directory.

## Project Structure

```
src/
├── components/           # React components
│   ├── ProcessMimic.tsx     # SVG process diagram
│   ├── TagDisplay.tsx       # Real-time tag table
│   ├── TrendChart.tsx       # Line chart trends
│   └── ControlPanel.tsx     # Session/tag controls
├── services/
│   └── ApiClient.ts         # REST API client
├── App.tsx              # Main application
├── main.tsx             # Entry point
└── index.css            # Global styles
```

## Components

### ProcessMimic

Simple SVG-based process flow diagram showing:
- Feed stream with temperature display
- Reactor unit
- Product stream with temperature
- Flow directions

**Customization:** Edit the SVG in `ProcessMimic.tsx` to match your flowsheet.

### TagDisplay

Real-time table displaying process variables:
- Updates every 1 second
- Shows value, units, and status
- Color-coded for easy reading
- Configurable tag list

**Add tags:** Modify `TAG_CONFIGS` in `TagDisplay.tsx`.

### TrendChart

Line chart showing historical trends:
- Dual Y-axes for different units
- Keeps last 20 data points
- 2-second update interval
- Tooltips and legend

**Customize:** Change `dataKey` and colors in `TrendChart.tsx`.

### ControlPanel

Operator controls:
- Start/Pause/Stop session
- Write tag values
- Create snapshots
- Snackbar notifications

## API Client

The `ApiClient` service provides methods for:

- `createSession()` - Create new training session
- `getSession()` - Get session info
- `startSession()` - Start simulation
- `pauseSession()` - Pause simulation
- `stopSession()` - Stop simulation
- `readTag()` - Read tag value
- `writeTag()` - Write tag value
- `createSnapshot()` - Save state
- `restoreSnapshot()` - Restore state
- `getEvents()` - Get event log

## Customization

### Change API URL

Edit `.env` or set environment variable:

```bash
VITE_API_URL=http://your-backend:5000/api/v1
```

### Add More Tags

Edit `TAG_CONFIGS` in `TagDisplay.tsx`:

```typescript
const TAG_CONFIGS: TagConfig[] = [
  { path: 'Streams.YourStream.Temperature', label: 'Your Label' },
  // Add more...
]
```

### Modify Process Diagram

Edit SVG in `ProcessMimic.tsx`:

```tsx
<svg width="600" height="400" viewBox="0 0 600 400">
  {/* Your custom SVG here */}
</svg>
```

### Change Theme

Edit theme in `App.tsx`:

```typescript
const darkTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: { main: '#yourcolor' },
    // ...
  },
})
```

## Development Tips

### Hot Reload

Vite provides instant hot module replacement (HMR). Changes appear immediately without full page reload.

### TypeScript

Type errors show in the terminal and IDE. Fix them before building:

```bash
npm run build  # Runs tsc to check types
```

### Linting

```bash
npm run lint
```

### API Proxy

Vite dev server proxies `/api` to `http://localhost:5000` automatically. No CORS issues during development.

## Production Deployment

### Build

```bash
npm run build
```

### Serve Static Files

Use any static file server:

```bash
# Using serve
npx serve -s dist -l 3000

# Using nginx
# Copy dist/ to /var/www/html/
```

### Docker

```dockerfile
FROM node:18 AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
```

## Troubleshooting

### Can't Connect to Backend

Check:
1. Backend is running on `http://localhost:5000`
2. API endpoint is correct
3. CORS is enabled on backend
4. Firewall allows connection

### Tags Not Updating

Check:
1. Session is created and started
2. Tag paths match your flowsheet
3. Browser console for errors
4. Network tab shows API calls

### Build Errors

```bash
# Clear node_modules and reinstall
rm -rf node_modules package-lock.json
npm install

# Clear Vite cache
rm -rf node_modules/.vite
npm run dev
```

## Future Enhancements

- [ ] WebSocket for real-time updates
- [ ] Alarm list with colors and sounds
- [ ] Multi-page navigation
- [ ] User authentication
- [ ] Scenario execution controls
- [ ] Event log viewer
- [ ] Advanced trend configuration
- [ ] Mobile-responsive design
- [ ] Touchscreen-optimized controls

## Contributing

To add new features:

1. Create component in `src/components/`
2. Add to `App.tsx` grid layout
3. Update API client if needed
4. Test with actual backend
5. Update this README

## License

Same as DWSIM OTS project (GPL-3.0)

## Support

For issues or questions:
- Check backend logs
- Check browser console
- Review API responses in Network tab
- Consult DWSIM OTS documentation

---

**Happy Operating! 🎛️**
