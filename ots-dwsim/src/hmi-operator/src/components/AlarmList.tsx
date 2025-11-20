import React, { useState, useEffect } from 'react';
import {
  List,
  ListItem,
  ListItemText,
  Chip,
  Box,
  Typography,
} from '@mui/material';
import { Warning, Error as ErrorIcon, Info } from '@mui/icons-material';
import { Alarm } from '../types';

const AlarmList: React.FC = () => {
  const [alarms, setAlarms] = useState<Alarm[]>([
    {
      id: '1',
      severity: 'warning',
      message: 'High temperature in top product stream',
      timestamp: new Date(Date.now() - 120000).toISOString(),
      acknowledged: false,
      tag: 'Streams.Top.Temperature',
      value: 385.5,
    },
    {
      id: '2',
      severity: 'info',
      message: 'Simulation started',
      timestamp: new Date(Date.now() - 300000).toISOString(),
      acknowledged: true,
    },
  ]);

  useEffect(() => {
    // In a real implementation, this would poll an alarm endpoint
    // or receive alarm updates via WebSocket
    const interval = setInterval(() => {
      // Simulate new alarms occasionally
      if (Math.random() > 0.95) {
        const newAlarm: Alarm = {
          id: Date.now().toString(),
          severity: Math.random() > 0.5 ? 'warning' : 'info',
          message: `Random alarm at ${new Date().toLocaleTimeString()}`,
          timestamp: new Date().toISOString(),
          acknowledged: false,
        };
        setAlarms((prev) => [newAlarm, ...prev].slice(0, 10));
      }
    }, 5000);

    return () => clearInterval(interval);
  }, []);

  const getSeverityIcon = (severity: string) => {
    switch (severity) {
      case 'critical':
        return <ErrorIcon sx={{ color: '#f44336' }} />;
      case 'warning':
        return <Warning sx={{ color: '#ff9800' }} />;
      default:
        return <Info sx={{ color: '#2196f3' }} />;
    }
  };

  const getSeverityColor = (severity: string): 'error' | 'warning' | 'info' | 'default' => {
    switch (severity) {
      case 'critical':
        return 'error';
      case 'warning':
        return 'warning';
      case 'info':
        return 'info';
      default:
        return 'default';
    }
  };

  const formatTimestamp = (timestamp: string) => {
    const date = new Date(timestamp);
    return date.toLocaleTimeString();
  };

  if (alarms.length === 0) {
    return (
      <Box sx={{ textAlign: 'center', py: 4 }}>
        <Typography variant="body2" color="text.secondary">
          No active alarms
        </Typography>
      </Box>
    );
  }

  return (
    <List dense>
      {alarms.map((alarm) => (
        <ListItem
          key={alarm.id}
          sx={{
            borderLeft: `4px solid`,
            borderLeftColor: alarm.severity === 'critical' ? '#f44336' : alarm.severity === 'warning' ? '#ff9800' : '#2196f3',
            mb: 1,
            bgcolor: alarm.acknowledged ? 'action.hover' : 'background.paper',
            borderRadius: 1,
          }}
        >
          <Box sx={{ mr: 1 }}>
            {getSeverityIcon(alarm.severity)}
          </Box>
          <ListItemText
            primary={
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Typography variant="body2" sx={{ flex: 1 }}>
                  {alarm.message}
                </Typography>
                <Chip
                  label={alarm.severity.toUpperCase()}
                  size="small"
                  color={getSeverityColor(alarm.severity)}
                  sx={{ minWidth: 80 }}
                />
              </Box>
            }
            secondary={
              <Typography variant="caption" color="text.secondary">
                {formatTimestamp(alarm.timestamp)}
                {alarm.tag && ` | ${alarm.tag}`}
                {alarm.value !== undefined && ` = ${alarm.value}`}
              </Typography>
            }
          />
        </ListItem>
      ))}
    </List>
  );
};

export default AlarmList;
