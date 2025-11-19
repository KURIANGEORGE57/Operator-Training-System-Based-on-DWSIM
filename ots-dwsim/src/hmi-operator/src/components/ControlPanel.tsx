import { useState } from 'react'
import {
  Box,
  Button,
  TextField,
  Stack,
  Divider,
  Typography,
  Alert,
  Snackbar
} from '@mui/material'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import PauseIcon from '@mui/icons-material/Pause'
import StopIcon from '@mui/icons-material/Stop'
import SaveIcon from '@mui/icons-material/Save'
import { ApiClient } from '../services/ApiClient'

interface ControlPanelProps {
  sessionId: string
}

export default function ControlPanel({ sessionId }: ControlPanelProps) {
  const [tagPath, setTagPath] = useState('Streams.Feed.Temperature')
  const [tagValue, setTagValue] = useState('298.15')
  const [snackbar, setSnackbar] = useState({ open: false, message: '', severity: 'success' as 'success' | 'error' })

  const handleStartSession = async () => {
    try {
      await ApiClient.startSession(sessionId)
      setSnackbar({ open: true, message: 'Session started', severity: 'success' })
    } catch (error: any) {
      setSnackbar({ open: true, message: `Error: ${error.message}`, severity: 'error' })
    }
  }

  const handlePauseSession = async () => {
    try {
      await ApiClient.pauseSession(sessionId)
      setSnackbar({ open: true, message: 'Session paused', severity: 'success' })
    } catch (error: any) {
      setSnackbar({ open: true, message: `Error: ${error.message}`, severity: 'error' })
    }
  }

  const handleStopSession = async () => {
    try {
      await ApiClient.stopSession(sessionId)
      setSnackbar({ open: true, message: 'Session stopped', severity: 'success' })
    } catch (error: any) {
      setSnackbar({ open: true, message: `Error: ${error.message}`, severity: 'error' })
    }
  }

  const handleWriteTag = async () => {
    try {
      await ApiClient.writeTag(sessionId, tagPath, {
        value: parseFloat(tagValue),
        user: 'operator',
        mode: 'manual',
      })
      setSnackbar({ open: true, message: `Tag written: ${tagPath} = ${tagValue}`, severity: 'success' })
    } catch (error: any) {
      setSnackbar({ open: true, message: `Error: ${error.message}`, severity: 'error' })
    }
  }

  const handleCreateSnapshot = async () => {
    try {
      const name = `Snapshot-${new Date().toLocaleTimeString()}`
      await ApiClient.createSnapshot(sessionId, name)
      setSnackbar({ open: true, message: `Snapshot created: ${name}`, severity: 'success' })
    } catch (error: any) {
      setSnackbar({ open: true, message: `Error: ${error.message}`, severity: 'error' })
    }
  }

  return (
    <Box>
      <Stack spacing={2}>
        {/* Session Controls */}
        <Box>
          <Typography variant="subtitle2" gutterBottom>
            Session Control
          </Typography>
          <Stack direction="row" spacing={1}>
            <Button
              variant="contained"
              color="success"
              size="small"
              startIcon={<PlayArrowIcon />}
              onClick={handleStartSession}
            >
              Start
            </Button>
            <Button
              variant="contained"
              color="warning"
              size="small"
              startIcon={<PauseIcon />}
              onClick={handlePauseSession}
            >
              Pause
            </Button>
            <Button
              variant="contained"
              color="error"
              size="small"
              startIcon={<StopIcon />}
              onClick={handleStopSession}
            >
              Stop
            </Button>
          </Stack>
        </Box>

        <Divider />

        {/* Tag Write */}
        <Box>
          <Typography variant="subtitle2" gutterBottom>
            Write Tag
          </Typography>
          <Stack spacing={1}>
            <TextField
              size="small"
              label="Tag Path"
              value={tagPath}
              onChange={(e) => setTagPath(e.target.value)}
              placeholder="Streams.Feed.Temperature"
              fullWidth
            />
            <TextField
              size="small"
              label="Value"
              type="number"
              value={tagValue}
              onChange={(e) => setTagValue(e.target.value)}
              fullWidth
            />
            <Button variant="outlined" onClick={handleWriteTag} fullWidth>
              Write
            </Button>
          </Stack>
        </Box>

        <Divider />

        {/* Snapshot Control */}
        <Box>
          <Typography variant="subtitle2" gutterBottom>
            Snapshot
          </Typography>
          <Button
            variant="outlined"
            color="primary"
            size="small"
            startIcon={<SaveIcon />}
            onClick={handleCreateSnapshot}
            fullWidth
          >
            Create Snapshot
          </Button>
        </Box>
      </Stack>

      {/* Snackbar for notifications */}
      <Snackbar
        open={snackbar.open}
        autoHideDuration={4000}
        onClose={() => setSnackbar({ ...snackbar, open: false })}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
      >
        <Alert severity={snackbar.severity} sx={{ width: '100%' }}>
          {snackbar.message}
        </Alert>
      </Snackbar>
    </Box>
  )
}
