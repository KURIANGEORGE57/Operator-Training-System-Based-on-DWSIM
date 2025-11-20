import React, { useEffect, useState } from 'react';
import {
  Paper,
  Box,
  Typography,
  List,
  ListItem,
  ListItemText,
  Chip,
  TextField,
  MenuItem,
} from '@mui/material';
import ApiClient from '../services/ApiClient';
import { Session, EventLogEntry } from '../types';

const EventTimeline: React.FC = () => {
  const [sessions, setSessions] = useState<Session[]>([]);
  const [selectedSessionId, setSelectedSessionId] = useState<string>('');
  const [events, setEvents] = useState<EventLogEntry[]>([]);

  useEffect(() => {
    const loadSessions = async () => {
      try {
        const data = await ApiClient.listSessions();
        setSessions(data);
        if (data.length > 0 && !selectedSessionId) {
          setSelectedSessionId(data[0].sessionId);
        }
      } catch (error) {
        console.error('Failed to load sessions:', error);
      }
    };
    loadSessions();
  }, [selectedSessionId]);

  useEffect(() => {
    if (!selectedSessionId) return;

    const loadEvents = async () => {
      try {
        const data = await ApiClient.getSessionEvents(selectedSessionId);
        setEvents(data);
      } catch (error) {
        console.error('Failed to load events:', error);
      }
    };

    loadEvents();
    const interval = setInterval(loadEvents, 3000);
    return () => clearInterval(interval);
  }, [selectedSessionId]);

  const getEventColor = (eventType: string) => {
    switch (eventType) {
      case 'session_start': return 'success';
      case 'session_stop': return 'default';
      case 'operator_action': return 'primary';
      case 'fault_injection': return 'error';
      case 'snapshot_created': return 'info';
      default: return 'default';
    }
  };

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h5">Event Timeline</Typography>
        <TextField
          select
          size="small"
          value={selectedSessionId}
          onChange={(e) => setSelectedSessionId(e.target.value)}
          sx={{ minWidth: 200 }}
          label="Select Session"
        >
          {sessions.map((session) => (
            <MenuItem key={session.sessionId} value={session.sessionId}>
              {session.sessionName}
            </MenuItem>
          ))}
        </TextField>
      </Box>

      <Paper sx={{ p: 2, height: '600px', overflow: 'auto' }}>
        {events.length === 0 ? (
          <Typography color="text.secondary" align="center">
            No events recorded yet
          </Typography>
        ) : (
          <List>
            {events.slice().reverse().map((event, index) => (
              <ListItem
                key={index}
                sx={{
                  borderLeft: 4,
                  borderLeftColor: getEventColor(event.type) + '.main',
                  mb: 1,
                  bgcolor: 'background.default',
                }}
              >
                <ListItemText
                  primary={
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                      <Chip label={event.type} color={getEventColor(event.type)} size="small" />
                      {event.target && <Typography variant="body2">Target: {event.target}</Typography>}
                      {event.value !== undefined && (
                        <Typography variant="body2">Value: {JSON.stringify(event.value)}</Typography>
                      )}
                    </Box>
                  }
                  secondary={
                    <Typography variant="caption" color="text.secondary">
                      {new Date(event.realTime).toLocaleString()}
                      {event.user && ` | User: ${event.user}`}
                      {event.action && ` | Action: ${event.action}`}
                    </Typography>
                  }
                />
              </ListItem>
            ))}
          </List>
        )}
      </Paper>
    </Box>
  );
};

export default EventTimeline;
