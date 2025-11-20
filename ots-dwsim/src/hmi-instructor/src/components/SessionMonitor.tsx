import React, { useEffect, useState } from 'react';
import {
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  IconButton,
  Box,
  Typography,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  TextField,
} from '@mui/material';
import { PlayArrow, Pause, Stop, Refresh, Add } from '@mui/icons-material';
import ApiClient from '../services/ApiClient';
import { Session } from '../types';

const SessionMonitor: React.FC = () => {
  const [sessions, setSessions] = useState<Session[]>([]);
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [newSessionName, setNewSessionName] = useState('');

  const loadSessions = async () => {
    try {
      const data = await ApiClient.listSessions();
      setSessions(data);
    } catch (error) {
      console.error('Failed to load sessions:', error);
    }
  };

  useEffect(() => {
    loadSessions();
    const interval = setInterval(loadSessions, 3000);
    return () => clearInterval(interval);
  }, []);

  const handleCreateSession = async () => {
    try {
      await ApiClient.createSession({
        flowsheet: 'sample.dwxmz',
        sessionName: newSessionName || 'Training Session',
      });
      setCreateDialogOpen(false);
      setNewSessionName('');
      loadSessions();
    } catch (error) {
      console.error('Failed to create session:', error);
    }
  };

  const handleStartSession = async (sessionId: string) => {
    try {
      await ApiClient.startSession(sessionId);
      loadSessions();
    } catch (error) {
      console.error('Failed to start session:', error);
    }
  };

  const handlePauseSession = async (sessionId: string) => {
    try {
      await ApiClient.pauseSession(sessionId);
      loadSessions();
    } catch (error) {
      console.error('Failed to pause session:', error);
    }
  };

  const handleStopSession = async (sessionId: string) => {
    try {
      await ApiClient.stopSession(sessionId);
      loadSessions();
    } catch (error) {
      console.error('Failed to stop session:', error);
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Running': return 'success';
      case 'Paused': return 'warning';
      case 'Stopped': return 'default';
      case 'Error': return 'error';
      default: return 'info';
    }
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h5">Active Training Sessions</Typography>
        <Box>
          <Button
            startIcon={<Add />}
            variant="contained"
            onClick={() => setCreateDialogOpen(true)}
            sx={{ mr: 1 }}
          >
            Create Session
          </Button>
          <IconButton onClick={loadSessions}>
            <Refresh />
          </IconButton>
        </Box>
      </Box>

      <TableContainer component={Paper}>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>Session Name</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Created</TableCell>
              <TableCell>Time Factor</TableCell>
              <TableCell>Flowsheet</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {sessions.length === 0 ? (
              <TableRow>
                <TableCell colSpan={6} align="center">
                  <Typography color="text.secondary">No sessions found. Create one to get started.</Typography>
                </TableCell>
              </TableRow>
            ) : (
              sessions.map((session) => (
                <TableRow key={session.sessionId}>
                  <TableCell>{session.sessionName}</TableCell>
                  <TableCell>
                    <Chip label={session.status} color={getStatusColor(session.status)} size="small" />
                  </TableCell>
                  <TableCell>{new Date(session.createdAt).toLocaleString()}</TableCell>
                  <TableCell>{session.timeFactor}x</TableCell>
                  <TableCell>{session.flowsheetPath.split('/').pop()}</TableCell>
                  <TableCell align="right">
                    <IconButton size="small" onClick={() => handleStartSession(session.sessionId)}>
                      <PlayArrow />
                    </IconButton>
                    <IconButton size="small" onClick={() => handlePauseSession(session.sessionId)}>
                      <Pause />
                    </IconButton>
                    <IconButton size="small" onClick={() => handleStopSession(session.sessionId)}>
                      <Stop />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Dialog open={createDialogOpen} onClose={() => setCreateDialogOpen(false)}>
        <DialogTitle>Create New Session</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            margin="dense"
            label="Session Name"
            fullWidth
            value={newSessionName}
            onChange={(e) => setNewSessionName(e.target.value)}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateDialogOpen(false)}>Cancel</Button>
          <Button onClick={handleCreateSession} variant="contained">Create</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default SessionMonitor;
