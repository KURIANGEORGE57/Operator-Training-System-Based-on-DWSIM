import { useState, useEffect } from 'react'
import { ThemeProvider, createTheme } from '@mui/material/styles'
import { CssBaseline, Box, AppBar, Toolbar, Typography, Container, Grid, Paper } from '@mui/material'
import ProcessMimic from './components/ProcessMimic'
import TagDisplay from './components/TagDisplay'
import TrendChart from './components/TrendChart'
import ControlPanel from './components/ControlPanel'
import { ApiClient, SessionInfo } from './services/ApiClient'

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
})

function App() {
  const [sessionId, setSessionId] = useState<string>('')
  const [sessionInfo, setSessionInfo] = useState<SessionInfo | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    // Try to load existing session or create new one
    const loadOrCreateSession = async () => {
      try {
        // Check if session ID in localStorage
        const savedSessionId = localStorage.getItem('dwsim_ots_session_id')

        if (savedSessionId) {
          // Try to load existing session
          const info = await ApiClient.getSession(savedSessionId)
          if (info) {
            setSessionId(savedSessionId)
            setSessionInfo(info)
            setIsLoading(false)
            return
          }
        }

        // Create new session if none exists
        console.log('No active session, ready to create one')
        setIsLoading(false)
      } catch (error) {
        console.error('Error loading session:', error)
        setIsLoading(false)
      }
    }

    loadOrCreateSession()
  }, [])

  const createSession = async (flowsheet: string) => {
    try {
      const response = await ApiClient.createSession({
        flowsheet,
        sessionName: 'Operator Training Session',
        seed: 12345,
      })

      setSessionId(response.sessionId)
      localStorage.setItem('dwsim_ots_session_id', response.sessionId)

      // Get session info
      const info = await ApiClient.getSession(response.sessionId)
      setSessionInfo(info)
    } catch (error) {
      console.error('Error creating session:', error)
    }
  }

  if (isLoading) {
    return (
      <ThemeProvider theme={darkTheme}>
        <CssBaseline />
        <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
          <Typography variant="h6">Loading...</Typography>
        </Box>
      </ThemeProvider>
    )
  }

  if (!sessionId) {
    return (
      <ThemeProvider theme={darkTheme}>
        <CssBaseline />
        <Container maxWidth="md" sx={{ mt: 8 }}>
          <Paper sx={{ p: 4 }}>
            <Typography variant="h4" gutterBottom>
              DWSIM OTS - Operator HMI
            </Typography>
            <Typography variant="body1" paragraph>
              No active session. Please start a new training session.
            </Typography>
            <Typography variant="body2" color="text.secondary">
              Contact your instructor to load a flowsheet and begin training.
            </Typography>
          </Paper>
        </Container>
      </ThemeProvider>
    )
  }

  return (
    <ThemeProvider theme={darkTheme}>
      <CssBaseline />
      <Box sx={{ flexGrow: 1, height: '100vh', display: 'flex', flexDirection: 'column' }}>
        {/* Top App Bar */}
        <AppBar position="static">
          <Toolbar>
            <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
              DWSIM OTS - Operator HMI
            </Typography>
            <Typography variant="body2" sx={{ mr: 2 }}>
              Session: {sessionInfo?.sessionName || sessionId}
            </Typography>
            <Typography variant="body2" sx={{
              px: 2,
              py: 0.5,
              borderRadius: 1,
              bgcolor: sessionInfo?.status === 'Running' ? 'success.main' : 'warning.main'
            }}>
              {sessionInfo?.status || 'Unknown'}
            </Typography>
          </Toolbar>
        </AppBar>

        {/* Main Content */}
        <Box sx={{ flexGrow: 1, p: 2, overflow: 'auto' }}>
          <Grid container spacing={2} sx={{ height: '100%' }}>
            {/* Left Panel - Process Mimic */}
            <Grid item xs={12} md={8}>
              <Paper sx={{ p: 2, height: '100%' }}>
                <Typography variant="h6" gutterBottom>
                  Process Mimic
                </Typography>
                <ProcessMimic sessionId={sessionId} />
              </Paper>
            </Grid>

            {/* Right Panel - Controls and Displays */}
            <Grid item xs={12} md={4}>
              <Grid container spacing={2}>
                {/* Tag Display */}
                <Grid item xs={12}>
                  <Paper sx={{ p: 2 }}>
                    <Typography variant="h6" gutterBottom>
                      Process Variables
                    </Typography>
                    <TagDisplay sessionId={sessionId} />
                  </Paper>
                </Grid>

                {/* Control Panel */}
                <Grid item xs={12}>
                  <Paper sx={{ p: 2 }}>
                    <Typography variant="h6" gutterBottom>
                      Controls
                    </Typography>
                    <ControlPanel sessionId={sessionId} />
                  </Paper>
                </Grid>

                {/* Trend Chart */}
                <Grid item xs={12}>
                  <Paper sx={{ p: 2, height: 300 }}>
                    <Typography variant="h6" gutterBottom>
                      Trends
                    </Typography>
                    <TrendChart sessionId={sessionId} />
                  </Paper>
                </Grid>
              </Grid>
            </Grid>
          </Grid>
        </Box>
      </Box>
    </ThemeProvider>
  )
}

export default App
