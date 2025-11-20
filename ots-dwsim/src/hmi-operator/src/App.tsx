import React, { useEffect, useState } from 'react';
import {
  AppBar,
  Box,
  Container,
  CssBaseline,
  Grid,
  Paper,
  Toolbar,
  Typography,
  IconButton,
  Chip,
  ThemeProvider,
  createTheme,
} from '@mui/material';
import {
  PlayArrow,
  Pause,
  Stop,
  Refresh,
} from '@mui/icons-material';
import ProcessMimic from './components/ProcessMimic';
import TrendChart from './components/TrendChart';
import ControlPanel from './components/ControlPanel';
import AlarmList from './components/AlarmList';
import ApiClient from './services/ApiClient';
import { Session } from './types';

const darkTheme = createTheme({
  palette: {
    mode: 'dark',
    primary: {
      main: '#1976d2',
    },
    secondary: {
      main: '#dc004e',
    },
    background: {
      default: '#121212',
      paper: '#1e1e1e',
    },
  },
});

function App() {
  const [session, setSession] = useState<Session | null>(null);
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Auto-refresh session data
  useEffect(() => {
    if (!sessionId) return;

    const loadSession = async () => {
      try {
        const data = await ApiClient.getSession(sessionId);
        setSession(data);
        setError(null);
      } catch (err: any) {
        setError(err.message || 'Failed to load session');
      }
    };

    loadSession();
    const interval = setInterval(loadSession, 2000); // Refresh every 2 seconds

    return () => clearInterval(interval);
  }, [sessionId]);

  // Load or create initial session
  useEffect(() => {
    const initializeSession = async () => {
      setLoading(true);
      try {
        // Try to get existing sessions
        const sessions = await ApiClient.listSessions();

        if (sessions.length > 0) {
          // Use first session
          setSessionId(sessions[0].sessionId);
        } else {
          // Create new session
          const newSession = await ApiClient.createSession({
            flowsheet: 'sample.dwxmz',
            sessionName: 'Operator Training Session',
          });
          setSessionId(newSession.sessionId);
        }
      } catch (err: any) {
        setError(err.message || 'Failed to initialize session');
      } finally {
        setLoading(false);
      }
    };

    initializeSession();
  }, []);

  const handleStart = async () => {
    if (!sessionId) return;
    try {
      await ApiClient.startSession(sessionId, 1.0);
    } catch (err: any) {
      setError(err.message);
    }
  };

  const handlePause = async () => {
    if (!sessionId) return;
    try {
      await ApiClient.pauseSession(sessionId);
    } catch (err: any) {
      setError(err.message);
    }
  };

  const handleStop = async () => {
    if (!sessionId) return;
    try {
      await ApiClient.stopSession(sessionId);
    } catch (err: any) {
      setError(err.message);
    }
  };

  const getStatusColor = (status?: string) => {
    switch (status) {
      case 'Running':
        return 'success';
      case 'Paused':
        return 'warning';
      case 'Stopped':
        return 'default';
      case 'Error':
        return 'error';
      default:
        return 'info';
    }
  };

  if (loading) {
    return (
      <ThemeProvider theme={darkTheme}>
        <CssBaseline />
        <Box display="flex" justifyContent="center" alignItems="center" minHeight="100vh">
          <Typography variant="h4">Loading session...</Typography>
        </Box>
      </ThemeProvider>
    );
  }

  return (
    <ThemeProvider theme={darkTheme}>
      <CssBaseline />
      <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
        {/* App Bar */}
        <AppBar position="static">
          <Toolbar>
            <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
              DWSIM OTS - Operator HMI
            </Typography>

            {session && (
              <>
                <Chip
                  label={session.status}
                  color={getStatusColor(session.status)}
                  sx={{ mr: 2 }}
                />
                <IconButton color="inherit" onClick={handleStart} title="Start">
                  <PlayArrow />
                </IconButton>
                <IconButton color="inherit" onClick={handlePause} title="Pause">
                  <Pause />
                </IconButton>
                <IconButton color="inherit" onClick={handleStop} title="Stop">
                  <Stop />
                </IconButton>
                <IconButton color="inherit" onClick={() => window.location.reload()} title="Refresh">
                  <Refresh />
                </IconButton>
              </>
            )}
          </Toolbar>
        </AppBar>

        {/* Main Content */}
        <Container maxWidth={false} sx={{ mt: 2, mb: 2, flexGrow: 1 }}>
          {error && (
            <Paper sx={{ p: 2, mb: 2, bgcolor: 'error.dark' }}>
              <Typography color="error.contrastText">{error}</Typography>
            </Paper>
          )}

          <Grid container spacing={2}>
            {/* Process Mimic - Top Left */}
            <Grid item xs={12} md={8}>
              <Paper sx={{ p: 2, height: '400px' }}>
                <Typography variant="h6" gutterBottom>
                  Process Overview
                </Typography>
                {sessionId && <ProcessMimic sessionId={sessionId} />}
              </Paper>
            </Grid>

            {/* Alarms - Top Right */}
            <Grid item xs={12} md={4}>
              <Paper sx={{ p: 2, height: '400px', overflow: 'auto' }}>
                <Typography variant="h6" gutterBottom>
                  Active Alarms
                </Typography>
                <AlarmList />
              </Paper>
            </Grid>

            {/* Trend Chart - Middle */}
            <Grid item xs={12}>
              <Paper sx={{ p: 2, height: '300px' }}>
                <Typography variant="h6" gutterBottom>
                  Real-Time Trends
                </Typography>
                {sessionId && <TrendChart sessionId={sessionId} />}
              </Paper>
            </Grid>

            {/* Control Panel - Bottom */}
            <Grid item xs={12}>
              <Paper sx={{ p: 2 }}>
                <Typography variant="h6" gutterBottom>
                  Control Panel
                </Typography>
                {sessionId && <ControlPanel sessionId={sessionId} />}
              </Paper>
            </Grid>
          </Grid>
        </Container>

        {/* Footer */}
        <Box component="footer" sx={{ py: 2, px: 2, mt: 'auto', backgroundColor: 'background.paper' }}>
          <Typography variant="body2" color="text.secondary" align="center">
            {session && `Session: ${session.sessionName} | Time Factor: ${session.timeFactor}x`}
          </Typography>
        </Box>
      </Box>
    </ThemeProvider>
  );
}

export default App;
